using Content.Server.Administration.Logs;
using Content.Server.Camera;
using Content.Server.CMU14.Insurgency;
using Content.Shared._RMC14.Camera;
using Content.Shared.Camera;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// Hacking computers into the deep web with an access tuner. First lock the tuner onto the terminal's carrier
/// wave with three dials, then turn the circuit board's tiles until the carrier reaches the core, all before the
/// security trace finds you; a wrong lock speeds the trace up. Also handles deep scans, camera taps and hijacks
/// on hacked computers.
/// </summary>
public sealed partial class CMUDeepWebSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";

    /// <summary>Chance that a scan only turns up an ad page that leads nowhere in particular.</summary>
    private const float JunkChance = 0.25f;

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private CameraNetworkSystem _cameras = default!;
    [Dependency] private CMUDeepMazeSystem _maze = default!;
    [Dependency] private CMUDeepAliasSystem _aliases = default!;
    [Dependency] private CMUNetBoardSystem _boards = default!;
    [Dependency] private InsurgencyFactionApplySystem _factions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<CMUDeepWebHackableComponent>(CMUDeepHackUiKey.Key, subs =>
        {
            subs.Event<CMUDeepHackLockMessage>(OnLock);
            subs.Event<CMUDeepHackRotateMessage>(OnRotate);
        });
        Subs.BuiEvents<CMUDeepWebAccessComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUDeepScanMessage>(OnScan);
            subs.Event<CMUDeepCameraMessage>(OnCamera);
            subs.Event<CMUDeepHijackMessage>(OnHijack);
        });
    }

    /// <summary>Opens the access tuner for someone who picked "hack terminal" on a computer's multitool menu.</summary>
    public bool TryStartHack(Entity<CMUDeepWebHackableComponent> ent, EntityUid user)
    {
        if (HasComp<CMUDeepWebAccessComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-deep-hack-already"), ent, user);
            return false;
        }

        if (!_skills.HasSkill(user, EngineerSkill, ent.Comp.SkillLevel))
        {
            _popup.PopupEntity(Loc.GetString("cmu-deep-hack-no-skill"), ent, user, PopupType.SmallCaution);
            return false;
        }

        if (_timing.CurTime < ent.Comp.LockedUntil)
        {
            var left = (int) Math.Ceiling((ent.Comp.LockedUntil - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("cmu-deep-hack-locked", ("seconds", left)), ent, user, PopupType.SmallCaution);
            return false;
        }

        if (ent.Comp.Board == null || ent.Comp.Board.User != user)
            ent.Comp.Board = NewBoard(ent.Comp, user);

        _ui.OpenUi(ent.Owner, CMUDeepHackUiKey.Key, user);
        SendState(ent);
        return true;
    }

    /// <summary>
    /// A fresh attempt. Better engineers get a cleaner carrier, a smaller board, more anchors and more time;
    /// experts are handed the carrier and only route it.
    /// </summary>
    public CMUDeepHackBoard NewBoard(CMUDeepWebHackableComponent hackable, EntityUid user)
    {
        var skill = _skills.GetSkill(user, EngineerSkill);
        var (width, height, anchors, noise, trace) = skill switch
        {
            >= 4 => (5, 4, 3, 0.15f, 1.3),
            3 => (6, 5, 2, 0.35f, 1.1),
            _ => (7, 5, 1, 0.55f, 1.0),
        };

        var rng = new System.Random(_random.Next());
        var sourceRow = rng.Next(height);
        var coreRow = rng.Next(height);
        var cells = CMUDeepHackPuzzle.NewBoard(rng, width, height, sourceRow, coreRow, anchors, out var solution);
        var board = new CMUDeepHackBoard
        {
            User = user,
            Stage = skill >= 4 ? CMUDeepHackStage.Route : CMUDeepHackStage.Tune,
            Frequency = rng.Next(1, CMUDeepHackPuzzle.MaxFrequency + 1),
            Amplitude = rng.Next(1, CMUDeepHackPuzzle.MaxAmplitude + 1),
            Phase = rng.Next(CMUDeepHackPuzzle.PhaseSteps),
            Noise = noise,
            Width = width,
            Height = height,
            SourceRow = sourceRow,
            CoreRow = coreRow,
            Cells = cells,
            Solution = solution,
            TraceStarted = _timing.CurTime,
            TraceEnds = _timing.CurTime + hackable.Trace * trace,
        };
        board.Log.Add(Loc.GetString(board.Stage == CMUDeepHackStage.Tune ? "cmu-deep-hack-log-tune" : "cmu-deep-hack-log-route"));
        return board;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CMUDeepWebHackableComponent>();
        while (query.MoveNext(out var uid, out var hackable))
        {
            if (hackable.Board is { } board && _timing.CurTime >= board.TraceEnds)
                Fail((uid, hackable), board.User);
        }
    }

    private void OnLock(Entity<CMUDeepWebHackableComponent> ent, ref CMUDeepHackLockMessage args)
    {
        if (ent.Comp.Board is not { Stage: CMUDeepHackStage.Tune } board || board.User != args.Actor)
            return;

        if (args.Frequency == board.Frequency && args.Amplitude == board.Amplitude && args.Phase == board.Phase)
        {
            board.Stage = CMUDeepHackStage.Route;
            board.Log.Add(Loc.GetString("cmu-deep-hack-log-locked-on"));
            board.Log.Add(Loc.GetString("cmu-deep-hack-log-route"));
        }
        else
        {
            board.TraceEnds -= ent.Comp.MissPenalty;
            board.Log.Add(Loc.GetString("cmu-deep-hack-log-miss", ("seconds", (int) ent.Comp.MissPenalty.TotalSeconds)));
        }

        SendState(ent);
    }

    private void OnRotate(Entity<CMUDeepWebHackableComponent> ent, ref CMUDeepHackRotateMessage args)
    {
        if (ent.Comp.Board is not { Stage: CMUDeepHackStage.Route } board
            || board.User != args.Actor
            || args.Cell < 0
            || args.Cell >= board.Cells.Count)
            return;

        var cell = board.Cells[args.Cell];
        if (cell.Locked || cell.Tile == CMUDeepHackTile.Empty)
            return;

        board.Cells[args.Cell] = cell with { Rotation = (byte) ((cell.Rotation + 1) % 4) };
        if (CMUDeepHackPuzzle.Solved(board.Cells, board.Width, board.Height, board.SourceRow, board.CoreRow))
        {
            Solve(ent, args.Actor);
            return;
        }

        SendState(ent);
    }

    private void Fail(Entity<CMUDeepWebHackableComponent> ent, EntityUid user)
    {
        ent.Comp.LockedUntil = _timing.CurTime + ent.Comp.Lockout;
        ent.Comp.Board!.Log.Add(Loc.GetString("cmu-deep-hack-log-traced"));
        SendState(ent);
        ent.Comp.Board = null;
        _popup.PopupEntity(Loc.GetString("cmu-deep-hack-traced"), ent, user, PopupType.SmallCaution);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} failed to hack {ToPrettyString(ent)} into the deep web");
    }

    private void Solve(Entity<CMUDeepWebHackableComponent> ent, EntityUid user)
    {
        ent.Comp.Board = null;
        EnsureComp<CMUDeepWebAccessComponent>(ent);
        _ui.CloseUi(ent.Owner, CMUDeepHackUiKey.Key, user);
        _popup.PopupEntity(Loc.GetString("cmu-deep-hack-success"), ent, user, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):player} hacked {ToPrettyString(ent)} into the deep web");
    }

    private void SendState(Entity<CMUDeepWebHackableComponent> ent)
    {
        if (ent.Comp.Board is not { } board)
            return;

        _ui.SetUiState(ent.Owner, CMUDeepHackUiKey.Key, new CMUDeepHackState(
            board.Stage,
            board.Frequency,
            board.Amplitude,
            board.Phase,
            board.Noise,
            board.Width,
            board.Height,
            board.SourceRow,
            board.CoreRow,
            new List<CMUDeepHackCell>(board.Cells),
            board.TraceStarted,
            board.TraceEnds,
            ent.Comp.LockedUntil,
            new List<string>(board.Log)));
    }

    private void OnScan(Entity<CMUDeepWebAccessComponent> ent, ref CMUDeepScanMessage args)
    {
        if (_timing.CurTime < ent.Comp.NextScan)
            return;

        // Lore sites and the first ad page of each chain; hidden sites themselves are only found through the ads.
        var found = new List<string>();
        foreach (var site in _prototype.EnumeratePrototypes<CMUNetSitePrototype>())
        {
            if (site.Networks.Contains(ent.Comp.Network) && !site.Hidden && !ent.Comp.Discovered.Contains(_aliases.GetAlias(site)))
                found.Add(_aliases.GetAlias(site));
        }

        foreach (var entry in _maze.Entries)
        {
            if (!ent.Comp.Discovered.Contains(entry))
                found.Add(entry);
        }

        ent.Comp.NextScan = _timing.CurTime + ent.Comp.ScanCooldown;
        ent.Comp.Discovered.Add(found.Count > 0 && !_random.Prob(JunkChance) ? _random.Pick(found) : _maze.JunkHost());
        Dirty(ent);
    }

    /// <summary>A <c>cam://</c> link on the unlocked camera site: shows that camera network on this computer.</summary>
    private void OnCamera(Entity<CMUDeepWebAccessComponent> ent, ref CMUDeepCameraMessage args)
    {
        ProtoId<CameraNetworkPrototype> network = args.Network;
        if (!ent.Comp.Unlocked.Contains(ent.Comp.CameraSite)
            || !ent.Comp.CameraNetworks.Contains(network)
            || !HasComp<CameraNetworkReceiverComponent>(ent))
            return;

        _cameras.SetReceiverNetworks(ent, new[] { network });
        _ui.OpenUi(ent.Owner, RMCCameraUiKey.Key, args.Actor);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} tapped camera network {network} from deep web computer {ToPrettyString(ent)}");
    }

    /// <summary>An insurgent seizes the colony's news front pages in the name of the round's insurgent faction.</summary>
    private void OnHijack(Entity<CMUDeepWebAccessComponent> ent, ref CMUDeepHijackMessage args)
    {
        if (!HasComp<CLFMemberComponent>(args.Actor))
        {
            _popup.PopupEntity(Loc.GetString("cmu-deep-hijack-not-insurgent"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (_timing.CurTime < _boards.NextHijack)
        {
            var left = (int) Math.Ceiling((_boards.NextHijack - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("cmu-deep-hijack-cooling", ("seconds", left)), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        var faction = _factions.GetActiveFaction()?.Metadata.Title ?? InsurgencyBuiltinFactions.VanillaClf().Metadata.Title;
        if (_boards.TryHijack(args.Actor, faction, args.Title, args.Body))
            _popup.PopupEntity(Loc.GetString("cmu-deep-hijack-done"), ent, args.Actor, PopupType.Medium);
    }
}
