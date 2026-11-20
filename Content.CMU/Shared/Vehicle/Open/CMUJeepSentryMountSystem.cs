using System.Linq;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Runs a sentry gun set on a vehicle's sentry pintle. The sentry keeps its own gun, ammo and IFF; the
/// pintle picks its targets by the rules the vehicle's key holder sets, swings onto the one it picks
/// and fires the sentry at it once it faces it.
/// </summary>
/// <remarks>
/// The rules live on the pintle. The sentry's own targeting holds every friendly faction; on the
/// server the pintle's holds those less the factions it still shoots when armed. Anyone the sentry's
/// set allows is a target, and so is anyone holding a gun whom only the smaller set allows, short of
/// the factions no sentry may shoot.
/// </remarks>
public sealed class CMUJeepSentryMountSystem : EntitySystem
{
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private CMUOpenVehicleSystem _jeep = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedSentryTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    /// <summary>
    /// The factions the settings window lists, as one row each: the sentry laptop's factions, with the
    /// two names it has for Weyland-Yutani, whose ID cards it tells apart from no one else's, as one.
    /// </summary>
    public static readonly (LocId Name, string[] Factions)[] FactionRows =
    [
        ("cmu-jeep-sentry-faction-govfor", ["GOVFOR"]),
        ("cmu-jeep-sentry-faction-opfor", ["OPFOR"]),
        ("cmu-jeep-sentry-faction-colony", ["Colony"]),
        ("cmu-jeep-sentry-faction-bureau", ["Bureau"]),
        ("cmu-jeep-sentry-faction-upp", ["UPP"]),
        ("cmu-jeep-sentry-faction-weyu", ["AUWeYu", "WeYa"]),
        ("cmu-jeep-sentry-faction-prodigy", ["Prodigy"]),
    ];

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJeepSentryMountComponent, EntInsertedIntoContainerMessage>(OnSentryInserted);
        SubscribeLocalEvent<CMUJeepSentryMountComponent, EntRemovedFromContainerMessage>(OnSentryRemoved);
        SubscribeLocalEvent<CMUJeepSentryMountComponent, EntGotInsertedIntoContainerMessage>(OnFitted);
        SubscribeLocalEvent<CMUOpenVehicleComponent, GetVerbsEvent<Verb>>(OnGetVerbs);

        Subs.BuiEvents<CMUJeepSentryMountComponent>(CMUJeepSentryUiKey.Key, subs =>
        {
            subs.Event<CMUJeepSentryFriendlyMessage>(OnFriendlyMsg);
            subs.Event<CMUJeepSentryArmedMessage>(OnArmedMsg);
            subs.Event<CMUJeepSentryModeMessage>(OnModeMsg);
            subs.Event<CMUJeepSentryPowerMessage>(OnPowerMsg);
        });
    }

    /// <summary>
    /// The sentry set on the pintle, if one is.
    /// </summary>
    public EntityUid? GetSentry(Entity<CMUJeepSentryMountComponent> mount)
    {
        return _itemSlots.GetItemOrNull(mount.Owner, mount.Comp.SlotId);
    }

    /// <summary>
    /// Whether the vehicle has a sentry on a pintle.
    /// </summary>
    public bool HasSentry(EntityUid vehicle)
    {
        return TryGetMount(vehicle, out var mount) && GetSentry(mount) != null;
    }

    private void OnSentryInserted(Entity<CMUJeepSentryMountComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || _timing.ApplyingState)
            return;

        // A sentry set up by an engineer brings its friendly factions; the pintle keeps them until the
        // key holder changes them.
        if (TryComp(args.Entity, out SentryTargetingComponent? targeting) && targeting.FriendlyFactions.Count > 0)
        {
            ent.Comp.FriendlyFactions = new HashSet<string>(targeting.FriendlyFactions);
            ent.Comp.ArmedHostileFactions.IntersectWith(ent.Comp.FriendlyFactions);
        }

        // Its shots leave from the pintle on the jeep, not from inside the slot, where nothing is hit,
        // and fly clear of the jeep that carries it, as the jeep's own gun's do.
        EnsureComp<GunIgnoreContainerOwnerCollisionComponent>(args.Entity);
        var origin = EnsureComp<CMUVehicleGunOriginComponent>(args.Entity);
        origin.Pivot = ent.Comp.SentryPivot;
        origin.Barrel = ent.Comp.SentryBarrel;

        ent.Comp.Target = null;
        Dirty(ent);
        ApplyRules(ent);

        if (_turret.TryGetVehicle(ent, out var vehicle))
            ClearGunnerSeat(vehicle);
    }

    private void OnSentryRemoved(Entity<CMUJeepSentryMountComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || _timing.ApplyingState)
            return;

        RemComp<CMUVehicleGunOriginComponent>(args.Entity);
        RemComp<GunIgnoreContainerOwnerCollisionComponent>(args.Entity);
        ent.Comp.Target = null;
        Dirty(ent);
    }

    // A pintle fitted with its sentry already on takes the gunner's place just the same.
    private void OnFitted(Entity<CMUJeepSentryMountComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState || GetSentry(ent) == null || !_turret.TryGetVehicle(ent, out var vehicle))
            return;

        ClearGunnerSeat(vehicle);
    }

    /// <summary>
    /// The sentry takes the gunner's place: whoever sat there gets out.
    /// </summary>
    private void ClearGunnerSeat(EntityUid vehicle)
    {
        if (_net.IsClient || !TryComp(vehicle, out CMUVehicleSeatsComponent? seats))
            return;

        foreach (var seat in seats.SeatEntities)
        {
            if (!TryComp(seat, out CMUVehicleSeatComponent? seatComp) ||
                !seatComp.Gunner ||
                !TryComp(seat, out StrapComponent? strap))
            {
                continue;
            }

            foreach (var rider in strap.BuckledEntities.ToArray())
            {
                _buckle.Unbuckle(rider, null);
            }
        }
    }

    /// <summary>
    /// Sets who the pintle's sentry holds fire on, and which of those it fires on anyway once armed.
    /// </summary>
    public void SetRules(Entity<CMUJeepSentryMountComponent> ent, IEnumerable<string> friendly, IEnumerable<string> armedHostile)
    {
        ent.Comp.FriendlyFactions = friendly.Where(SharedSentryTargetingSystem.SentryAllowedFactions.Contains).ToHashSet();
        ent.Comp.ArmedHostileFactions = armedHostile.Where(ent.Comp.FriendlyFactions.Contains).ToHashSet();
        Dirty(ent);
        ApplyRules(ent);
    }

    /// <summary>
    /// Copies the pintle's rules onto the sentry's targeting and its own. Targeting runs on the server
    /// alone, so the pintle's own targeting is only kept there.
    /// </summary>
    private void ApplyRules(Entity<CMUJeepSentryMountComponent> ent)
    {
        if (_net.IsClient)
            return;

        if (GetSentry(ent) is { } sentry && TryComp(sentry, out SentryTargetingComponent? sentryTargeting))
            _targeting.SetFriendlyFactions((sentry, sentryTargeting), ent.Comp.FriendlyFactions);

        var unarmedOnly = new HashSet<string>(ent.Comp.FriendlyFactions);
        unarmedOnly.ExceptWith(ent.Comp.ArmedHostileFactions);
        _targeting.SetFriendlyFactions((ent, EnsureComp<SentryTargetingComponent>(ent)), unarmedOnly);
    }

    private void OnGetVerbs(Entity<CMUOpenVehicleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !TryGetMount(ent, out var mount) ||
            !HoldsKey(args.User, ent))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-jeep-sentry-verb"),
            Act = () => _ui.OpenUi(mount.Owner, CMUJeepSentryUiKey.Key, user),
        });
    }

    /// <summary>
    /// Loads a sentry magazine into the sentry on the vehicle's pintle, as if it were used on the
    /// sentry. False if it isn't a magazine that sentry takes, or the vehicle has no sentry on.
    /// </summary>
    public bool TryLoadSentry(EntityUid vehicle, EntityUid user, EntityUid used, EntityCoordinates clickLocation)
    {
        if (!HasComp<BallisticAmmoProviderComponent>(used) ||
            HasComp<GunComponent>(used) ||
            !TryGetMount(vehicle, out var mount) ||
            GetSentry(mount) is not { } sentry ||
            !TryComp(sentry, out SentryComponent? sentryComp) ||
            sentryComp.MagazineTag is { } magazine && !_tags.HasTag(used, magazine))
        {
            return false;
        }

        var forwarded = new InteractUsingEvent(user, used, sentry, clickLocation);
        RaiseLocalEvent(sentry, forwarded);
        return forwarded.Handled;
    }

    /// <summary>
    /// The vehicle's sentry pintle, if it has one fitted.
    /// </summary>
    public bool TryGetMount(EntityUid vehicle, out Entity<CMUJeepSentryMountComponent> mount)
    {
        mount = default;
        if (!TryComp(vehicle, out ContainerManagerComponent? containers))
            return false;

        foreach (var container in _container.GetAllContainers(vehicle, containers))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp(contained, out CMUJeepSentryMountComponent? comp))
                {
                    mount = (contained, comp);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the user holds the key cut for this vehicle.
    /// </summary>
    public bool HoldsKey(EntityUid user, EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleIgnitionComponent? ignition) || ignition.KeyCode == 0)
            return false;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp(held, out CMUVehicleKeyComponent? key) && key.Code == ignition.KeyCode)
                return true;
        }

        return false;
    }

    private bool CanConfigure(Entity<CMUJeepSentryMountComponent> ent, EntityUid user)
    {
        if (_turret.TryGetVehicle(ent, out var vehicle) && HoldsKey(user, vehicle))
            return true;

        _popup.PopupEntity(Loc.GetString("cmu-jeep-sentry-need-key"), user, user, PopupType.SmallCaution);
        _ui.CloseUi(ent.Owner, CMUJeepSentryUiKey.Key, user);
        return false;
    }

    private void OnFriendlyMsg(Entity<CMUJeepSentryMountComponent> ent, ref CMUJeepSentryFriendlyMessage args)
    {
        if (!SharedSentryTargetingSystem.SentryAllowedFactions.Contains(args.Faction) || !CanConfigure(ent, args.Actor))
            return;

        if (args.Friendly)
        {
            ent.Comp.FriendlyFactions.Add(args.Faction);
        }
        else
        {
            ent.Comp.FriendlyFactions.Remove(args.Faction);
            ent.Comp.ArmedHostileFactions.Remove(args.Faction);
        }

        Dirty(ent);
        ApplyRules(ent);
    }

    private void OnArmedMsg(Entity<CMUJeepSentryMountComponent> ent, ref CMUJeepSentryArmedMessage args)
    {
        if (!ent.Comp.FriendlyFactions.Contains(args.Faction) || !CanConfigure(ent, args.Actor))
            return;

        if (args.ArmedHostile)
            ent.Comp.ArmedHostileFactions.Add(args.Faction);
        else
            ent.Comp.ArmedHostileFactions.Remove(args.Faction);

        Dirty(ent);
        ApplyRules(ent);
    }

    private void OnModeMsg(Entity<CMUJeepSentryMountComponent> ent, ref CMUJeepSentryModeMessage args)
    {
        if (!Enum.IsDefined(args.Mode) || !CanConfigure(ent, args.Actor))
            return;

        ent.Comp.Mode = args.Mode;
        ent.Comp.NextRetarget = TimeSpan.Zero;
        Dirty(ent);
    }

    private void OnPowerMsg(Entity<CMUJeepSentryMountComponent> ent, ref CMUJeepSentryPowerMessage args)
    {
        if (!CanConfigure(ent, args.Actor))
            return;

        ent.Comp.Powered = args.Powered;
        SetTarget(ent, null);
        Dirty(ent);
    }

    /// <summary>
    /// Whether the sentry on this pintle would shoot the target, by the pintle's rules.
    /// </summary>
    public bool IsTarget(Entity<CMUJeepSentryMountComponent> mount, EntityUid sentry, EntityUid target)
    {
        if (!TryComp(target, out MobStateComponent? mobState) || _mobState.IsDead(target, mobState))
            return false;

        if (!TryComp(sentry, out SentryTargetingComponent? sentryTargeting))
            return false;

        if (_targeting.IsValidTarget((sentry, sentryTargeting), target))
            return true;

        if (mount.Comp.ArmedHostileFactions.Count == 0 || !IsArmed(target) || NeverShot(sentryTargeting, target))
            return false;

        // Every friendly faction is shot once armed: anyone the sentry can tell apart is a target.
        if (mount.Comp.ArmedHostileFactions.IsSupersetOf(mount.Comp.FriendlyFactions))
            return HasComp<UserIFFComponent>(target) || HasComp<NpcFactionMemberComponent>(target);

        return TryComp(mount, out SentryTargetingComponent? unarmedOnly) &&
               _targeting.IsValidTarget((mount, unarmedOnly), target);
    }

    /// <summary>
    /// Whether no sentry may shoot them, armed or not: a faction kept off every sentry's list, or one the
    /// alliance console has made friends with.
    /// </summary>
    private bool NeverShot(SentryTargetingComponent sentry, EntityUid target)
    {
        if (!TryComp(target, out NpcFactionMemberComponent? member))
            return false;

        var alliance = sentry.AllianceFriendlyNpcFactions;
        foreach (var faction in member.Factions)
        {
            if (alliance.Contains(faction) ||
                _prototypes.TryIndex(faction, out NpcFactionPrototype? proto) && proto.SentryProtected)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether they hold a gun in their hands.
    /// </summary>
    public bool IsArmed(EntityUid target)
    {
        foreach (var held in _hands.EnumerateHeld(target))
        {
            if (HasComp<GunComponent>(held))
                return true;
        }

        return false;
    }

    /// <summary>
    /// How dangerous a target is, for the biggest-threat mode: the Queen and King over tier 3 over tier
    /// 2 over tier 1, then an armed person over an unarmed one, then larvae.
    /// </summary>
    public float Threat(EntityUid target)
    {
        if (TryComp(target, out XenoComponent? xeno))
        {
            if (xeno.Tier > 0)
                return xeno.Tier + 1;

            // Tier 0 is the Queen and King, and larvae: told apart by size.
            return _size.TryGetSize(target, out var size) && size >= RMCSizes.Big ? 5f : 0.25f;
        }

        return IsArmed(target) ? 1f : 0.5f;
    }

    /// <summary>
    /// How far the pintle looks for targets with this sentry on.
    /// </summary>
    public float GetRange(Entity<CMUJeepSentryMountComponent> mount, EntityUid sentry)
    {
        return MetaData(sentry).EntityPrototype is { } proto && mount.Comp.SentryRanges.TryGetValue(proto.ID, out var range)
            ? range
            : mount.Comp.Range;
    }

    /// <summary>
    /// Whether the sentry can run: switched on, with the vehicle's key in, its pintle and hull whole,
    /// and the vehicle neither written off nor in for maintenance.
    /// </summary>
    public bool CanRun(Entity<CMUJeepSentryMountComponent> mount, EntityUid vehicle)
    {
        return mount.Comp.Powered &&
               (!mount.Comp.NeedsKeyIn ||
                TryComp(vehicle, out CMUVehicleIgnitionComponent? ignition) && ignition.KeySlot.HasItem) &&
               _hardpoints.IsHardpointFunctional(mount) &&
               !_jeep.IsHullWrecked(vehicle) &&
               !HasComp<VehicleTotaledComponent>(vehicle) &&
               !(TryComp(vehicle, out VehicleMaintenanceComponent? maintenance) && maintenance.ControlsLocked);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUJeepSentryMountComponent, VehicleTurretComponent>();
        while (query.MoveNext(out var uid, out var mount, out var turret))
        {
            var ent = (uid, mount);
            if (GetSentry(ent) is not { } sentry ||
                !TryComp(sentry, out GunComponent? gun) ||
                !_turret.TryGetVehicle(uid, out var vehicle) ||
                !CanRun(ent, vehicle) ||
                !_turret.TryGetTurretOrigin(uid, out var originCoords))
            {
                SetTarget(ent, null);
                continue;
            }

            var origin = _transform.ToMapCoordinates(originCoords);
            var range = GetRange(ent, sentry);
            if (mount.Target is { } current && !CanEngage(ent, sentry, vehicle, current, origin, range))
                SetTarget(ent, null);

            if (now >= mount.NextRetarget)
            {
                mount.NextRetarget = now + mount.RetargetInterval;
                if (PickTarget(ent, sentry, vehicle, origin, range) is { } best && IsClearlyBetter(ent, best, origin))
                    SetTarget(ent, best);
            }

            // It follows its target whether or not it can fire on it.
            if (mount.Target is not { } target || !_turret.TryAimAtTarget(uid, target, out var targetCoords))
                continue;

            // It fires once it has had time to swing round and faces its target.
            var toTarget = _transform.ToMapCoordinates(targetCoords).Position - origin.Position;
            if (now < mount.FireFrom ||
                Math.Abs(Angle.ShortestDistance(turret.WorldRotation, toTarget.ToWorldAngle()).Degrees) > mount.AimTolerance)
            {
                continue;
            }

            // Marked as aimed at the target, so it can hit them lying down.
            var previous = _guns.SwapTarget((sentry, gun), target);
            _guns.AttemptShoot((sentry, gun), sentry, targetCoords);
            _guns.SwapTarget((sentry, gun), previous);
        }
    }

    private void SetTarget(Entity<CMUJeepSentryMountComponent> ent, EntityUid? target)
    {
        if (ent.Comp.Target == target)
            return;

        ent.Comp.Target = target;
        ent.Comp.FireFrom = _timing.CurTime + ent.Comp.AimTime;
        Dirty(ent);
    }

    /// <summary>
    /// Whether to turn from the current target to this one: a bigger threat on that mode, or one much
    /// closer, so it doesn't flick between two targets at much the same range.
    /// </summary>
    private bool IsClearlyBetter(Entity<CMUJeepSentryMountComponent> mount, EntityUid candidate, MapCoordinates origin)
    {
        if (mount.Comp.Target is not { } current)
            return true;

        if (current == candidate)
            return false;

        if (mount.Comp.Mode == CMUJeepSentryTargetMode.Biggest)
        {
            var threat = Threat(candidate).CompareTo(Threat(current));
            if (threat != 0)
                return threat > 0;
        }

        var margin = mount.Comp.SwitchMargin * mount.Comp.SwitchMargin;
        return DistanceSquared(candidate, origin) < DistanceSquared(current, origin) * margin;
    }

    private float DistanceSquared(EntityUid target, MapCoordinates origin)
    {
        return (_transform.GetMapCoordinates(target).Position - origin.Position).LengthSquared();
    }

    private bool CanEngage(
        Entity<CMUJeepSentryMountComponent> mount,
        EntityUid sentry,
        EntityUid vehicle,
        EntityUid target,
        MapCoordinates origin,
        float range)
    {
        // Its own riders are never its targets; its rounds pass them by anyway.
        if (TerminatingOrDeleted(target) || _jeep.IsAboard(target, vehicle) || !IsTarget(mount, sentry, target))
            return false;

        var targetMap = _transform.GetMapCoordinates(target);
        return targetMap.MapId == origin.MapId &&
               _interaction.InRangeUnobstructed(origin, targetMap, range);
    }

    private EntityUid? PickTarget(
        Entity<CMUJeepSentryMountComponent> mount,
        EntityUid sentry,
        EntityUid vehicle,
        MapCoordinates origin,
        float range)
    {
        EntityUid? best = null;
        var bestThreat = float.MinValue;
        var bestDistance = float.MaxValue;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(origin, range, _nearby);
        foreach (var candidate in _nearby)
        {
            if (!CanEngage(mount, sentry, vehicle, candidate, origin, range))
                continue;

            var distance = DistanceSquared(candidate, origin);
            var threat = mount.Comp.Mode == CMUJeepSentryTargetMode.Biggest ? Threat(candidate) : 0f;
            if (threat < bestThreat || threat == bestThreat && distance >= bestDistance)
                continue;

            best = candidate;
            bestThreat = threat;
            bestDistance = distance;
        }

        return best;
    }
}
