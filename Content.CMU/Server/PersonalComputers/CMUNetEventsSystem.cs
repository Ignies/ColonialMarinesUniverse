using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Makes the ExtraNet react to the round. The Herald wire runs short news items, the Company intranet spins
/// them, the deep forums pick up sightings of the red dog and the names of colonists who went missing. Each
/// writer has its own switch and its own pace, so boards never flood.
/// </summary>
public sealed partial class CMUNetEventsSystem : EntitySystem
{
    private static readonly ProtoId<CMUNetSitePrototype> Herald = "CMUNetSiteColonyHerald";
    private static readonly ProtoId<CMUNetSitePrototype> Company = "CMUNetSiteWeylandIntranet";
    private static readonly ProtoId<CMUNetSitePrototype> LastSeen = "CMUNetSiteDeepLastSeen";
    private static readonly ProtoId<CMUNetSitePrototype> RedDog = "CMUNetSiteDeepRedDog";
    private static readonly ProtoId<DatasetPrototype> Handles = "CMUDeepHackerHandles";
    private static readonly TimeSpan WireCooldown = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan SpinCooldown = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan SightingCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan XenoScan = TimeSpan.FromSeconds(30);

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private CMUNetBoardSystem _boards = default!;
    [Dependency] private CMUPersonalComputerSystem _computers = default!;
    [Dependency] private SharedMindSystem _minds = default!;

    private TimeSpan _nextWire;
    private TimeSpan _nextSpin;
    private TimeSpan _nextSighting;
    private TimeSpan _nextXenoScan;
    private bool _xenosReported;
    private readonly HashSet<EntityUid> _seenXenos = new();
    private readonly List<(TimeSpan At, ProtoId<CMUNetSitePrototype> Site, string Author, string Title, string Body)> _queued = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _nextWire = _nextSpin = _nextSighting = _nextXenoScan = TimeSpan.Zero;
        _xenosReported = false;
        _seenXenos.Clear();
        _queued.Clear();
    }

    private bool On(CVarDef<bool> feature)
    {
        return _cfg.GetCVar(CMUPersonalComputerCVars.Enabled) && _cfg.GetCVar(feature);
    }

    private string Colony()
    {
        return _computers.CurrentColony()?.Name ?? Loc.GetString("cmu-net-colony-unknown");
    }

    private string Where(EntityUid uid)
    {
        return _areas.TryGetArea(uid, out _, out var area) ? area.Name : Loc.GetString("cmu-net-events-somewhere");
    }

    /// <summary>Posts later, so news trickles in after the fact like real reporting.</summary>
    private void Queue(TimeSpan delay, ProtoId<CMUNetSitePrototype> site, string author, string title, string body)
    {
        _queued.Add((_timing.CurTime + delay, site, author, title, body));
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (On(CMUPersonalComputerCVars.Wire))
            Queue(TimeSpan.FromSeconds(30), Herald, Loc.GetString("cmu-net-events-wire-author"),
                Loc.GetString("cmu-net-events-wire-shift-title", ("colony", Colony())),
                Loc.GetString("cmu-net-events-wire-shift-body", ("colony", Colony())));

        if (On(CMUPersonalComputerCVars.CompanySpin))
            Queue(TimeSpan.FromSeconds(45), Company, Loc.GetString("cmu-net-events-spin-author"),
                Loc.GetString("cmu-net-events-spin-shift-title"),
                Loc.GetString("cmu-net-events-spin-shift-body", ("colony", Colony())));
    }

    /// <summary>A colonist dying: the wire reports a death, the Company calls it an incident, Last Seen asks after them.</summary>
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead
            || !HasComp<HumanoidProfileComponent>(args.Target)
            || !_minds.TryGetMind(args.Target, out _, out _))
            return;

        var now = _timing.CurTime;
        var name = Name(args.Target);
        var where = Where(args.Target);

        if (On(CMUPersonalComputerCVars.Wire) && now >= _nextWire)
        {
            _nextWire = now + WireCooldown;
            Queue(TimeSpan.FromSeconds(_random.Next(30, 90)), Herald, Loc.GetString("cmu-net-events-wire-author"),
                Loc.GetString("cmu-net-events-wire-death-title"),
                Loc.GetString("cmu-net-events-wire-death-body", ("where", where)));
        }

        if (On(CMUPersonalComputerCVars.CompanySpin) && now >= _nextSpin)
        {
            _nextSpin = now + SpinCooldown;
            Queue(TimeSpan.FromSeconds(_random.Next(60, 150)), Company, Loc.GetString("cmu-net-events-spin-author"),
                Loc.GetString("cmu-net-events-spin-death-title"),
                Loc.GetString("cmu-net-events-spin-death-body", ("where", where)));
        }

        if (On(CMUPersonalComputerCVars.LoreEvents) && On(CMUPersonalComputerCVars.DeepWeb))
            Queue(TimeSpan.FromMinutes(_random.Next(2, 6)), LastSeen, Handle(),
                Loc.GetString("cmu-net-events-lastseen-title", ("name", name)),
                Loc.GetString("cmu-net-events-lastseen-body", ("name", name), ("where", where)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        for (var i = _queued.Count - 1; i >= 0; i--)
        {
            var (at, site, author, title, body) = _queued[i];
            if (now < at)
                continue;

            _queued.RemoveAt(i);
            _boards.PostAs(site, author, Loc.GetString("cmu-net-events-role"), title, body);
        }

        if (now < _nextXenoScan)
            return;

        _nextXenoScan = now + XenoScan;
        ScanXenos(now);
    }

    /// <summary>New xenos on the map: the first ever makes the wire and the Company, runners make the red dog board.</summary>
    private void ScanXenos(TimeSpan now)
    {
        var query = EntityQueryEnumerator<XenoComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_seenXenos.Add(uid))
                continue;

            var where = Where(uid);
            if (!_xenosReported)
            {
                _xenosReported = true;
                if (On(CMUPersonalComputerCVars.Wire))
                    Queue(TimeSpan.FromSeconds(_random.Next(60, 180)), Herald, Loc.GetString("cmu-net-events-wire-author"),
                        Loc.GetString("cmu-net-events-wire-creature-title"),
                        Loc.GetString("cmu-net-events-wire-creature-body", ("where", where)));

                if (On(CMUPersonalComputerCVars.CompanySpin))
                    Queue(TimeSpan.FromSeconds(_random.Next(120, 240)), Company, Loc.GetString("cmu-net-events-spin-author"),
                        Loc.GetString("cmu-net-events-spin-creature-title"),
                        Loc.GetString("cmu-net-events-spin-creature-body"));
            }

            var id = MetaData(uid).EntityPrototype?.ID ?? string.Empty;
            if (id.Contains("Runner", StringComparison.OrdinalIgnoreCase)
                && now >= _nextSighting
                && On(CMUPersonalComputerCVars.LoreEvents)
                && On(CMUPersonalComputerCVars.DeepWeb))
            {
                _nextSighting = now + SightingCooldown;
                Queue(TimeSpan.FromSeconds(_random.Next(60, 240)), RedDog, Handle(),
                    Loc.GetString("cmu-net-events-reddog-title", ("where", where)),
                    Loc.GetString("cmu-net-events-reddog-body", ("where", where)));
            }
        }
    }

    private string Handle()
    {
        return _random.Pick(_prototype.Index(Handles).Values);
    }
}
