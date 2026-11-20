using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Movement;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Pushing an open vehicle by hand. Ctrl+click its front or back to brace against the bumper; walking
/// into it then pushes it slowly along its length, away from you. The vehicle mover reads the pushes
/// through <see cref="TryGetPushInput"/> and rolls the vehicle as it does when a xeno shoves it.
/// People can't roll it on bare hubs; xenos, strong enough to drag it anyway, push it through the
/// mover's own xeno push.
/// </summary>
public sealed class CMUVehiclePushSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleWheelSystem _wheels = default!;

    // How closely a pusher's walking must line up with the vehicle's length to count as pushing it.
    private const float PushAlignment = 0.7f;

    // How far a pusher may be knocked from their place at the bumper before they let go.
    private const float LetGoDistance = 0.5f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehiclePushableComponent, CMUTryPullObjectEvent>(OnTryPull);
        SubscribeLocalEvent<CMUVehiclePushingComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<CMUVehiclePushingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CMUVehiclePushingComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnTryPull(Entity<CMUVehiclePushableComponent> ent, ref CMUTryPullObjectEvent args)
    {
        args.Handled = true;
        if (_net.IsClient)
            return;

        TryBrace(args.User, ent);
    }

    /// <summary>
    /// Braces the user against the vehicle's front or back, whichever they stand at, if they can push it.
    /// </summary>
    public bool TryBrace(EntityUid user, Entity<CMUVehiclePushableComponent> vehicle)
    {
        if (TryComp(user, out CMUVehiclePushingComponent? already) && already.Vehicle == vehicle.Owner)
        {
            StopPushing((user, already));
            return false;
        }

        if (Refusal(user, vehicle) is { } refusal)
        {
            _popup.PopupEntity(Loc.GetString(refusal, ("vehicle", vehicle.Owner)), user, user, PopupType.SmallCaution);
            return false;
        }

        var local = ToVehicle(vehicle, _transform.GetWorldPosition(user));
        var extents = vehicle.Comp.HalfExtents;
        var past = MathF.Abs(local.Y) - extents.Y;
        if (MathF.Abs(local.X) > extents.X + 0.25f || past < -0.2f || past > vehicle.Comp.Reach)
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-push-ends-only", ("vehicle", vehicle.Owner)), user, user, PopupType.SmallCaution);
            return false;
        }

        if (TryComp(user, out CMUVehiclePushingComponent? other))
            StopPushing((user, other));

        // The vehicle faces south in its own space: its front is -y.
        var front = local.Y < 0f;
        var pushing = EnsureComp<CMUVehiclePushingComponent>(user);
        pushing.Vehicle = vehicle;
        pushing.Front = front;
        pushing.Pushing = false;
        pushing.Across = Math.Clamp(local.X, -extents.X + 0.1f, extents.X - 0.1f);
        Dirty(user, pushing);

        Follow((user, pushing), vehicle);
        _physics.RegenerateContacts(user);
        _speed.RefreshMovementSpeedModifiers(user);
        _popup.PopupEntity(Loc.GetString(front ? "cmu-vehicle-push-brace-front" : "cmu-vehicle-push-brace-back",
            ("vehicle", vehicle.Owner)), user, user);
        return true;
    }

    private string? Refusal(EntityUid user, EntityUid vehicle)
    {
        if (!HasComp<HumanoidProfileComponent>(user))
            return "cmu-vehicle-push-cant";
        if (TryComp(vehicle, out VehicleComponent? driven) && driven.Operator != null)
            return "cmu-vehicle-push-driven";
        if (TryComp(user, out PullerComponent? puller) && puller.Pulling != null)
            return "cmu-wall-lean-grabbing";
        if (_mobState.IsIncapacitated(user))
            return "cmu-wall-lean-incapacitated";
        if (_standing.IsDown(user))
            return "cmu-wall-lean-resting";
        if (TryComp(user, out BuckleComponent? buckle) && buckle.Buckled)
            return "cmu-wall-lean-buckled";

        return null;
    }

    public void StopPushing(Entity<CMUVehiclePushingComponent> ent)
    {
        if (ent.Comp.Vehicle == null)
            return;

        ent.Comp.Vehicle = null;
        ent.Comp.Pushing = false;
        Dirty(ent);
        _physics.RegenerateContacts(ent.Owner);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        RemCompDeferred<CMUVehiclePushingComponent>(ent);
    }

    /// <summary>
    /// Knocks everyone pushing the vehicle off their feet and away from it, as when a xeno shoves it back
    /// against them.
    /// </summary>
    public void KnockOffPushers(EntityUid vehicle)
    {
        if (_net.IsClient || !TryComp(vehicle, out CMUVehiclePushableComponent? pushable))
            return;

        var query = EntityQueryEnumerator<CMUVehiclePushingComponent>();
        while (query.MoveNext(out var uid, out var pushing))
        {
            if (pushing.Vehicle != vehicle)
                continue;

            StopPushing((uid, pushing));
            _stun.TryKnockdown(uid, pushable.KnockedOffTime, true);
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-push-knocked-off", ("vehicle", vehicle)), uid, uid, PopupType.MediumCaution);
        }
    }

    private void OnMoveInput(Entity<CMUVehiclePushingComponent> ent, ref MoveInputEvent args)
    {
        if (_net.IsClient || ent.Comp.Vehicle is not { } vehicle)
            return;

        if (!args.HasDirectionalMovement)
        {
            SetPushing(ent, false);
            return;
        }

        // Walking into the vehicle pushes it; walking any other way lets go.
        var toward = Forward(vehicle) * (ent.Comp.Front ? -1f : 1f);
        if (Vector2.Dot(InputDirection(args.Entity), toward) < PushAlignment)
        {
            StopPushing(ent);
            return;
        }

        if (!ent.Comp.Pushing && !_wheels.HasAnyFunctionalWheel(vehicle))
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-push-no-wheels", ("vehicle", vehicle)), ent, ent, PopupType.SmallCaution);

        SetPushing(ent, true);
    }

    private void SetPushing(Entity<CMUVehiclePushingComponent> ent, bool pushing)
    {
        if (ent.Comp.Pushing == pushing)
            return;

        ent.Comp.Pushing = pushing;
        Dirty(ent);
    }

    // Braced, they stand still against the bumper; the vehicle carries them along as it rolls.
    private void OnRefreshSpeed(Entity<CMUVehiclePushingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Vehicle != null)
            args.ModifySpeed(0f);
    }

    // Leaning on the bumper, they stand a little into the vehicle's body.
    private void OnPreventCollide(Entity<CMUVehiclePushingComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OtherEntity == ent.Comp.Vehicle)
            args.Cancelled = true;
    }

    /// <summary>
    /// Which way the people pushing the vehicle move it, in its grid's cardinal directions, and how fast
    /// they can get it going. Pushes from opposite ends cancel out. False if nobody is pushing it, or it
    /// has no whole wheels to roll on.
    /// </summary>
    public bool TryGetPushInput(EntityUid vehicle, EntityUid grid, out Vector2i direction, out float maxSpeed)
    {
        direction = Vector2i.Zero;
        maxSpeed = 0f;
        if (!TryComp(vehicle, out CMUVehiclePushableComponent? pushable) || !_wheels.HasAnyFunctionalWheel(vehicle))
            return false;

        var net = 0;
        var query = EntityQueryEnumerator<CMUVehiclePushingComponent>();
        while (query.MoveNext(out _, out var pushing))
        {
            if (pushing.Vehicle == vehicle && pushing.Pushing)
                net += pushing.Front ? -1 : 1;
        }

        if (net == 0)
            return false;

        var forward = (_transform.GetWorldRotation(vehicle) - _transform.GetWorldRotation(grid)).GetCardinalDir().ToIntVec();
        direction = net > 0 ? forward : -forward;
        maxSpeed = MathF.Min(pushable.MaxPushSpeed, pushable.PushSpeed + (Math.Abs(net) - 1) * pushable.ExtraPusherSpeed);
        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUVehiclePushingComponent>();
        while (query.MoveNext(out var uid, out var pushing))
        {
            if (pushing.Vehicle is not { } vehicle ||
                TerminatingOrDeleted(vehicle) ||
                !TryComp(vehicle, out CMUVehiclePushableComponent? pushable) ||
                Refusal(uid, vehicle) != null ||
                (_transform.GetWorldPosition(uid) - Spot((vehicle, pushable), pushing)).Length() > LetGoDistance)
            {
                StopPushing((uid, pushing));
                continue;
            }

            Follow((uid, pushing), (vehicle, pushable));
        }
    }

    /// <summary>
    /// Puts the pusher at their place against the bumper, facing the vehicle, as it stands now.
    /// </summary>
    private void Follow(Entity<CMUVehiclePushingComponent> ent, Entity<CMUVehiclePushableComponent> vehicle)
    {
        var toward = Forward(vehicle) * (ent.Comp.Front ? -1f : 1f);
        _transform.SetWorldPosition(ent.Owner, Spot(vehicle, ent.Comp));
        _transform.SetWorldRotation(ent.Owner, toward.ToWorldAngle());

        var offset = toward * vehicle.Comp.LeanIn;
        if (ent.Comp.Offset == offset)
            return;

        ent.Comp.Offset = offset;
        Dirty(ent);
    }

    private Vector2 Spot(Entity<CMUVehiclePushableComponent> vehicle, CMUVehiclePushingComponent pushing)
    {
        var along = vehicle.Comp.HalfExtents.Y + vehicle.Comp.StandOff;
        var local = new Vector2(pushing.Across, pushing.Front ? -along : along);
        var (position, rotation) = _transform.GetWorldPositionRotation(vehicle);
        return position + rotation.RotateVec(local);
    }

    private Vector2 ToVehicle(EntityUid vehicle, Vector2 world)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(vehicle);
        return (-rotation).RotateVec(world - position);
    }

    /// <summary>
    /// The vehicle's forward direction in world space. It faces south in its own space.
    /// </summary>
    private Vector2 Forward(EntityUid vehicle)
    {
        return _transform.GetWorldRotation(vehicle).RotateVec(new Vector2(0f, -1f));
    }

    /// <summary>
    /// The way the mover is walking, in world space, from their held keys and how their view is turned.
    /// </summary>
    private Vector2 InputDirection(Entity<InputMoverComponent> mover)
    {
        var keys = _mover.DirVecForButtons(mover.Comp.HeldMoveButtons);
        if (keys == Vector2.Zero)
            return Vector2.Zero;

        var rotation = mover.Comp.TargetRelativeRotation;
        if (mover.Comp.RelativeEntity is { } relative)
            rotation += _transform.GetWorldRotation(relative);

        return Vector2.Normalize(rotation.RotateVec(keys));
    }
}
