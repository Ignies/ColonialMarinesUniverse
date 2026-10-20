using Content.Shared._RMC14.Power;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// Whether a terminal can run: the area's equipment power if it has a receiver. With computers switched off the
/// terminal does not open at all, on either side. Without power it opens to its front panel, dark, and the power
/// button does nothing.
/// </summary>
public sealed class CMUComputerPowerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCPowerSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPersonalComputerComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    /// <summary>A terminal without a power receiver, like a mapper's prop, always works.</summary>
    public bool IsPowered(EntityUid computer)
    {
        return !_cfg.GetCVar(CMUPersonalComputerCVars.NeedsPower)
            || !HasComp<RMCPowerReceiverComponent>(computer)
            || _power.IsPowered(computer);
    }

    private void OnOpenAttempt(Entity<CMUPersonalComputerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (_cfg.GetCVar(CMUPersonalComputerCVars.Enabled))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("cmu-pc-offline"), ent, args.User);
    }
}
