using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws a vehicle's turret overlay as the gun fitted to its mount: the machine gun, the grenade
/// launcher or the rocket pod, damaged while it's too damaged to fire. The overlay otherwise only ever
/// shows the mount prototype's one state.
/// </summary>
/// <remarks>
/// The overlay entity is the vehicle turret system's, redrawn only when it first appears; nothing is
/// raised when a gun is swapped or damaged under it, so each frame sets the state, touching the sprite
/// only when it differs. There are a handful of turret overlays at most.
/// </remarks>
public sealed class CMUJeepGunArtSystem : EntitySystem
{
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (!TryGetEntity(visual.Turret, out var turret)
                || HasComp<CMUJeepSentryMountComponent>(turret)
                || FittedGun(turret.Value) is not { } gun
                || !_sprite.TryGetLayer((uid, sprite), 0, out var layer, false))
            {
                continue;
            }

            var state = !_hardpoints.IsHardpointFunctional(gun.Owner) && gun.Comp.DamagedState is { } damaged
                ? damaged
                : gun.Comp.State;

            if (layer.State.Name != state)
                _sprite.LayerSetRsiState((uid, sprite), 0, state);
        }
    }

    private Entity<CMUVehicleTurretArtComponent>? FittedGun(EntityUid mount)
    {
        if (!TryComp(mount, out ItemSlotsComponent? slots))
            return null;

        foreach (var slot in slots.Slots.Values)
        {
            if (slot.Item is { } item && TryComp(item, out CMUVehicleTurretArtComponent? art))
                return (item, art);
        }

        return null;
    }
}
