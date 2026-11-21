using System.Numerics;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// What the utility kits bolted onto a vehicle do: a roof rack to stow gear on, an auxiliary fuel
/// tank, smoke dischargers, a winch, a tow hitch and a camo net.
/// </summary>
public sealed class CMUVehicleUtilitySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUOpenVehicleSystem _jeep = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleSmokeReloadDoAfterEvent>(OnSmokeReload);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleWinchDoAfterEvent>(OnWinch);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleHitchDoAfterEvent>(OnHitch);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleCamoNetDoAfterEvent>(OnCamoNet);
    }

    /// <summary>
    /// The kit with <typeparamref name="T"/> bolted onto the vehicle, if there is one.
    /// </summary>
    public Entity<T>? Kit<T>(EntityUid vehicle) where T : IComponent
    {
        if (!TryComp(vehicle, out CMUVehicleUpgradesComponent? upgrades)
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
    /// A kit went on or came off the vehicle. An auxiliary tank grows or shrinks its fuel tank, fuel
    /// past the smaller tank spilling; a hitch taken off lets its load go, and a camo net comes down.
    /// </summary>
    public void KitChanged(EntityUid vehicle, EntityUid kit, bool fitted)
    {
        if (!fitted && HasComp<CMUVehicleTowHitchComponent>(kit))
            Unhitch(vehicle);

        if (!fitted && HasComp<CMUVehicleCamoNetComponent>(kit))
            SetCamouflaged(vehicle, null);

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
                Disabled = smoke.Comp.Charges <= 0,
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
    /// Fires the vehicle's smoke dischargers, if it has them loaded.
    /// </summary>
    public bool TryFireSmoke(EntityUid vehicle, EntityUid user)
    {
        if (Kit<CMUVehicleSmokeDischargerComponent>(vehicle) is not { } smoke || smoke.Comp.Charges <= 0)
            return false;

        smoke.Comp.Charges--;
        Dirty(smoke);
        _popup.PopupPredicted(Loc.GetString("cmu-vehicle-smoke-fired"), vehicle, user, PopupType.Medium);
        if (_net.IsClient)
            return true;

        // A burst off each corner, screening the vehicle from every side.
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
        {
            var at = _transform.ToMapCoordinates(new Robust.Shared.Map.EntityCoordinates(vehicle,
                Vector2.Normalize(corner) * smoke.Comp.Distance));
            Spawn(smoke.Comp.Smoke, at);
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
        if (args.Cancelled || args.Handled || args.Used is not { } used
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
    /// What the winch or hitch can take hold of near <paramref name="at"/>: not the vehicle itself,
    /// nor anything anchored for good, contained or already towed.
    /// </summary>
    private void Loads(EntityUid vehicle, Robust.Shared.Map.MapCoordinates at, float range, EntityWhitelist? whitelist)
    {
        _nearby.Clear();
        foreach (var near in _lookup.GetEntitiesInRange(at, range, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (near == vehicle
                || _container.IsEntityInContainer(near)
                || !_whitelist.IsWhitelistPass(whitelist, near)
                || IsTowed(near))
            {
                continue;
            }

            _nearby.Add(near);
        }
    }

    private bool IsTowed(EntityUid uid)
    {
        var query = EntityQueryEnumerator<CMUVehicleTowingComponent>();
        while (query.MoveNext(out _, out var towing))
        {
            if (towing.Towed == uid)
                return true;
        }

        return false;
    }

    private void AddWinchVerbs(
        EntityUid vehicle,
        Entity<CMUVehicleWinchComponent> winch,
        EntityUid user,
        SortedSet<InteractionVerb> verbs)
    {
        Loads(vehicle, _transform.GetMapCoordinates(vehicle), winch.Comp.Range, winch.Comp.Whitelist);
        foreach (var load in _nearby)
        {
            var target = load;
            verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-vehicle-winch-verb", ("load", target)),
                Act = () => StartDoAfter(user, vehicle, winch.Comp.Delay, new CMUVehicleWinchDoAfterEvent(), target),
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
    /// Hauls the load up behind the vehicle on its winch, if the way is clear.
    /// </summary>
    public bool TryWinch(EntityUid vehicle, EntityUid load, EntityUid? user = null)
    {
        if (Kit<CMUVehicleWinchComponent>(vehicle) is not { } winch)
            return false;

        var drop = new Robust.Shared.Map.EntityCoordinates(vehicle, winch.Comp.Offset);
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

        var behind = _transform.ToMapCoordinates(new Robust.Shared.Map.EntityCoordinates(vehicle, hitch.Comp.Offset));
        Loads(vehicle, behind, hitch.Comp.Reach, hitch.Comp.Whitelist);
        foreach (var load in _nearby)
        {
            var target = load;
            verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-vehicle-hitch-verb", ("load", target)),
                Act = () => StartDoAfter(user, vehicle, hitch.Comp.Delay, new CMUVehicleHitchDoAfterEvent(), target),
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
    /// Hitches the load behind the vehicle, if it has a hitch free and the load isn't towed already.
    /// </summary>
    public bool TryHitch(EntityUid vehicle, EntityUid load)
    {
        if (Kit<CMUVehicleTowHitchComponent>(vehicle) is null
            || CompOrNull<CMUVehicleTowingComponent>(vehicle)?.Towed != null
            || IsTowed(load))
        {
            return false;
        }

        Unanchor(load);
        var towing = EnsureComp<CMUVehicleTowingComponent>(vehicle);
        towing.Towed = load;
        Dirty(vehicle, towing);
        return true;
    }

    public void Unhitch(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleTowingComponent? towing) || towing.Towed == null)
            return;

        towing.Towed = null;
        Dirty(vehicle, towing);
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
            SetCamouflaged(vehicle, null);
            return true;
        }

        if (Moving(vehicle))
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("cmu-vehicle-camo-moving"), vehicle, user.Value);

            return false;
        }

        SetCamouflaged(vehicle, net.Comp.Opacity);
        return true;
    }

    /// <summary>
    /// Spreads the camo net over the vehicle and its overlay, or takes it down with no opacity.
    /// </summary>
    private void SetCamouflaged(EntityUid vehicle, float? opacity)
    {
        var overlay = CompOrNull<CMUVehicleOverlayComponent>(vehicle)?.Overlay;
        if (opacity is not { } seen)
        {
            RemComp<CMUVehicleCamouflagedComponent>(vehicle);
            RemComp<EntityActiveInvisibleComponent>(vehicle);
            if (overlay != null)
                RemComp<EntityActiveInvisibleComponent>(overlay.Value);

            return;
        }

        EnsureComp<CMUVehicleCamouflagedComponent>(vehicle);
        foreach (var uid in new[] { vehicle, overlay })
        {
            if (uid == null)
                continue;

            var invisible = EnsureComp<EntityActiveInvisibleComponent>(uid.Value);
            invisible.Opacity = seen;
            Dirty(uid.Value, invisible);
        }
    }

    private bool Moving(EntityUid vehicle)
    {
        return TryComp(vehicle, out GridVehicleMoverComponent? mover) && MathF.Abs(mover.CurrentSpeed) > 0.05f;
    }

    public override void Update(float frameTime)
    {
        // The net comes off the moment the vehicle moves off.
        var camouflaged = EntityQueryEnumerator<CMUVehicleCamouflagedComponent>();
        while (camouflaged.MoveNext(out var uid, out _))
        {
            if (Moving(uid))
                SetCamouflaged(uid, null);
        }

        if (_net.IsClient)
            return;

        // The towed load follows the hitch, and comes loose when something holds it back.
        var towing = EntityQueryEnumerator<CMUVehicleTowingComponent, TransformComponent>();
        while (towing.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Towed is not { } towed)
                continue;

            if (TerminatingOrDeleted(towed)
                || Kit<CMUVehicleTowHitchComponent>(uid) is not { } hitch
                || Transform(towed).Anchored
                || _container.IsEntityInContainer(towed))
            {
                Unhitch(uid);
                continue;
            }

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            _transform.SetWorldPositionRotation(towed, position + rotation.RotateVec(hitch.Comp.Offset), rotation);
        }
    }
}
