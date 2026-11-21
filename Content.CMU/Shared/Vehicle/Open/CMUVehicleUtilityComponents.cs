using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// An auxiliary fuel tank kit: while bolted on, the vehicle's tank holds this much more.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleAuxTankComponent : Component
{
    [DataField]
    public FixedPoint2 Capacity = 50;
}

/// <summary>
/// A roof rack kit, a storage of its own: bolted on, it opens from the vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleRoofRackComponent : Component;

/// <summary>
/// Smoke dischargers: the riders fire a ring of smoke around the vehicle, a few times before they
/// want reloading with smoke grenades.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUVehicleUtilitySystem))]
public sealed partial class CMUVehicleSmokeDischargerComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Charges = 2;

    [DataField]
    public int MaxCharges = 2;

    [DataField]
    public EntProtoId Smoke = "RMCSmoke";

    /// <summary>
    /// How far out from the vehicle's middle each charge bursts, in tiles.
    /// </summary>
    [DataField]
    public float Distance = 1.5f;

    /// <summary>
    /// What reloads a charge.
    /// </summary>
    [DataField]
    public EntityWhitelist? Reload;

    [DataField]
    public TimeSpan ReloadDelay = TimeSpan.FromSeconds(2);
}

/// <summary>
/// A winch: hauls a stuck vehicle, a wreck or a crate up behind the vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleWinchComponent : Component
{
    /// <summary>
    /// How far its cable reaches, in tiles.
    /// </summary>
    [DataField]
    public float Range = 6f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Where the hauled load ends up, in vehicle space with the vehicle facing south.
    /// </summary>
    [DataField]
    public System.Numerics.Vector2 Offset = new(0f, 2.2f);

    [DataField]
    public EntityWhitelist? Whitelist;
}

/// <summary>
/// A tow hitch: tows a vehicle, wreck or crate hitched behind it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleTowHitchComponent : Component
{
    /// <summary>
    /// Where the towed load rides, in vehicle space with the vehicle facing south.
    /// </summary>
    [DataField]
    public System.Numerics.Vector2 Offset = new(0f, 2.4f);

    /// <summary>
    /// How near behind the vehicle a load has to be to hitch it.
    /// </summary>
    [DataField]
    public float Reach = 1.5f;

    /// <summary>
    /// The share of top speed the vehicle keeps while towing.
    /// </summary>
    [DataField]
    public float Speed = 0.6f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    [DataField]
    public EntityWhitelist? Whitelist;
}

/// <summary>
/// What a vehicle is towing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUVehicleUtilitySystem))]
public sealed partial class CMUVehicleTowingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Towed;
}

/// <summary>
/// A camo net: spread over a parked vehicle, it is hard to see until it moves.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleCamoNetComponent : Component
{
    [DataField]
    public float Opacity = 0.35f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);
}

/// <summary>
/// A vehicle under its spread camo net.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUVehicleUtilitySystem))]
public sealed partial class CMUVehicleCamouflagedComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleSmokeReloadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleWinchDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleHitchDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleCamoNetDoAfterEvent : SimpleDoAfterEvent;
