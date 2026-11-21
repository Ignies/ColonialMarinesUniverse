using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    [Dependency] private CMUVehicleTyreSystem _cmuTyres = default!;
    [Dependency] private CMUVehicleUpgradeSystem _cmuUpgrades = default!;

    /// <summary>
    /// The tyres' grip on the ground under the vehicle, and its upgrades' weight.
    /// </summary>
    private float CMUTyreSpeed(EntityUid vehicle)
    {
        return _cmuTyres.SpeedMultiplier(vehicle) * _cmuUpgrades.SpeedMultiplier(vehicle);
    }

    /// <summary>
    /// The tyres' drag in water, and a snorkel's.
    /// </summary>
    private float CMUTyreWaterDrag(EntityUid vehicle)
    {
        return _cmuTyres.WaterDrag(vehicle) * _cmuUpgrades.WaterDrag(vehicle);
    }
}
