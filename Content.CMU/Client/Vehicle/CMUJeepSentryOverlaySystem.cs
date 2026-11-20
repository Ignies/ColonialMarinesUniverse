using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws the sentry on a jeep's sentry pintle as the sentry it is: a mini, sniper, shotgun or flamer
/// sentry in its own art, lit while it runs, dark while it doesn't, out of ammo or wrecked as it stands,
/// and the bare pintle with none on. The turret overlay otherwise only ever shows the pintle
/// prototype's one state.
/// </summary>
/// <remarks>
/// Only the sentry's head is drawn. On the ground most sentries also stand on a base plate, drawn as a
/// separate layer; on the jeep the pintle and its pedestal take the plate's place.
/// The overlay entity is the vehicle turret system's, redrawn only when it first appears; nothing is
/// raised when the sentry's ammo, power or the vehicle's state change, so each frame sets the state,
/// touching the sprite only when it differs. There are a handful of turret overlays at most.
/// </remarks>
public sealed class CMUJeepSentryOverlaySystem : EntitySystem
{
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private CMUJeepSentryMountSystem _mounts = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private VehicleTurretSystem _turrets = default!;

    // The sentry's icon is its item art: "<sentry>_handheld". Its deployed art drops the suffix.
    private const string HandheldSuffix = "_handheld";

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (!TryGetEntity(visual.Turret, out var turret)
                || !TryComp(turret, out CMUJeepSentryMountComponent? mount)
                || !TryComp(turret, out VehicleTurretComponent? turretComp))
            {
                continue;
            }

            var (rsi, state) = Art((turret.Value, mount), turretComp);
            if (!_sprite.TryGetLayer((uid, sprite), 0, out var layer, false))
                continue;

            if (layer.ActualRsi?.Path != rsi)
                _sprite.LayerSetRsi((uid, sprite), 0, rsi, state);
            else if (layer.State.Name != state)
                _sprite.LayerSetRsiState((uid, sprite), 0, state);

            _sprite.LayerSetVisible((uid, sprite), 0, true);
        }
    }

    private (ResPath Rsi, string State) Art(Entity<CMUJeepSentryMountComponent> mount, VehicleTurretComponent turret)
    {
        if (_mounts.GetSentry(mount) is not { } sentry
            || !TryComp(sentry, out IconComponent? icon)
            || !icon.Icon.RsiState.EndsWith(HandheldSuffix))
        {
            return (new ResPath(turret.OverlayRsi), mount.Comp.EmptyState);
        }

        var art = icon.Icon.RsiState[..^HandheldSuffix.Length];
        string state;
        if (!_hardpoints.IsHardpointFunctional(mount))
            state = $"{art}_destroyed";
        else if (!HasAmmo(sentry))
            state = $"{art}_noammo";
        else if (_turrets.TryGetVehicle(mount, out var vehicle) && _mounts.CanRun(mount, vehicle))
            state = $"{art}_on";
        else
            state = art;

        return (icon.Icon.RsiPath, state);
    }

    private bool HasAmmo(EntityUid sentry)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(sentry, ref ammo);
        return ammo.Count > 0;
    }
}
