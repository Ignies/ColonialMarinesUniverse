using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws the sentry on a jeep's sentry pintle as the sentry it is: a mini, sniper, shotgun or flamer
/// sentry in its own art, out of ammo or wrecked as it stands, and nothing with the pintle empty. The
/// turret overlay otherwise only ever shows the pintle prototype's one state.
/// </summary>
public sealed class CMUJeepSentryOverlaySystem : EntitySystem
{
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private CMUJeepSentryMountSystem _mounts = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    // The sentry's icon is its item art: "<sentry>_handheld". Its deployed art drops the suffix.
    private const string HandheldSuffix = "_handheld";

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (!TryGetEntity(visual.Turret, out var turret) ||
                !TryComp(turret, out CMUJeepSentryMountComponent? mount))
            {
                continue;
            }

            if (_mounts.GetSentry((turret.Value, mount)) is not { } sentry ||
                !TryComp(sentry, out IconComponent? icon) ||
                !icon.Icon.RsiState.EndsWith(HandheldSuffix))
            {
                _sprite.LayerSetVisible((uid, sprite), 0, false);
                continue;
            }

            var art = icon.Icon.RsiState[..^HandheldSuffix.Length];
            var state = !_hardpoints.IsHardpointFunctional(turret.Value)
                ? $"{art}_destroyed"
                : HasAmmo(sentry) ? $"{art}_on" : $"{art}_noammo";

            if (!_sprite.TryGetLayer((uid, sprite), 0, out var layer, false))
                continue;

            if (layer.ActualRsi?.Path != icon.Icon.RsiPath)
                _sprite.LayerSetRsi((uid, sprite), 0, icon.Icon.RsiPath, state);
            else if (layer.State.Name != state)
                _sprite.LayerSetRsiState((uid, sprite), 0, state);

            _sprite.LayerSetVisible((uid, sprite), 0, true);
        }
    }

    private bool HasAmmo(EntityUid sentry)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(sentry, ref ammo);
        return ammo.Count > 0;
    }
}
