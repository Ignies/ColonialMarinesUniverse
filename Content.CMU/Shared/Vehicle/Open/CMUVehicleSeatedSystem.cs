using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Pulling.Events;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Keeps riders of an open vehicle in their seats. The buckle system unbuckles a rider who moves
/// off the strap by even a few millimetres, and the physics solver adds every awake body's velocity
/// and contact corrections to its local position, so any bump to a rider would throw them out.
/// While seated, the rider's hard contacts are cancelled and their velocity is cleared around every
/// step (see <see cref="CMUOpenVehiclePhysicsController"/>). Their body keeps its type, so tile fires,
/// smoke and bullets still reach them, and knockbacks, throws and drags still pull them out. Shots
/// fired from aboard their own vehicle, by its mounted gun or another rider, pass them by.
/// </summary>
public sealed class CMUVehicleSeatedSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSeatedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUVehicleSeatedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUVehicleSeatedComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CMUVehicleSeatedComponent, StartPullAttemptEvent>(OnStartPullAttempt);
    }

    private void OnStartup(Entity<CMUVehicleSeatedComponent> ent, ref ComponentStartup args)
    {
        // A rider can't pull from a seat: the pull would tug them off it. Server state already
        // carries the stop, so it isn't repeated while that state is being applied.
        if (!_timing.ApplyingState &&
            TryComp(ent, out PullerComponent? puller) &&
            puller.Pulling is { } pulled &&
            TryComp(pulled, out PullableComponent? pullable))
        {
            _pulling.TryStopPull(pulled, pullable, ent);
        }

        if (!TryComp(ent, out PhysicsComponent? body))
            return;

        Hold((ent, body));

        // Contacts made before buckling skip the filter below until they are rebuilt.
        _physics.RegenerateContacts((ent, body));
    }

    private void OnShutdown(Entity<CMUVehicleSeatedComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp(ent, out PhysicsComponent? body))
            _physics.RegenerateContacts((ent, body));
    }

    private void OnStartPullAttempt(Entity<CMUVehicleSeatedComponent> ent, ref StartPullAttemptEvent args)
    {
        if (args.Puller == ent.Owner)
            args.Cancel();
    }

    private void OnPreventCollide(Entity<CMUVehicleSeatedComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OurFixture.Hard && args.OtherFixture.Hard
            || IsShotFromOwnVehicle(ent, args.OtherEntity))
        {
            args.Cancelled = true;
        }
    }

    /// <summary>
    /// Whether the shot was fired from aboard the vehicle the rider sits in: by its mounted gun, or by
    /// a rider's own gun. The crew sit in the gunner's arc, and IFF only spares those sharing the
    /// shooter's faction, so without this the gunner would hit the driver firing over the cab.
    /// </summary>
    public bool IsShotFromOwnVehicle(EntityUid rider, EntityUid shot)
    {
        return TryComp(shot, out ProjectileComponent? projectile)
               && CompOrNull<BuckleComponent>(rider)?.BuckledTo is { } seat
               && CompOrNull<CMUVehicleSeatComponent>(seat)?.Vehicle is { } vehicle
               && (IsAboard(projectile.Weapon, vehicle) || IsAboard(projectile.Shooter, vehicle));
    }

    /// <summary>
    /// Whether the entity is the vehicle or rides somewhere on it: a gun in its turret, a rider in a
    /// seat, or a gun in a rider's hands.
    /// </summary>
    private bool IsAboard(EntityUid? entity, EntityUid vehicle)
    {
        for (var i = 0; i < 5 && entity is { } current && current.IsValid(); i++)
        {
            if (current == vehicle)
                return true;

            // The weapon or shooter may already be gone by the time its shot arrives.
            if (!TryComp(current, out TransformComponent? xform))
                break;

            entity = xform.ParentUid;
        }

        return false;
    }

    /// <summary>
    /// Clears a seated rider's velocity so the solver leaves them on the strap. A rider being thrown
    /// keeps it, so knockbacks still carry them off the seat.
    /// </summary>
    public void Hold(Entity<PhysicsComponent> rider)
    {
        if (HasComp<ThrownItemComponent>(rider) ||
            rider.Comp.LinearVelocity == Vector2.Zero && rider.Comp.AngularVelocity == 0f)
        {
            return;
        }

        _physics.SetLinearVelocity(rider, Vector2.Zero, wakeBody: false, body: rider.Comp);
        _physics.SetAngularVelocity(rider, 0f, body: rider.Comp);
    }
}
