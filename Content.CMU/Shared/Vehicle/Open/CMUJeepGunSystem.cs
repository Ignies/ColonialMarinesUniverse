using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// The gun jeep's launchers' safety rules: a grenade launcher won't lob a round at anything so close
/// its burst would reach the jeep, and a rocket launcher won't fire while its backblast would hit
/// someone behind it.
/// </summary>
public sealed class CMUJeepGunSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _behind = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGunMinimumRangeComponent, AttemptShootEvent>(OnMinimumRange);
        SubscribeLocalEvent<CMUGunBackblastComponent, AttemptShootEvent>(OnBackblast);
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

    private void OnBackblast(Entity<CMUGunBackblastComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || args.ToCoordinates is not { } to)
            return;

        var from = _transform.ToMapCoordinates(args.FromCoordinates);
        var target = _transform.ToMapCoordinates(to);
        var aim = target.Position - from.Position;
        if (from.MapId != target.MapId || aim.LengthSquared() < 0.0001f)
            return;

        if (FirstInBackblast(ent, from, aim, args.User) is not { } blocked)
            return;

        args.Cancelled = true;
        args.ResetCooldown = true;
        args.Message = Loc.GetString("cmu-jeep-gun-backblast", ("target", blocked));
    }

    /// <summary>
    /// Someone other than the gunner standing in the blast behind the muzzle, if anyone is.
    /// </summary>
    public EntityUid? FirstInBackblast(
        Entity<CMUGunBackblastComponent> gun,
        MapCoordinates muzzle,
        System.Numerics.Vector2 aim,
        EntityUid gunner)
    {
        var back = (-aim).ToWorldAngle();
        var halfArc = gun.Comp.Arc / 2f;
        _behind.Clear();
        _lookup.GetEntitiesInRange(muzzle, gun.Comp.Range, _behind);
        foreach (var mob in _behind)
        {
            if (mob.Owner == gunner)
                continue;

            var offset = _transform.GetMapCoordinates(mob).Position - muzzle.Position;
            if (offset.LengthSquared() < 0.0001f
                || Math.Abs(Angle.ShortestDistance(offset.ToWorldAngle(), back).Degrees) <= halfArc)
            {
                return mob.Owner;
            }
        }

        return null;
    }
}
