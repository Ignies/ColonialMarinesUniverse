using Content.Shared._RMC14.Sensor;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Gates what a terminal answers. Switched off, only its front panel. Switched on, the colony network rides on the
/// sensor tower: while every tower on the terminal's colony, counting all its z levels, is down, the terminal has no
/// carrier. It keeps its own files, notes and wallpaper, but anything that goes over the network is dropped. A
/// colony without any tower keeps its network. A dark terminal has a carrier but no colony network: it keeps its
/// files and its own log of pages, boards answer only on the networks it still reaches, and a hacked one keeps the
/// deep web.
/// </summary>
public sealed class CMUComputerCarrierSystem : EntitySystem
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(2);

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;

    private readonly Dictionary<EntityUid, bool> _towers = new();
    private readonly HashSet<EntityUid> _maps = new();
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPersonalComputerComponent, BoundUserInterfaceMessageAttempt>(OnMessageAttempt);
    }

    /// <summary>What a terminal can still do with no carrier: things that stay on the machine.</summary>
    private static bool IsLocal(BoundUserInterfaceMessage message)
    {
        return message is CMUNoteSaveMessage
            or CMUNoteDeleteMessage
            or CMUNetSetWallpaperMessage
            or CMUNetSaveShortcutMessage
            or CMUNetRemoveShortcutMessage
            or CMUGameDeleteMessage
            or CMUComputerLogoutMessage
            or CMUComputerOpenConsoleMessage
            or CMUComputerScreenMessage;
    }

    /// <summary>
    /// What still goes out from a dark terminal: page visits, which only land in its own log; board traffic, which
    /// the boards check against the networks it reaches; and the deep web, once it is hacked.
    /// </summary>
    private bool ReachesWhileDark(EntityUid computer, BoundUserInterfaceMessage message)
    {
        if (message is CMUNetVisitMessage
            or CMUNetBoardRequestMessage
            or CMUNetBoardPostMessage
            or CMUNetBoardCommentMessage
            or CMUNetBoardDeleteMessage
            or CMUNetBoardRestoreMessage
            or CMUNetBoardPayoutMessage
            or CMUNetBoardWithdrawMessage)
            return true;

        return HasComp<CMUDeepWebAccessComponent>(computer)
            && message is CMUDeepScanMessage
                or CMUDeepPageRequestMessage
                or CMUDeepCameraMessage
                or CMUDeepHijackMessage
                or CMUDeepSniffMessage
                or CMUDeepLoopMessage
                or CMUMarketRequestMessage
                or CMUMarketListMessage
                or CMUMarketBuyMessage;
    }

    /// <summary>The front panel works whatever state the machine is in.</summary>
    private static bool IsPanel(BoundUserInterfaceMessage message)
    {
        return message is CMUComputerPowerButtonMessage or CMUComputerCardSlotMessage;
    }

    /// <summary>A switched off terminal only answers its front panel; one with no carrier, only its own files.</summary>
    private void OnMessageAttempt(Entity<CMUPersonalComputerComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        // The engine opens and closes the window through this too; a switched off machine still shows its front panel.
        if (!args.UiKey.Equals(CMUPersonalComputerUiKey.Key)
            || IsPanel(args.Message)
            || args.Message is OpenBoundInterfaceMessage or CloseBoundInterfaceMessage)
            return;

        if (!ent.Comp.On
            || !IsLocal(args.Message) && (!ent.Comp.Carrier || _net.IsDark(ent.Comp) && !ReachesWhileDark(ent, args.Message)))
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + CheckEvery;
        var needed = _cfg.GetCVar(CMUPersonalComputerCVars.NeedsTower);

        // per map: whether any tower on it is up
        _towers.Clear();
        var towers = EntityQueryEnumerator<SensorTowerComponent, TransformComponent>();
        while (towers.MoveNext(out var tower, out var xform))
        {
            if (xform.MapUid is not { } map)
                continue;

            _towers[map] = _towers.GetValueOrDefault(map) || tower.State == SensorTowerState.On;
        }

        var computers = EntityQueryEnumerator<CMUPersonalComputerComponent, TransformComponent>();
        while (computers.MoveNext(out var uid, out var computer, out var xform))
        {
            var carrier = !needed || !computer.NeedsTower || xform.MapUid is not { } map || HasCarrier(map);
            if (computer.Carrier == carrier)
                continue;

            computer.Carrier = carrier;
            Dirty(uid, computer);
        }
    }

    /// <summary>Up when a tower on the colony is on, or when the colony has no tower at all.</summary>
    private bool HasCarrier(EntityUid map)
    {
        _maps.Clear();
        _maps.Add(map);
        if (TryComp(map, out CMUZLevelMapComponent? level) && TryComp(level.NetworkUid, out CMUZLevelsNetworkComponent? network))
        {
            foreach (var other in network.ZLevels.Values)
            {
                if (other is { } levelMap)
                    _maps.Add(levelMap);
            }
        }

        var any = false;
        foreach (var colonyMap in _maps)
        {
            if (!_towers.TryGetValue(colonyMap, out var up))
                continue;

            if (up)
                return true;

            any = true;
        }

        return !any;
    }
}
