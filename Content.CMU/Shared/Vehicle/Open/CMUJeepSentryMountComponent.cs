using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A pintle on an open vehicle's turret mount that takes an engineer's sentry gun and fires it by
/// itself. Whoever holds the vehicle's key sets who it shoots: the sentry laptop's friendly
/// factions, which of those it still shoots once they hold a gun, and whether it goes for the
/// closest threat or the biggest one. It swings to follow the target it picks whether or not it can
/// fire on it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CMUJeepSentryMountComponent : Component
{
    /// <summary>
    /// The item slot the sentry sits in.
    /// </summary>
    [DataField]
    public string SlotId = "jeep-sentry";

    /// <summary>
    /// Whether the sentry is switched on: it looks for targets and fires.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Powered = true;

    [DataField, AutoNetworkedField]
    public CMUJeepSentryTargetMode Mode = CMUJeepSentryTargetMode.Closest;

    /// <summary>
    /// Factions it holds fire on, as the sentry laptop names them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> FriendlyFactions = new();

    /// <summary>
    /// Friendly factions it shoots all the same once one of them holds a gun.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> ArmedHostileFactions = new();

    /// <summary>
    /// How far it looks for targets, in tiles, for a sentry not listed in <see cref="SentryRanges"/>:
    /// a standard sentry's sight.
    /// </summary>
    [DataField]
    public float Range = 6f;

    /// <summary>
    /// How far sentries that see further or less than a standard one look from the pintle, by their
    /// prototype: a sniper sentry's long sight, a shotgun sentry's short one. Keep these to the
    /// sentries' own VisionRadius in their HTN blackboards (sentries.yml), which only the server's NPC
    /// code can read.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, float> SentryRanges = new();

    /// <summary>
    /// The least time it spends swinging onto a new target before it opens fire.
    /// </summary>
    [DataField]
    public TimeSpan AimTime = TimeSpan.FromSeconds(0.4);

    /// <summary>
    /// How far off its target the pintle may still be turned and fire, in degrees.
    /// </summary>
    [DataField]
    public float AimTolerance = 10f;

    /// <summary>
    /// How much closer, as a share of the distance, another target has to be before it turns from the
    /// one it has to that one, so it doesn't flick between two at much the same range.
    /// </summary>
    [DataField]
    public float SwitchMargin = 0.75f;

    /// <summary>
    /// The turret overlay's state, in its RSI, while no sentry is on: the bare pintle.
    /// </summary>
    [DataField]
    public string EmptyState = "defense_base_off";

    /// <summary>
    /// It only runs with the vehicle's key in the ignition: pull the key and it goes quiet.
    /// </summary>
    [DataField]
    public bool NeedsKeyIn = true;

    /// <summary>
    /// How often it looks around for a better target.
    /// </summary>
    [DataField]
    public TimeSpan RetargetInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Where the sentry's shots start from: its pivot on the vehicle, in tiles with the vehicle facing
    /// south, and its barrel's length. See <see cref="CMUVehicleGunOriginComponent"/>.
    /// </summary>
    [DataField]
    public Vector2 SentryPivot = new(0f, 0.5f);

    [DataField]
    public float SentryBarrel = 0.5f;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRetarget;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan FireFrom;
}

[Serializable, NetSerializable]
public enum CMUJeepSentryTargetMode : byte
{
    /// <summary>The nearest target.</summary>
    Closest,

    /// <summary>The most dangerous target: the biggest xeno, an armed person over an unarmed one.</summary>
    Biggest,
}

[Serializable, NetSerializable]
public enum CMUJeepSentryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUJeepSentryFriendlyMessage(string faction, bool friendly) : BoundUserInterfaceMessage
{
    public readonly string Faction = faction;
    public readonly bool Friendly = friendly;
}

[Serializable, NetSerializable]
public sealed class CMUJeepSentryArmedMessage(string faction, bool armedHostile) : BoundUserInterfaceMessage
{
    public readonly string Faction = faction;
    public readonly bool ArmedHostile = armedHostile;
}

[Serializable, NetSerializable]
public sealed class CMUJeepSentryModeMessage(CMUJeepSentryTargetMode mode) : BoundUserInterfaceMessage
{
    public readonly CMUJeepSentryTargetMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class CMUJeepSentryPowerMessage(bool powered) : BoundUserInterfaceMessage
{
    public readonly bool Powered = powered;
}
