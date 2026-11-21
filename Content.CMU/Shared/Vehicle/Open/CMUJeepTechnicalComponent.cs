using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A gun mount frame that rides in a cargo bed like a crate. Bolted down, its mount goes into the
/// vehicle's turret slot and the bed's gunner seat opens; unbolted, the mount comes back into the frame.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUJeepTechnicalSystem))]
public sealed partial class CMUJeepTechnicalComponent : Component
{
    /// <summary>
    /// Where the frame keeps its mount while it isn't bolted down.
    /// </summary>
    [DataField]
    public string Container = "technical-mount";

    /// <summary>
    /// The vehicle slot the mount goes into once the frame is bolted down.
    /// </summary>
    [DataField]
    public string TurretSlot = "turret";
}

/// <summary>
/// Raised on a crate in a cargo bed when it's bolted down or let loose, before an unloaded crate leaves.
/// </summary>
[ByRefEvent]
public readonly record struct CMUVehicleCargoSecuredEvent(EntityUid Vehicle, bool Secured);
