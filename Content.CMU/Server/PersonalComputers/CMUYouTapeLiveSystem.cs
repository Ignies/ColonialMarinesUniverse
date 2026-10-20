using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Camera;
using Content.Shared._RMC14.Camera;
using Content.Shared.Camera;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// YouTape's live page: every broadcasting camera on the TV network is a live channel. A terminal can list them, with
/// who carries each and where, and open the TV network's cameras to watch one, the way a television set does.
/// Watching needs the carrier like any other page, so a downed sensor tower takes live tape off the air.
/// </summary>
public sealed class CMUYouTapeLiveSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private CameraNetworkSystem _cameras = default!;
    [Dependency] private CMUSecurityNetSystem _security = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>The network broadcasting cameras send on, which television sets receive.</summary>
    public static readonly ProtoId<CameraNetworkPrototype> TelevisionNetwork = "RMCTelevision";

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUTapeLiveRequestMessage>(OnRequest);
            subs.Event<CMUTapeWatchLiveMessage>(OnWatch);
        });
    }

    /// <summary>The broadcasting cameras on air now, with who holds each and where it is.</summary>
    public List<CMUTapeLiveFeed> Feeds()
    {
        var feeds = new List<CMUTapeLiveFeed>();
        foreach (var camera in _cameras.GetNetworkMembers(TelevisionNetwork))
        {
            if (TerminatingOrDeleted(camera))
                continue;

            feeds.Add(new CMUTapeLiveFeed(GetNetEntity(camera), Holder(camera), _security.Location(camera)));
        }

        return feeds;
    }

    /// <summary>The nearest mob the camera is inside or held by, or nobody.</summary>
    private string Holder(EntityUid camera)
    {
        var parent = Transform(camera).ParentUid;
        while (parent.IsValid())
        {
            if (HasComp<MobStateComponent>(parent))
                return Name(parent);

            parent = Transform(parent).ParentUid;
        }

        return string.Empty;
    }

    private void OnRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUTapeLiveRequestMessage args)
    {
        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key, new CMUTapeLiveStateMessage(Feeds()), args.Actor);
    }

    private void OnWatch(Entity<CMUPersonalComputerComponent> ent, ref CMUTapeWatchLiveMessage args)
    {
        if (!ent.Comp.On
            || !TryGetEntity(args.Camera, out var camera)
            || !_cameras.GetNetworkMembers(TelevisionNetwork).Contains(camera.Value)
            || !HasComp<CameraNetworkReceiverComponent>(ent))
            return;

        _cameras.SetReceiverNetworks(ent, new[] { TelevisionNetwork });
        _ui.OpenUi(ent.Owner, RMCCameraUiKey.Key, args.Actor);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} watched the live broadcast from {ToPrettyString(camera.Value)} on terminal {ToPrettyString(ent)}");
    }
}
