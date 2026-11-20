using Content.Shared.CMU14.Vehicle.Open;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    [Dependency] private CMUVehiclePushSystem _cmuHandPush = default!;

    // Top speeds of vehicles last pushed by hand, held while they roll on after the push stops.
    private readonly Dictionary<EntityUid, float> _cmuHandPushSpeeds = new();

    private bool TryGetCMUHandPush(EntityUid vehicle, EntityUid grid, out Vector2i direction)
    {
        if (_cmuHandPush.TryGetPushInput(vehicle, grid, out direction, out var maxSpeed))
        {
            _cmuHandPushSpeeds[vehicle] = maxSpeed;
            return true;
        }

        _cmuHandPushSpeeds.Remove(vehicle);
        return false;
    }

    private float CMUCapHandPushSpeed(EntityUid vehicle, float maxSpeed)
    {
        return _cmuHandPushSpeeds.TryGetValue(vehicle, out var handSpeed) ? MathF.Min(maxSpeed, handSpeed) : maxSpeed;
    }

    private bool IsCMUHandPusherOf(EntityUid mob, EntityUid vehicle)
    {
        return TryComp(mob, out CMUVehiclePushingComponent? pushing) && pushing.Vehicle == vehicle;
    }

    private void CMUOverpowerHandPushers(EntityUid vehicle)
    {
        _cmuHandPushSpeeds.Remove(vehicle);
        _cmuHandPush.KnockOffPushers(vehicle);
    }
}
