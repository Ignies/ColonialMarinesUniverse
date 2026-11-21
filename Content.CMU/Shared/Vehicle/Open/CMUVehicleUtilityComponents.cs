using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// An auxiliary fuel tank kit: while bolted on, the vehicle's tank holds this much more.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleAuxTankComponent : Component
{
    [DataField]
    public FixedPoint2 Capacity = 40;
}

/// <summary>
/// A roof rack kit, a storage of its own: bolted on, it opens from the vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleRoofRackComponent : Component;

/// <summary>
/// Smoke dischargers: the riders fire a ring of smoke around the vehicle, a few times before they
/// want reloading with smoke canisters.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
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

    /// <summary>
    /// How long the dischargers take to fire again.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextFire;
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
    public Vector2 Offset = new(0f, 2.2f);

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
    public Vector2 Offset = new(0f, 2.4f);

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
/// A load in tow behind a vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUVehicleUtilitySystem))]
public sealed partial class CMUVehicleTowedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Tower;
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
/// A vehicle under its spread camo net. The net comes off when it moves, or any gun aboard fires.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUVehicleUtilitySystem))]
public sealed partial class CMUVehicleCamouflagedComponent : Component
{
    /// <summary>
    /// The latest time any gun aboard could next fire when the net went up: a gun past it has fired.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LastShot;

    /// <summary>
    /// The entities hidden under the net with the vehicle: its overlay and gun overlays.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> Hidden = new();
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleSmokeReloadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleWinchDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleHitchDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleCamoNetDoAfterEvent : SimpleDoAfterEvent;
