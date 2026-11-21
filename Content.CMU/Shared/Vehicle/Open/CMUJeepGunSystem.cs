using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// The gun jeep's launchers: a grenade launcher won't lob a round at anything so close its burst would
/// reach the jeep, and a rocket pod's backblast knocks down and scorches anyone standing behind it.
/// </summary>
public sealed class CMUJeepGunSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUOpenVehicleSystem _jeep = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();
    private readonly List<EntityUid> _caught = new();

    public override void Initialize()
    {
        // Measured from the muzzle, which the gun origin system moves the shot to.
        SubscribeLocalEvent<CMUGunMinimumRangeComponent, AttemptShootEvent>(OnMinimumRange,
            after: [typeof(CMUVehicleGunOriginSystem)]);
        SubscribeLocalEvent<CMUGunBackblastComponent, GunShotEvent>(OnBackblast);
    }

    private void OnMinimumRange(Entity<CMUGunMinimumRangeComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || args.ToCoordinates is not { } to)
            return;

        var from = _transform.ToMapCoordinates(args.FromCoordinates);
        var target = _transform.ToMapCoordinates(to);
        if (from.MapId != target.MapId || (target.Position - from.Position).Length() >= ent.Comp.Range)
            return;

        args.Cancelled = true;
        args.ResetCooldown = true;
        args.Message = Loc.GetString("cmu-jeep-gun-too-close");
    }

    private void OnBackblast(Entity<CMUGunBackblastComponent> ent, ref GunShotEvent args)
    {
        // Knockdowns and damage are the server's; the client would only guess at who stood there.
        if (_net.IsClient)
            return;

        var from = _transform.ToMapCoordinates(args.FromCoordinates);
        var target = _transform.ToMapCoordinates(args.ToCoordinates);
        var aim = target.Position - from.Position;
        if (from.MapId != target.MapId || aim.LengthSquared() < 0.0001f)
            return;

        EntityUid? vehicle = _turret.TryGetVehicle(ent, out var found) ? found : null;
        InBackblast(ent, from, aim, args.User, vehicle, _caught);
        foreach (var mob in _caught)
        {
            _stun.TryKnockdown(mob, ent.Comp.KnockdownTime);
            _damageable.TryChangeDamage(mob, ent.Comp.Damage, origin: args.User);
            _popup.PopupEntity(Loc.GetString("cmu-jeep-gun-backblast"), mob, mob, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// Fills <paramref name="caught"/> with everyone the blast behind the muzzle reaches: in its cone,
    /// in the open from the muzzle, and neither the gunner nor riding the vehicle, whose bodywork
    /// shields them.
    /// </summary>
    public void InBackblast(
        Entity<CMUGunBackblastComponent> gun,
        MapCoordinates muzzle,
        Vector2 aim,
        EntityUid gunner,
        EntityUid? vehicle,
        List<EntityUid> caught)
    {
        caught.Clear();
        var back = (-aim).ToWorldAngle();
        var halfArc = gun.Comp.Arc / 2f;
        _nearby.Clear();
        _lookup.GetEntitiesInRange(muzzle, gun.Comp.Range, _nearby);
        foreach (var mob in _nearby)
        {
            if (mob.Owner == gunner || (vehicle is { } aboard && _jeep.IsAboard(mob.Owner, aboard)))
                continue;

            var at = _transform.GetMapCoordinates(mob);
            var offset = at.Position - muzzle.Position;
            if (offset.LengthSquared() >= 0.0001f
                && Math.Abs(Angle.ShortestDistance(offset.ToWorldAngle(), back).Degrees) > halfArc)
            {
                continue;
            }

            // Walls take the blast; the vehicle and the one caught don't block it.
            var self = mob.Owner;
            if (!_interaction.InRangeUnobstructed(muzzle, at, gun.Comp.Range + 0.5f,
                    predicate: e => e == self || e == vehicle))
            {
                continue;
            }

            caught.Add(mob.Owner);
        }
    }
}
