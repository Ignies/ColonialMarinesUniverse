using System.Linq;
using Content.Server.Pinpointer;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CriminalRecords;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>Raised by the fax system for every fax a machine receives, so the Marshals can keep copies.</summary>
[ByRefEvent]
public readonly record struct CMUFaxReceivedEvent(EntityUid Fax, FaxPrintout Printout);

/// <summary>
/// The Colonial Marshal Bureau network: lookup alerts from terminals that went looking for the deep web, a map
/// of every terminal on the colony grid, copies of every fax received this round, and the colony records.
/// Only computers on the CMB network get answers.
/// </summary>
public sealed partial class CMUCmbSystem : EntitySystem
{
    public static readonly ProtoId<CMUNetworkPrototype> CmbNetwork = "CMUNetCMB";
    private const int MaxAlerts = 60;
    private const int MaxFaxes = 60;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private CMUUniversalRecordsSystem _universalRecords = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly List<CMUCmbAlert> _alerts = new();
    private readonly HashSet<int> _alerted = new();
    private readonly List<CMUCmbFax> _faxes = new();
    private readonly Dictionary<EntityUid, TimeSpan> _traceCooldowns = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _alerts.Clear();
            _alerted.Clear();
            _faxes.Clear();
            _traceCooldowns.Clear();
        });
        SubscribeLocalEvent<CMUFaxReceivedEvent>(OnFaxReceived);
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUCmbRequestMessage>(OnRequest);
            subs.Event<CMUCmbTraceMessage>(OnTrace);
        });
    }

    public void Alert(Entity<CMUPersonalComputerComponent> terminal, string address, string location)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Cmb))
            return;

        _alerts.Insert(0, new CMUCmbAlert(_timing.CurTime, terminal.Comp.TerminalId, address, location));
        if (_alerts.Count > MaxAlerts)
            _alerts.RemoveAt(_alerts.Count - 1);

        _alerted.Add(terminal.Comp.TerminalId);
    }

    public IReadOnlyList<CMUCmbAlert> Alerts => _alerts;

    /// <summary>
    /// A Marshal picks a terminal on the map and starts a trace. Whoever is at that terminal sees the trace
    /// climb on their screen; if they are still logged in when it finishes, their name goes on the alerts.
    /// </summary>
    private void OnTrace(Entity<CMUPersonalComputerComponent> ent, ref CMUCmbTraceMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Cmb)
            || !_cfg.GetCVar(CMUPersonalComputerCVars.Trace)
            || !_net.GetNetworks(ent).Contains(CmbNetwork)
            || !TryGetEntity(args.Terminal, out var target)
            || !TryComp(target, out CMUPersonalComputerComponent? terminal))
            return;

        StartTrace((target.Value, terminal));
    }

    public bool StartTrace(Entity<CMUPersonalComputerComponent> terminal)
    {
        var now = _timing.CurTime;
        if (terminal.Comp.TraceEnds > now
            || _traceCooldowns.TryGetValue(terminal, out var ready) && now < ready)
            return false;

        terminal.Comp.TraceStarted = now;
        terminal.Comp.TraceEnds = now + CMUComputerLimits.TraceTime;
        _traceCooldowns[terminal] = terminal.Comp.TraceEnds + CMUComputerLimits.TraceCooldown;
        Dirty(terminal);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPersonalComputerComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            if (terminal.TraceEnds == TimeSpan.Zero || now < terminal.TraceEnds)
                continue;

            terminal.TraceEnds = TimeSpan.Zero;
            terminal.TraceStarted = TimeSpan.Zero;
            Dirty(uid, terminal);

            var result = terminal.SessionUser is { } user
                ? Loc.GetString("cmu-cmb-trace-found", ("name", user))
                : Loc.GetString("cmu-cmb-trace-lost");
            _alerts.Insert(0, new CMUCmbAlert(now, terminal.TerminalId, string.Empty, Name(uid), result));
            if (_alerts.Count > MaxAlerts)
                _alerts.RemoveAt(_alerts.Count - 1);
        }
    }

    public IReadOnlyList<CMUCmbFax> Faxes => _faxes;

    private void OnFaxReceived(ref CMUFaxReceivedEvent ev)
    {
        var to = TryComp(ev.Fax, out FaxMachineComponent? fax) ? fax.FaxName : Name(ev.Fax);
        var from = ev.Printout.SenderFaxName ?? Loc.GetString("cmu-cmb-fax-unknown-sender");
        var title = ev.Printout.Label ?? ev.Printout.Name;
        _faxes.Insert(0, new CMUCmbFax(_timing.CurTime, from, to, title, ev.Printout.Content));
        if (_faxes.Count > MaxFaxes)
            _faxes.RemoveAt(_faxes.Count - 1);
    }

    private void OnRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUCmbRequestMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Cmb) || !_net.GetNetworks(ent).Contains(CmbNetwork))
            return;

        var alerts = new List<CMUCmbAlert>();
        var terminals = new List<CMUCmbTerminal>();
        var faxes = new List<CMUCmbFax>();
        var records = new List<CMUCmbRecord>();
        NetEntity? grid = null;

        switch (args.Panel)
        {
            case CMUCmbPanel.Alerts:
                alerts.AddRange(_alerts);
                break;
            case CMUCmbPanel.Faxes:
                faxes.AddRange(_faxes);
                break;
            case CMUCmbPanel.Records:
                records.AddRange(Records());
                break;
            case CMUCmbPanel.Map:
                if (Transform(ent).GridUid is { } gridUid && TryComp(gridUid, out MapGridComponent? mapGrid))
                {
                    _navMap.EnsureNavMap((gridUid, mapGrid));
                    grid = GetNetEntity(gridUid);
                    terminals.AddRange(Terminals(gridUid));
                }

                alerts.AddRange(_alerts);
                break;
        }

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key,
            new CMUCmbStateMessage(args.Panel, alerts, terminals, faxes, records, grid), args.Actor);
    }

    /// <summary>Every terminal on this grid, where it stands on the grid.</summary>
    private IEnumerable<CMUCmbTerminal> Terminals(EntityUid grid)
    {
        var query = EntityQueryEnumerator<CMUPersonalComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var computer, out var xform))
        {
            if (xform.GridUid != grid || computer.TerminalId == 0)
                continue;

            var position = _transform.WithEntityId(xform.Coordinates, grid).Position;
            yield return new CMUCmbTerminal(computer.TerminalId, GetNetEntity(uid), position, _alerted.Contains(computer.TerminalId));
        }
    }

    private IEnumerable<CMUCmbRecord> Records()
    {
        if (_universalRecords.GetRecords() is not { } station)
            yield break;

        foreach (var (id, record) in _records.GetRecordsOfType<CriminalRecord>(station).OrderBy(r => r.Item1))
        {
            var key = new StationRecordKey(id, station);
            var name = _records.TryGetRecord<GeneralStationRecord>(key, out var general)
                ? general.Name
                : Loc.GetString("cmu-cmb-record-unknown");
            var history = record.History.Select(h => h.Crime).ToList();
            yield return new CMUCmbRecord(name, StatusName(record.Status), record.Reason ?? string.Empty, history);
        }
    }

    private static string StatusName(SecurityStatus status)
    {
        return status.ToString().ToUpperInvariant();
    }
}
