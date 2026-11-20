using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A vehicle people on foot can push by hand. Ctrl+click its front or back to brace against it, then
/// walk into it: from behind it rolls forward, from ahead it rolls back, always slowly. Nobody can push
/// it while someone is at the wheel.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehiclePushableComponent : Component
{
    /// <summary>
    /// Top speed, in tiles a second, one person pushing gets the vehicle to.
    /// </summary>
    [DataField]
    public float PushSpeed = 0.5f;

    /// <summary>
    /// Top speed each further person pushing the same way adds.
    /// </summary>
    [DataField]
    public float ExtraPusherSpeed = 0.25f;

    /// <summary>
    /// The most any number of people can push it to.
    /// </summary>
    [DataField]
    public float MaxPushSpeed = 1f;

    /// <summary>
    /// Half the vehicle's length and width, in tiles, with it facing south: where its front and back
    /// bumpers are, and how wide they are.
    /// </summary>
    [DataField]
    public Vector2 HalfExtents = new(0.45f, 1.05f);

    /// <summary>
    /// How far from a bumper someone may stand, in tiles, and still brace against it.
    /// </summary>
    [DataField]
    public float Reach = 1f;

    /// <summary>
    /// Where a pusher stands, in tiles beyond the bumper.
    /// </summary>
    [DataField]
    public float StandOff = 0.3f;

    /// <summary>
    /// How far a pusher's sprite leans in toward the vehicle, in tiles.
    /// </summary>
    [DataField]
    public float LeanIn = 6f / 32f;

    /// <summary>
    /// How long people pushing it are knocked down when a xeno shoves it back against them.
    /// </summary>
    [DataField]
    public TimeSpan KnockedOffTime = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Someone braced against a vehicle's front or back to push it. They stand still against the bumper,
/// following it as it rolls, until they walk any way but into it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehiclePushingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    /// <summary>
    /// Braced against the front, pushing the vehicle back; otherwise against the back, pushing it on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Front;

    /// <summary>
    /// Whether they are walking into the vehicle right now.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Pushing;

    /// <summary>
    /// Where along the bumper they stand, in tiles from its middle, with the vehicle facing south.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Across;

    /// <summary>
    /// How far their sprite is drawn toward the vehicle, in world space, while braced.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset;
}
