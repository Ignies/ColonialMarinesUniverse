using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Personal computer test server settings. All off by default; the test config in
/// Content.CMU/Design/PersonalComputers/Test turns them on.
/// </summary>
[CVarDefs]
public sealed class CMUPersonalComputerDevCVars
{
    /// <summary>
    /// On a server without a lobby, places a colony desktop and a corporate desktop on open tiles beside
    /// each player who spawns, once per player per round.
    /// </summary>
    public static readonly CVarDef<bool> DevDesks = CVarDef.Create("cmu.pc.dev_desks", false, CVar.SERVERONLY);

    /// <summary>
    /// Job given to joining players when their station offers it, so a test account spawns as, say, the
    /// journalist whatever its saved preferences say. Empty to leave jobs alone.
    /// </summary>
    public static readonly CVarDef<string> DevJob = CVarDef.Create("cmu.pc.dev_job", "", CVar.SERVERONLY);

    /// <summary>
    /// Kits test players out for the deep web: an access tuner in hand and enough engineering skill to hack
    /// the desktops beside them.
    /// </summary>
    public static readonly CVarDef<bool> DevHacker = CVarDef.Create("cmu.pc.dev_hacker", false, CVar.SERVERONLY);

    /// <summary>Makes test players insurgents, so they can post on insurgent boards and seize the Herald.</summary>
    public static readonly CVarDef<bool> DevInsurgent = CVarDef.Create("cmu.pc.dev_insurgent", false, CVar.SERVERONLY);

    /// <summary>Posts deep web leaks every minute, starting half a minute into the round, instead of every few minutes.</summary>
    public static readonly CVarDef<bool> DevFastLeaks = CVarDef.Create("cmu.pc.dev_fast_leaks", false, CVar.SERVERONLY);
}

/// <summary>Puts test players next to working computers. See <see cref="CMUPersonalComputerDevCVars"/>.</summary>
public sealed partial class CMUPersonalComputerDevSystem : EntitySystem
{
    private static readonly string[] Desks = { "RMCPersonalDesktop", "CMUPersonalDesktopWeyland" };
    private static readonly EntProtoId AccessTuner = "CMMultitool";
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";
    private const int HackerSkill = 4;

    private static readonly Vector2i[] Around =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(-1, 1), new(1, -1), new(-1, -1),
        new(2, 0), new(-2, 0), new(0, 2), new(0, -2),
    };

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly HashSet<NetUserId> _served = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnJobsAssigned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _served.Clear());
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        if (args.Handled || args.JobId != null || _ticker.LobbyEnabled)
            return;

        var job = _cfg.GetCVar(CMUPersonalComputerDevCVars.DevJob);
        if (job.Length == 0
            || !_prototypes.HasIndex<JobPrototype>(job)
            || !TryComp(args.Station, out StationJobsComponent? stationJobs)
            || !_stationJobs.TryGetJobSlot(args.Station, job, out _, stationJobs))
            return;

        args.JobId = job;
    }

    /// <summary>
    /// At round start the ticker only spawns players whose own preferences got them a job; spawn the rest as
    /// late joiners, which forces the dev job above.
    /// </summary>
    private void OnJobsAssigned(RulePlayerJobsAssignedEvent args)
    {
        if (_ticker.LobbyEnabled || _cfg.GetCVar(CMUPersonalComputerDevCVars.DevJob).Length == 0)
            return;

        foreach (var session in args.Players)
        {
            if (session.AttachedEntity == null)
                _ticker.MakeJoinGame(session, EntityUid.Invalid);
        }
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerDevCVars.DevDesks)
            || _ticker.LobbyEnabled
            || !_served.Add(args.Player.UserId))
            return;

        var xform = Transform(args.Mob);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return;

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var placed = 0;
        foreach (var offset in Around)
        {
            if (placed == Desks.Length)
                break;

            var tile = _map.GetTileRef(gridUid, grid, origin + offset);
            if (tile.Tile.IsEmpty || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                continue;

            Spawn(Desks[placed], _map.GridTileToLocal(gridUid, grid, tile.GridIndices));
            placed++;
        }

        Log.Info($"Placed {placed} test desktops beside {args.Player.Name}");

        if (_cfg.GetCVar(CMUPersonalComputerDevCVars.DevHacker))
        {
            _skills.SetSkill(args.Mob, EngineerSkill, HackerSkill);
            var tuner = Spawn(AccessTuner, xform.Coordinates);
            _hands.PickupOrDrop(args.Mob, tuner);
        }

        if (_cfg.GetCVar(CMUPersonalComputerDevCVars.DevInsurgent))
            EnsureComp<CLFMemberComponent>(args.Mob);
    }
}
