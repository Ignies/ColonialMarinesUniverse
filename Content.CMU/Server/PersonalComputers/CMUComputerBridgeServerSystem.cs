using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// The console bridge. Consoles on colony maps whose prototype comes from one of the bridge's parents get the PC OS
/// at runtime: using the console opens the OS, and the screen it opened before becomes a program on the desktop.
/// Mappers and content makers build consoles as always; nothing in their prototypes changes. Switching
/// <see cref="CMUPersonalComputerCVars.Bridge"/> off hands every bridged console straight back to its own screen.
/// </summary>
public sealed class CMUComputerBridgeServerSystem : EntitySystem
{
    private static readonly ProtoId<CMUComputerBridgePrototype> Rules = "Default";
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private CMUComputerBridgeSystem _bridge = default!;
    [Dependency] private CMUPersonalComputerSystem _computers = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>Per prototype: whether it is bridged, and the OS it gets.</summary>
    private readonly Dictionary<string, (bool Bridged, ProtoId<CMUComputerOsPrototype>? Os)> _byPrototype = new();
    private readonly List<(EntityUid Uid, bool Bridge)> _changes = new();
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _byPrototype.Clear());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + CheckEvery;
        Refresh();
    }

    /// <summary>Bridges every console that should be, and lets go of every one that should not.</summary>
    public void Refresh()
    {
        var on = _cfg.GetCVar(CMUPersonalComputerCVars.Enabled) && _cfg.GetCVar(CMUPersonalComputerCVars.Bridge);
        _changes.Clear();
        var consoles = EntityQueryEnumerator<ActivatableUIComponent, TransformComponent, MetaDataComponent>();
        while (consoles.MoveNext(out var uid, out var activatable, out var xform, out var meta))
        {
            var bridged = TryComp(uid, out CMUComputerConsoleComponent? console) && console.OriginalKey != null;
            var wanted = on && ShouldBridge(uid, activatable, xform, meta);
            if (wanted != bridged)
                _changes.Add((uid, wanted));
        }

        foreach (var (uid, bridge) in _changes)
        {
            if (bridge)
                Bridge(uid);
            else
                Release(uid);
        }
    }

    private bool ShouldBridge(EntityUid uid, ActivatableUIComponent activatable, TransformComponent xform, MetaDataComponent meta)
    {
        if (meta.EntityPrototype is not { } proto || !xform.Anchored || xform.MapUid is not { } map || !HasComp<RMCPlanetComponent>(map))
            return false;

        // A computer that already runs the OS of its own, like a desk PC, is not a console.
        if (HasComp<CMUPersonalComputerComponent>(uid) && !HasComp<CMUComputerConsoleComponent>(uid))
            return false;

        if (activatable.Key is CMUPersonalComputerUiKey && !HasComp<CMUComputerConsoleComponent>(uid))
            return false;

        return Classify(proto).Bridged;
    }

    private (bool Bridged, ProtoId<CMUComputerOsPrototype>? Os) Classify(EntityPrototype proto)
    {
        if (_byPrototype.TryGetValue(proto.ID, out var known))
            return known;

        var rules = _prototypes.Index(Rules);
        var included = false;
        ProtoId<CMUComputerOsPrototype>? os = null;
        foreach (var ancestor in Ancestry(proto))
        {
            if (rules.Exclude.Contains(ancestor))
            {
                included = false;
                os = null;
                break;
            }

            if (rules.Include.Contains(ancestor))
                included = true;

            if (os == null && rules.Os.TryGetValue(ancestor, out var match))
                os = match;
        }

        return _byPrototype[proto.ID] = (included, os);
    }

    /// <summary>The prototype and all its parents, abstract ones included, nearest first.</summary>
    private IEnumerable<string> Ancestry(EntityPrototype proto)
    {
        foreach (var (id, _) in _prototypes.EnumerateAllParents<EntityPrototype>(proto.ID, true))
            yield return id;
    }

    private void Bridge(EntityUid uid)
    {
        if (!TryComp(uid, out ActivatableUIComponent? activatable) || TryComp(uid, out MetaDataComponent? meta) && meta.EntityPrototype == null)
            return;

        var key = activatable.Key;
        var os = Classify(MetaData(uid).EntityPrototype!).Os;

        _ui.SetUi(uid, CMUPersonalComputerUiKey.Key, new InterfaceData("CMUPersonalComputerBui"));
        _ui.SetUi(uid, CMUComputerToolUiKey.Key, new InterfaceData("CMUComputerToolBui"));
        _ui.SetUi(uid, CMUComputerLogUiKey.Key, new InterfaceData("CMUComputerLogBui"));

        var computer = EnsureComp<CMUPersonalComputerComponent>(uid);
        if (os is { } chosen)
            computer.Os = chosen;

        _computers.Setup((uid, computer));

        var console = EnsureComp<CMUComputerConsoleComponent>(uid);
        console.Apps = new List<CMUConsoleApp> { new() { Name = Name(uid), Key = key } };
        console.OriginalKey = key;
        Dirty(uid, console);
        _bridge.Apply((uid, console));
    }

    private void Release(EntityUid uid)
    {
        _ui.CloseUi(uid, CMUPersonalComputerUiKey.Key);
        _ui.CloseUi(uid, CMUComputerToolUiKey.Key);
        _ui.CloseUi(uid, CMUComputerLogUiKey.Key);

        // A card left in the reader drops out in front of the console.
        if (_container.TryGetContainer(uid, CMUPersonalComputerComponent.CardSlotId, out var slot))
            _container.EmptyContainer(slot);

        RemComp<CMUPersonalComputerComponent>(uid);
        RemComp<CMUComputerConsoleComponent>(uid);
    }
}
