using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// The art a mounted gun is drawn with on its vehicle's turret overlay, from the overlay's own RSI.
/// A mount swapped from one gun to another draws whichever is fitted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleTurretArtComponent : Component
{
    [DataField(required: true)]
    public string State = string.Empty;

    /// <summary>
    /// Drawn while the gun is too damaged to fire.
    /// </summary>
    [DataField]
    public string? DamagedState;
}

/// <summary>
/// A gun that won't fire at anything closer than this, like a grenade launcher whose rounds would
/// burst on the vehicle firing them.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUJeepGunSystem))]
public sealed partial class CMUGunMinimumRangeComponent : Component
{
    /// <summary>
    /// In tiles, from the muzzle.
    /// </summary>
    [DataField]
    public float Range = 3f;
}

/// <summary>
/// A gun that throws its blast out behind it as it fires, like a rocket launcher: anyone standing in it,
/// but its gunner and the vehicle's riders, is knocked down and scorched.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUJeepGunSystem))]
public sealed partial class CMUGunBackblastComponent : Component
{
    /// <summary>
    /// How far behind the muzzle the blast reaches, in tiles.
    /// </summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>
    /// How wide the blast spreads behind the muzzle, in degrees.
    /// </summary>
    [DataField]
    public float Arc = 60f;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    [DataField]
    public DamageSpecifier Damage = new();
}
