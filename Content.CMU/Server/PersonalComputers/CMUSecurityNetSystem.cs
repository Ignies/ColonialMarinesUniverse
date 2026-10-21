using System.Numerics;
using Content.Server.Pinpointer;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// The colony security network: a map of every colony terminal with what it is doing, and a log of unauthorized
/// use. Terminals report refused cards, hack attempts, log tampering and deep lookups as they happen. A dark
/// terminal reports nothing at all: the last thing it sends is that it went dark, and the map keeps it where it
/// was then, so security has to walk over and look at it. Only computers on the security network get answers.
/// </summary>
public sealed class CMUSecurityNetSystem : EntitySystem
{
    public static readonly ProtoId<CMUNetworkPrototype> SecurityNetwork = "CMUNetSecurity";
    private const int MaxEntries = 80;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private CMUComputerPowerSystem _computerPower = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly List<CMUSecurityEntry> _entries = new();

    /// <summary>Where each terminal was when it went dark, in its grid's coordinates.</summary>
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2 Position, string Location)> _lastSeen = new();

    /// <summary>Terminals security saw someone start hacking.</summary>
    private readonly HashSet<EntityUid> _hackSeen = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _entries.Clear();
            _lastSeen.Clear();
            _hackSeen.Clear();
        });
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUSecurityRequestMessage>(OnRequest);
        });
    }

    public IReadOnlyList<CMUSecurityEntry> Entries => _entries;

    /// <summary>The area a terminal stands in, or its own name off any area.</summary>
    public string Location(EntityUid terminal)
    {
        return _areas.TryGetArea(terminal, out _, out var area) ? area.Name : Name(terminal);
    }

    /// <summary>
    /// Puts a line on the unauthorized use log. A dark terminal is off the colony's monitoring, so it reports
    /// nothing; going dark is reported just before it drops off.
    /// </summary>
    public void Report(Entity<CMUPersonalComputerComponent?> terminal, CMUSecurityEvent kind, string? name = null, string? detail = null)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.SecurityNet)
            || !Resolve(terminal, ref terminal.Comp, false)
            || _net.IsDark(terminal.Comp))
            return;

        var location = Location(terminal);
        _entries.Insert(0, new CMUSecurityEntry(_timing.CurTime, terminal.Comp.TerminalId, kind, location, name ?? terminal.Comp.SessionUser, detail));
        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(_entries.Count - 1);

        switch (kind)
        {
            case CMUSecurityEvent.HackStarted:
                _hackSeen.Add(terminal);
                break;
            case CMUSecurityEvent.WentDark:
                var xform = Transform(terminal);
                if (xform.GridUid is { } grid)
                    _lastSeen[terminal] = (grid, _transform.WithEntityId(xform.Coordinates, grid).Position, location);
                break;
        }
    }

    /// <summary>Where a dark terminal was when it went dark, if security saw it go.</summary>
    public bool TryGetLastSeen(EntityUid terminal, out EntityUid grid, out Vector2 position)
    {
        var found = _lastSeen.TryGetValue(terminal, out var seen);
        (grid, position) = (seen.Grid, seen.Position);
        return found;
    }

    private void OnRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUSecurityRequestMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.SecurityNet) || !_net.GetNetworks(ent).Contains(SecurityNetwork))
            return;

        var entries = new List<CMUSecurityEntry>();
        var terminals = new List<CMUSecurityTerminal>();
        NetEntity? grid = null;
        switch (args.Panel)
        {
            case CMUSecurityPanel.Log:
                entries.AddRange(_entries);
                break;
            case CMUSecurityPanel.Map:
                if (Transform(ent).GridUid is { } gridUid && TryComp(gridUid, out MapGridComponent? mapGrid))
                {
                    _navMap.EnsureNavMap((gridUid, mapGrid));
                    grid = GetNetEntity(gridUid);
                    terminals.AddRange(Terminals(gridUid));
                }

                break;
        }

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key,
            new CMUSecurityStateMessage(args.Panel, entries, terminals, grid), args.Actor);
    }

    /// <summary>Every terminal on this grid; dark ones where they were last seen, not where they are.</summary>
    public IEnumerable<CMUSecurityTerminal> Terminals(EntityUid grid)
    {
        var query = EntityQueryEnumerator<CMUPersonalComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var computer, out var xform))
        {
            if (computer.TerminalId == 0)
                continue;

            var status = Status(uid, computer);
            var hacked = _hackSeen.Contains(uid) && HasComp<CMUDeepWebAccessComponent>(uid);
            if (status == CMUSecurityStatus.Dark && _lastSeen.TryGetValue(uid, out var seen))
            {
                if (seen.Grid == grid)
                    yield return new CMUSecurityTerminal(computer.TerminalId, GetNetEntity(uid), seen.Position, status, hacked, seen.Location);

                continue;
            }

            if (xform.GridUid != grid)
                continue;

            var position = _transform.WithEntityId(xform.Coordinates, grid).Position;
            yield return new CMUSecurityTerminal(computer.TerminalId, GetNetEntity(uid), position, status, hacked, Location(uid));
        }
    }

    private CMUSecurityStatus Status(EntityUid uid, CMUPersonalComputerComponent computer)
    {
        if (_net.IsDark(computer))
            return CMUSecurityStatus.Dark;

        if (!_computerPower.IsPowered(uid))
            return CMUSecurityStatus.Unpowered;

        if (!computer.On)
            return CMUSecurityStatus.Off;

        return computer.SessionUser != null || computer.Guest ? CMUSecurityStatus.LoggedIn : CMUSecurityStatus.Online;
    }
}
