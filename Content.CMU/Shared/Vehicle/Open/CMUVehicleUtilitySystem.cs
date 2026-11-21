using System.Numerics;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// What the utility kits bolted onto a vehicle do: a roof rack to stow gear on, an auxiliary fuel
/// tank, smoke dischargers, a winch, a tow hitch and a camo net. None of them work on a wrecked hull.
/// </summary>
/// <remarks>
/// A towed load is moved by the server after its vehicle each tick: clients see it follow a tick
/// behind. It comes loose when the way to the hitch is blocked.
/// </remarks>
public sealed class CMUVehicleUtilitySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private CMUOpenVehicleSystem _jeep = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private static readonly Vector2[] Corners =
    [
        new(-0.7071f, -0.7071f), new(0.7071f, -0.7071f), new(-0.7071f, 0.7071f), new(0.7071f, 0.7071f),
    ];

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleSmokeReloadDoAfterEvent>(OnSmokeReload);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleWinchDoAfterEvent>(OnWinch);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleHitchDoAfterEvent>(OnHitch);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleCamoNetDoAfterEvent>(OnCamoNet);
        SubscribeLocalEvent<CMUVehicleRoofRackComponent, StorageInteractAttemptEvent>(OnRackInteractAttempt);
        SubscribeLocalEvent<CMUVehicleTowingComponent, ComponentShutdown>(OnTowingShutdown);
        SubscribeLocalEvent<CMUVehicleTowedComponent, ComponentShutdown>(OnTowedShutdown);
    }

    private void OnTowingShutdown(Entity<CMUVehicleTowingComponent> ent, ref ComponentShutdown args)
    {
        // A tower deleted outright lets its load go.
        if (ent.Comp.Towed is { } load && !TerminatingOrDeleted(load))
            RemComp<CMUVehicleTowedComponent>(load);
    }

    private void OnTowedShutdown(Entity<CMUVehicleTowedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent.Comp.Tower, out CMUVehicleTowingComponent? towing) && towing.Towed == ent.Owner)
        {
            towing.Towed = null;
            Dirty(ent.Comp.Tower.Value, towing);
        }
    }

    /// <summary>
    /// The kit with <typeparamref name="T"/> bolted onto the vehicle, if there is one and the vehicle's
    /// hull isn't wrecked.
    /// </summary>
    public Entity<T>? Kit<T>(EntityUid vehicle) where T : IComponent
    {
        if (!TryComp(vehicle, out CMUVehicleUpgradesComponent? upgrades)
            || _jeep.IsHullWrecked(vehicle)
            || !_container.TryGetContainer(vehicle, upgrades.Container, out var kits))
        {
            return null;
        }

        foreach (var kit in kits.ContainedEntities)
        {
            if (TryComp(kit, out T? comp))
                return (kit, comp);
        }

        return null;
    }

    /// <summary>
    /// The share of top speed the vehicle keeps while it tows something.
    /// </summary>
    public float TowSpeed(EntityUid vehicle)
    {
        if (CompOrNull<CMUVehicleTowingComponent>(vehicle)?.Towed == null)
            return 1f;

        return Kit<CMUVehicleTowHitchComponent>(vehicle)?.Comp.Speed ?? 1f;
    }

    /// <summary>
    /// A kit went on or came off the vehicle. An auxiliary tank grows or shrinks its fuel tank, the fuel
    /// that no longer fits being lost; a hitch taken off lets its load go, and a camo net comes down.
    /// </summary>
    public void KitChanged(EntityUid vehicle, EntityUid kit, bool fitted)
    {
        if (!fitted && HasComp<CMUVehicleTowHitchComponent>(kit))
            Unhitch(vehicle);

        if (!fitted && HasComp<CMUVehicleCamoNetComponent>(kit))
            TakeDownNet(vehicle);

        if (_net.IsClient
            || !TryComp(kit, out CMUVehicleAuxTankComponent? aux)
            || !TryComp(vehicle, out CMUVehicleFuelComponent? fuel)
            || !_solutions.TryGetSolution(vehicle, fuel.Solution, out var tank, out var solution))
        {
            return;
        }

        var capacity = solution.MaxVolume + (fitted ? aux.Capacity : -aux.Capacity);
        if (!fitted && solution.Volume > capacity)
            _solutions.SplitSolution(tank.Value, solution.Volume - capacity);

        _solutions.SetCapacity(tank.Value, capacity);
    }

    private void OnRackInteractAttempt(Entity<CMUVehicleRoofRackComponent> ent, ref StorageInteractAttemptEvent args)
    {
        // A rack is gear lashed to a jeep, not a crate to carry about: it only opens bolted on.
        if (!_container.TryGetContainingContainer((ent, null, null), out var container)
            || !TryComp(container.Owner, out CMUVehicleUpgradesComponent? upgrades)
            || container.ID != upgrades.Container)
        {
            args.Cancelled = true;
        }
    }

    private void OnGetVerbs(Entity<CMUVehicleUpgradesComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var vehicle = ent.Owner;

        if (Kit<CMUVehicleRoofRackComponent>(vehicle) is { } rack)
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-vehicle-roof-rack-verb"),
                Act = () => _storage.OpenStorageUI(rack, user),
            });
        }

        if (Kit<CMUVehicleSmokeDischargerComponent>(vehicle) is { } smoke && _jeep.IsAboard(user, vehicle))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-vehicle-smoke-verb", ("charges", smoke.Comp.Charges)),
                Disabled = smoke.Comp.Charges <= 0 || smoke.Comp.NextFire > _timing.CurTime,
                Act = () => TryFireSmoke(vehicle, user),
            });
        }

        if (Kit<CMUVehicleWinchComponent>(vehicle) is { } winch)
            AddWinchVerbs(vehicle, winch, user, args.Verbs);

        if (Kit<CMUVehicleTowHitchComponent>(vehicle) is { } hitch)
            AddHitchVerbs(vehicle, hitch, user, args.Verbs);

        if (Kit<CMUVehicleCamoNetComponent>(vehicle) is { } net)
        {
            var spread = HasComp<CMUVehicleCamouflagedComponent>(vehicle);
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString(spread ? "cmu-vehicle-camo-down-verb" : "cmu-vehicle-camo-up-verb"),
                Act = () => StartDoAfter(user, vehicle, net.Comp.Delay, new CMUVehicleCamoNetDoAfterEvent(), net),
            });
        }
    }

    private void StartDoAfter(EntityUid user, EntityUid vehicle, TimeSpan delay, DoAfterEvent ev, EntityUid? used)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, vehicle, vehicle, used)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    /// <summary>
    /// Fires the vehicle's smoke dischargers, if it has them loaded and ready.
    /// </summary>
    public bool TryFireSmoke(EntityUid vehicle, EntityUid user)
    {
        if (Kit<CMUVehicleSmokeDischargerComponent>(vehicle) is not { } smoke
            || smoke.Comp.Charges <= 0
            || smoke.Comp.NextFire > _timing.CurTime)
        {
            return false;
        }

        smoke.Comp.Charges--;
        smoke.Comp.NextFire = _timing.CurTime + smoke.Comp.Cooldown;
        Dirty(smoke);
        _popup.PopupPredicted(Loc.GetString("cmu-vehicle-smoke-fired"), vehicle, user, PopupType.Medium);
        if (_net.IsClient)
            return true;

        // A burst off each corner, screening the vehicle from every side.
        foreach (var corner in Corners)
        {
            var at = new EntityCoordinates(vehicle, corner * smoke.Comp.Distance);
            Spawn(smoke.Comp.Smoke, _transform.ToMapCoordinates(at));
        }

        return true;
    }

    /// <summary>
    /// Starts reloading the vehicle's smoke dischargers with <paramref name="used"/>, if it's what they
    /// take and they have room.
    /// </summary>
    public bool TryReloadSmoke(EntityUid vehicle, EntityUid user, EntityUid used)
    {
        if (Kit<CMUVehicleSmokeDischargerComponent>(vehicle) is not { } smoke
            || !_whitelist.IsWhitelistPass(smoke.Comp.Reload, used))
        {
            return false;
        }

        if (smoke.Comp.Charges >= smoke.Comp.MaxCharges)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-smoke-full"), vehicle, user);
            return true;
        }

        StartDoAfter(user, vehicle, smoke.Comp.ReloadDelay, new CMUVehicleSmokeReloadDoAfterEvent(), used);
        return true;
    }

    private void OnSmokeReload(Entity<CMUVehicleUpgradesComponent> ent, ref CMUVehicleSmokeReloadDoAfterEvent args)
    {
        if (args.Cancelled
            || args.Handled
            || args.Used is not { } used
            || Kit<CMUVehicleSmokeDischargerComponent>(ent) is not { } smoke
            || smoke.Comp.Charges >= smoke.Comp.MaxCharges)
        {
            return;
        }

        args.Handled = true;
        smoke.Comp.Charges++;
        Dirty(smoke);
        PredictedQueueDel(used);
        var message = Loc.GetString("cmu-vehicle-smoke-reloaded", ("charges", smoke.Comp.Charges));
        _popup.PopupClient(message, ent, args.User);
    }

    /// <summary>
    /// Whether the winch or hitch can take hold of the load: a crate, a jeep nobody's in or a jeep's
    /// wreck, in the open from the vehicle and within reach, not anchored, and in nobody's tow. A tank
    /// or APC is far too heavy.
    /// </summary>
    private bool CanTake(EntityUid vehicle, EntityUid load, float range, EntityWhitelist? whitelist)
    {
        if (load == vehicle
            || _container.IsEntityInContainer(load)
            || !_whitelist.IsWhitelistPass(whitelist, load)
            || Transform(load).Anchored
            || Crewed(load)
            || CompOrNull<CMUVehicleTowedComponent>(load)?.Tower != null
            || CompOrNull<CMUVehicleTowingComponent>(load)?.Towed != null
            || CompOrNull<CMUVehicleTowedComponent>(vehicle)?.Tower == load)
        {
            return false;
        }

        return _interaction.InRangeUnobstructed(vehicle, load, range,
            predicate: uid => uid == vehicle || uid == load);
    }

    /// <summary>
    /// What's within <paramref name="range"/> of <paramref name="at"/> that the vehicle can take hold of
    /// within <paramref name="reach"/> of itself.
    /// </summary>
    private List<EntityUid> Loads(
        EntityUid vehicle,
        MapCoordinates at,
        float range,
        float reach,
        EntityWhitelist? whitelist)
    {
        var loads = new List<EntityUid>();
        foreach (var near in _lookup.GetEntitiesInRange(at, range, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (CanTake(vehicle, near, reach, whitelist))
                loads.Add(near);
        }

        return loads;
    }

    private void AddWinchVerbs(
        EntityUid vehicle,
        Entity<CMUVehicleWinchComponent> winch,
        EntityUid user,
        SortedSet<InteractionVerb> verbs)
    {
        var from = _transform.GetMapCoordinates(vehicle);
        foreach (var load in Loads(vehicle, from, winch.Comp.Range, winch.Comp.Range, winch.Comp.Whitelist))
        {
            verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-vehicle-winch-verb", ("load", load)),
                Act = () => StartDoAfter(user, vehicle, winch.Comp.Delay, new CMUVehicleWinchDoAfterEvent(), load),
            });
        }
    }

    private void OnWinch(Entity<CMUVehicleUpgradesComponent> ent, ref CMUVehicleWinchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } load)
            return;

        args.Handled = true;
        if (!_net.IsClient)
            TryWinch(ent, load, args.User);
    }

    /// <summary>
    /// Hauls the load up behind the vehicle on its winch, if it's still within reach and the way is clear.
    /// </summary>
    public bool TryWinch(EntityUid vehicle, EntityUid load, EntityUid? user = null)
    {
        if (Kit<CMUVehicleWinchComponent>(vehicle) is not { } winch)
            return false;

        if (!CanTake(vehicle, load, winch.Comp.Range, winch.Comp.Whitelist))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-winch-lost", ("load", load)), vehicle, user.Value);

            return false;
        }

        var drop = new EntityCoordinates(vehicle, winch.Comp.Offset);
        if (!_interaction.InRangeUnobstructed(vehicle, _transform.ToMapCoordinates(drop),
                range: winch.Comp.Offset.Length() + 0.5f,
                predicate: uid => uid == load))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-winch-blocked"), vehicle, user.Value);

            return false;
        }

        Unanchor(load);
        _transform.SetCoordinates(load, drop);
        _transform.AttachToGridOrMap(load);
        if (user != null)
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-winch-done", ("load", load)), vehicle, user.Value);

        return true;
    }

    private void AddHitchVerbs(
        EntityUid vehicle,
        Entity<CMUVehicleTowHitchComponent> hitch,
        EntityUid user,
        SortedSet<InteractionVerb> verbs)
    {
        if (CompOrNull<CMUVehicleTowingComponent>(vehicle)?.Towed is { } towed)
        {
            verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-vehicle-unhitch-verb", ("load", towed)),
                Act = () => Unhitch(vehicle),
            });
            return;
        }

        var behind = _transform.ToMapCoordinates(new EntityCoordinates(vehicle, hitch.Comp.Offset));
        var reach = hitch.Comp.Offset.Length() + hitch.Comp.Reach;
        foreach (var load in Loads(vehicle, behind, hitch.Comp.Reach, reach, hitch.Comp.Whitelist))
        {
            verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-vehicle-hitch-verb", ("load", load)),
                Act = () => StartDoAfter(user, vehicle, hitch.Comp.Delay, new CMUVehicleHitchDoAfterEvent(), load),
            });
        }
    }

    private void OnHitch(Entity<CMUVehicleUpgradesComponent> ent, ref CMUVehicleHitchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } load)
            return;

        args.Handled = true;
        if (!_net.IsClient && TryHitch(ent, load))
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-hitched", ("load", load)), ent, args.User);
    }

    /// <summary>
    /// Hitches the load behind the vehicle, if it has a hitch free and can take the load.
    /// </summary>
    public bool TryHitch(EntityUid vehicle, EntityUid load)
    {
        // No trains: a jeep in tow can't tow another.
        if (Kit<CMUVehicleTowHitchComponent>(vehicle) is not { } hitch
            || CompOrNull<CMUVehicleTowingComponent>(vehicle)?.Towed != null
            || CompOrNull<CMUVehicleTowedComponent>(vehicle)?.Tower != null
            || !CanTake(vehicle, load, hitch.Comp.Offset.Length() + hitch.Comp.Reach, hitch.Comp.Whitelist))
        {
            return false;
        }

        Unanchor(load);
        var towing = EnsureComp<CMUVehicleTowingComponent>(vehicle);
        towing.Towed = load;
        Dirty(vehicle, towing);
        var towed = EnsureComp<CMUVehicleTowedComponent>(load);
        towed.Tower = vehicle;
        Dirty(load, towed);
        return true;
    }

    /// <summary>
    /// Lets go of whatever the vehicle tows.
    /// </summary>
    public void Unhitch(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleTowingComponent? towing) || towing.Towed is not { } load)
            return;

        towing.Towed = null;
        Dirty(vehicle, towing);
        RemComp<CMUVehicleTowedComponent>(load);
    }

    private void Unanchor(EntityUid load)
    {
        var xform = Transform(load);
        if (xform.Anchored)
            _transform.Unanchor(load, xform);
    }

    private void OnCamoNet(Entity<CMUVehicleUpgradesComponent> ent, ref CMUVehicleCamoNetDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ToggleCamoNet(ent, args.User);
    }

    /// <summary>
    /// Spreads the vehicle's camo net over it, parked, or takes it down.
    /// </summary>
    public bool ToggleCamoNet(EntityUid vehicle, EntityUid? user = null)
    {
        if (Kit<CMUVehicleCamoNetComponent>(vehicle) is not { } net)
            return false;

        if (HasComp<CMUVehicleCamouflagedComponent>(vehicle))
        {
            TakeDownNet(vehicle);
            return true;
        }

        if (Moving(vehicle))
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("cmu-vehicle-camo-moving"), vehicle, user.Value);

            return false;
        }

        var camouflaged = EnsureComp<CMUVehicleCamouflagedComponent>(vehicle);
        camouflaged.LastShot = LastShot(vehicle);
        camouflaged.Hidden.Clear();
        if (CompOrNull<CMUVehicleOverlayComponent>(vehicle)?.Overlay is { } overlay)
            camouflaged.Hidden.Add(overlay);

        // The guns' own overlays, drawn over the vehicle.
        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent>();
        while (turrets.MoveNext(out var uid, out var visual))
        {
            if (TryGetEntity(visual.Turret, out var turret)
                && _turret.TryGetVehicle(turret.Value, out var owner)
                && owner == vehicle)
            {
                camouflaged.Hidden.Add(uid);
            }
        }

        Hide(vehicle, net.Comp.Opacity);
        foreach (var hidden in camouflaged.Hidden)
        {
            Hide(hidden, net.Comp.Opacity);
        }

        Dirty(vehicle, camouflaged);
        return true;
    }

    private void Hide(EntityUid uid, float opacity)
    {
        var invisible = EnsureComp<EntityActiveInvisibleComponent>(uid);
        invisible.Opacity = opacity;
        Dirty(uid, invisible);
    }

    private void TakeDownNet(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleCamouflagedComponent? camouflaged))
            return;

        foreach (var hidden in camouflaged.Hidden)
        {
            if (!TerminatingOrDeleted(hidden))
                RemComp<EntityActiveInvisibleComponent>(hidden);
        }

        RemComp<EntityActiveInvisibleComponent>(vehicle);
        RemComp<CMUVehicleCamouflagedComponent>(vehicle);
    }

    /// <summary>
    /// The latest time any gun aboard can next fire: mounted on the vehicle, or in its riders' hands.
    /// A gun that fires pushes it on.
    /// </summary>
    private TimeSpan LastShot(EntityUid vehicle)
    {
        var last = TimeSpan.Zero;
        if (_itemSlots.GetItemOrNull(vehicle, CMUOpenVehicleSystem.TurretSlot) is { } mount
            && TryComp(mount, out ItemSlotsComponent? mounted))
        {
            foreach (var slot in mounted.Slots.Values)
            {
                if (TryComp(slot.Item, out GunComponent? gun) && gun.NextFire > last)
                    last = gun.NextFire;
            }
        }

        if (!TryComp(vehicle, out CMUVehicleSeatsComponent? seats))
            return last;

        foreach (var seat in seats.SeatEntities)
        {
            if (!TryComp(seat, out StrapComponent? strap))
                continue;

            foreach (var rider in strap.BuckledEntities)
            {
                foreach (var held in _hands.EnumerateHeld(rider))
                {
                    if (TryComp(held, out GunComponent? gun) && gun.NextFire > last)
                        last = gun.NextFire;
                }
            }
        }

        return last;
    }

    /// <summary>
    /// Whether anyone is driving the vehicle or sitting in it.
    /// </summary>
    private bool Crewed(EntityUid vehicle)
    {
        if (CompOrNull<VehicleComponent>(vehicle)?.Operator != null)
            return true;

        if (!TryComp(vehicle, out CMUVehicleSeatsComponent? seats))
            return false;

        foreach (var seat in seats.SeatEntities)
        {
            if (TryComp(seat, out StrapComponent? strap) && strap.BuckledEntities.Count > 0)
                return true;
        }

        return false;
    }

    private bool Moving(EntityUid vehicle)
    {
        return CompOrNull<CMUVehicleTowedComponent>(vehicle)?.Tower != null
            || (TryComp(vehicle, out GridVehicleMoverComponent? mover) && MathF.Abs(mover.CurrentSpeed) > 0.05f);
    }

    public override void Update(float frameTime)
    {
        // The net comes off the moment the vehicle moves, or anything aboard fires.
        var camouflaged = EntityQueryEnumerator<CMUVehicleCamouflagedComponent>();
        while (camouflaged.MoveNext(out var uid, out var net))
        {
            if (Moving(uid) || LastShot(uid) > net.LastShot || Kit<CMUVehicleCamoNetComponent>(uid) == null)
                TakeDownNet(uid);
        }

        if (_net.IsClient)
            return;

        // The towed load follows the hitch, and comes loose when something holds it back.
        var towing = EntityQueryEnumerator<CMUVehicleTowingComponent>();
        while (towing.MoveNext(out var uid, out var comp))
        {
            if (comp.Towed is not { } towed)
                continue;

            // Someone taking the wheel or a seat in a towed jeep slips the tow.
            if (TerminatingOrDeleted(towed)
                || Kit<CMUVehicleTowHitchComponent>(uid) is not { } hitch
                || Crewed(towed)
                || Transform(towed).Anchored
                || _container.IsEntityInContainer(towed))
            {
                Unhitch(uid);
                continue;
            }

            var hitchPoint = new EntityCoordinates(uid, hitch.Comp.Offset);
            if (!_interaction.InRangeUnobstructed(uid, _transform.ToMapCoordinates(hitchPoint),
                    range: hitch.Comp.Offset.Length() + 0.5f,
                    predicate: other => other == towed))
            {
                Unhitch(uid);
                continue;
            }

            _transform.SetCoordinates(towed, hitchPoint);
            _transform.AttachToGridOrMap(towed);
            _transform.SetWorldRotation(towed, _transform.GetWorldRotation(uid));
        }
    }
}
