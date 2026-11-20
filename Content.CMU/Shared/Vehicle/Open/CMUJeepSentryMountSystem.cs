using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Runs a sentry gun set on a vehicle's sentry pintle. The sentry keeps its own gun, ammo and IFF; the
/// pintle picks its targets by the rules the vehicle's key holder sets, swings onto the one it picks
/// and fires the sentry at it.
/// </summary>
/// <remarks>
/// The rules live on the pintle. The sentry's own targeting holds every friendly faction; the
/// pintle's holds those less the factions it still shoots when armed. Anyone the sentry's set allows
/// is a target, and so is anyone holding a gun whom only the smaller set allows.
/// </remarks>
public sealed class CMUJeepSentryMountSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private SharedSentryTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJeepSentryMountComponent, EntInsertedIntoContainerMessage>(OnSentryInserted);
        SubscribeLocalEvent<CMUJeepSentryMountComponent, EntRemovedFromContainerMessage>(OnSentryRemoved);
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
    /// Copies the pintle's rules onto the sentry's targeting and its own.
    /// </summary>
    private void ApplyRules(Entity<CMUJeepSentryMountComponent> ent)
    {
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
    /// Loads a magazine into the sentry on the vehicle's pintle, as if it were used on the sentry. False
    /// if the vehicle has no sentry on a pintle.
    /// </summary>
    public bool TryLoadSentry(EntityUid vehicle, EntityUid user, EntityUid used, EntityCoordinates clickLocation)
    {
        if (!HasComp<BallisticAmmoProviderComponent>(used) ||
            !TryGetMount(vehicle, out var mount) ||
            GetSentry(mount) is not { } sentry)
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
        ent.Comp.Target = null;
        Dirty(ent);
    }

    /// <summary>
    /// Whether the sentry on this pintle would shoot the target, by the pintle's rules.
    /// </summary>
    public bool IsTarget(Entity<CMUJeepSentryMountComponent> mount, EntityUid sentry, EntityUid target)
    {
        if (!TryComp(target, out MobStateComponent? mobState) || _mobState.IsDead(target, mobState))
            return false;

        if (TryComp(sentry, out SentryTargetingComponent? sentryTargeting) &&
            _targeting.IsValidTarget((sentry, sentryTargeting), target))
        {
            return true;
        }

        if (mount.Comp.ArmedHostileFactions.Count == 0 || !IsArmed(target))
            return false;

        // Every friendly faction is shot once armed: anyone the sentry can tell apart is a target.
        if (mount.Comp.ArmedHostileFactions.IsSupersetOf(mount.Comp.FriendlyFactions))
            return HasComp<UserIFFComponent>(target) || HasComp<NpcFactionMemberComponent>(target);

        return TryComp(mount, out SentryTargetingComponent? unarmedOnly) &&
               _targeting.IsValidTarget((mount, unarmedOnly), target);
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

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUJeepSentryMountComponent>();
        while (query.MoveNext(out var uid, out var mount))
        {
            var ent = (uid, mount);
            if (!mount.Powered ||
                GetSentry(ent) is not { } sentry ||
                !TryComp(sentry, out GunComponent? gun) ||
                !_turret.TryGetTurretOrigin(uid, out var originCoords))
            {
                SetTarget(ent, null);
                continue;
            }

            var origin = _transform.ToMapCoordinates(originCoords);
            if (mount.Target is { } current && !CanEngage(ent, sentry, current, origin))
                SetTarget(ent, null);

            if (now >= mount.NextRetarget)
            {
                mount.NextRetarget = now + mount.RetargetInterval;
                var best = PickTarget(ent, sentry, origin);
                if (best != mount.Target)
                    SetTarget(ent, best);
            }

            // It follows its target whether or not it can fire on it.
            if (mount.Target is not { } target || !_turret.TryAimAtTarget(uid, target, out var targetCoords))
                continue;

            if (now < mount.FireFrom)
                continue;

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

    private bool CanEngage(
        Entity<CMUJeepSentryMountComponent> mount,
        EntityUid sentry,
        EntityUid target,
        MapCoordinates origin)
    {
        if (TerminatingOrDeleted(target) || !IsTarget(mount, sentry, target))
            return false;

        var targetMap = _transform.GetMapCoordinates(target);
        return targetMap.MapId == origin.MapId &&
               _interaction.InRangeUnobstructed(origin, targetMap, mount.Comp.Range);
    }

    private EntityUid? PickTarget(Entity<CMUJeepSentryMountComponent> mount, EntityUid sentry, MapCoordinates origin)
    {
        EntityUid? best = null;
        var bestThreat = float.MinValue;
        var bestDistance = float.MaxValue;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(origin, mount.Comp.Range, _nearby);
        foreach (var candidate in _nearby)
        {
            if (!CanEngage(mount, sentry, candidate, origin))
                continue;

            var distance = (_transform.GetMapCoordinates(candidate).Position - origin.Position).LengthSquared();
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
