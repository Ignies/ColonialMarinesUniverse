using Content.Server.Forensics;
using Content.Shared.Access.Components;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.AU14.ColonyEconomy;

/// <summary>
///     Handles the CLF Network Tap item being used on ATMs to install/retrieve skimmers.
/// </summary>
public sealed class ClfNetworkTapSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClfNetworkTapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClfNetworkTapComponent, InteractUsingEvent>(OnUseOnAtm);
        SubscribeLocalEvent<ClfNetworkTapComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnMapInit(EntityUid uid, ClfNetworkTapComponent comp, MapInitEvent args)
    {
        if (comp.TapId == 0)
            comp.TapId = _random.Next(1000, 99999);
    }

    /// <summary>
    ///     Someone uses the tap ON an ATM (ATM is the Used item).
    ///     This path handles: tap used while holding in hand → interact with ATM.
    /// </summary>
    private void OnAfterInteract(EntityUid uid, ClfNetworkTapComponent tap, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null)
            return;

        if (!HasComp<ColonyAtmComponent>(args.Target.Value))
            return;

        args.Handled = true;
        HandleTapOnAtm(uid, tap, args.Target.Value, args.User);
    }

    /// <summary>
    ///     Someone uses an ATM on the tap (unusual direction, handled by AfterInteract above).
    ///     This is a fallback to ensure both directions work.
    /// </summary>
    private void OnUseOnAtm(EntityUid uid, ClfNetworkTapComponent tap, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<ColonyAtmComponent>(args.Used))
            return;

        args.Handled = true;
        HandleTapOnAtm(uid, tap, args.Used, args.User);
    }

    private void HandleTapOnAtm(EntityUid tapUid, ClfNetworkTapComponent tap, EntityUid atmUid, EntityUid user)
    {
        // If this tap is already linked to this ATM → retrieve data
        if (tap.LinkedAtm == atmUid && HasComp<ColonyAtmSkimmerComponent>(atmUid))
        {
            RetrieveSkimmerData(tapUid, tap, atmUid, user);
            return;
        }

        // If ATM already has a skimmer from a different tap → already compromised
        if (HasComp<ColonyAtmSkimmerComponent>(atmUid))
        {
            _popup.PopupEntity("This terminal already has an active network tap.", atmUid, user);
            return;
        }

        // Install the skimmer
        InstallSkimmer(tapUid, tap, atmUid, user);
    }

    private void InstallSkimmer(EntityUid tapUid, ClfNetworkTapComponent tap, EntityUid atmUid, EntityUid user)
    {
        // Get installer DNA if available
        var dna = string.Empty;
        if (TryComp<DnaComponent>(user, out var dnaComp) && !string.IsNullOrEmpty(dnaComp.DNA))
            dna = dnaComp.DNA;

        var skimmer = EnsureComp<ColonyAtmSkimmerComponent>(atmUid);
        skimmer.InstallerTapId = tap.TapId;
        skimmer.InstallerEntity = user;
        skimmer.InstallerDna = dna;

        // Mark as tampered (permanent)
        EnsureComp<ColonyAtmTamperedComponent>(atmUid);

        // Add DNA to ForensicsComponent so forensic scanners pick it up
        if (!string.IsNullOrEmpty(dna))
        {
            var forensics = EnsureComp<Content.Server.Forensics.ForensicsComponent>(atmUid);
            forensics.DNAs.Add(dna);
        }

        tap.LinkedAtm = atmUid;
        Dirty(tapUid, tap);

        _popup.PopupEntity("Network tap installed. Return later to retrieve captured data.", atmUid, user);
    }

    private void RetrieveSkimmerData(EntityUid tapUid, ClfNetworkTapComponent tap, EntityUid atmUid, EntityUid user)
    {
        if (!TryComp<ColonyAtmSkimmerComponent>(atmUid, out var skimmer))
            return;

        if (skimmer.CapturedAccounts.Count == 0)
        {
            _popup.PopupEntity("No account data captured yet. Come back after more cards have been used.", atmUid, user);
            return;
        }

        // Open the skimmer data UI on the ATM — the ATM server system handles rendering it
        if (TryComp<ColonyAtmComponent>(atmUid, out var atmComp))
        {
            atmComp.Screen = AtmScreen.SkimmerData;
            _ui.TryOpenUi(atmUid, ColonyAtmUi.Key, user);
        }

        // Remove the skimmer (DNA evidence gone) but tampering mark remains
        RemComp<ColonyAtmSkimmerComponent>(atmUid);
        // Also remove DNA from forensics since skimmer removed
        if (TryComp<Content.Server.Forensics.ForensicsComponent>(atmUid, out var forensics)
            && !string.IsNullOrEmpty(skimmer.InstallerDna))
        {
            forensics.DNAs.Remove(skimmer.InstallerDna);
        }

        tap.LinkedAtm = null;
        Dirty(tapUid, tap);

        _popup.PopupEntity($"Retrieved {skimmer.CapturedAccounts.Count} account record(s).", atmUid, user);
    }
}
