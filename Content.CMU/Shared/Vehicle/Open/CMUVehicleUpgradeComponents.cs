using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// An armor or mobility upgrade for an open vehicle: a heavier part in one of its own slots, like an
/// armored door, or a kit bolted on, like a bull bar. Each adds weight; past the vehicle's free load
/// it slows, and none goes on past its most. Fitting one takes an engineer.
/// </summary>
/// <remarks>
/// The effect fields are what this upgrade does while fitted; those left at their defaults do nothing.
/// A vehicle's fitted upgrades multiply together.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleUpgradeComponent : Component
{
    [DataField]
    public float Weight;

    /// <summary>
    /// A kit, bolted onto the vehicle with a wrench, rather than a part fitted in its slot.
    /// </summary>
    [DataField]
    public bool Kit;

    /// <summary>
    /// The vehicle's sprite layer, and its overlay's, a kit shows on while fitted.
    /// </summary>
    [DataField]
    public string? Layer;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillEngineer";

    [DataField]
    public int SkillLevel = 1;

    /// <summary>
    /// How long a kit takes to bolt on or off.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(6);

    /// <summary>
    /// The vehicle's crash damage, to its hull and its wheels, is multiplied by these.
    /// </summary>
    [DataField]
    public float CrashDamage = 1f;

    [DataField]
    public float CrashWheelDamage = 1f;

    /// <summary>
    /// The engine's share of each hit is multiplied by this; the rest goes on to the hull.
    /// </summary>
    [DataField]
    public float EngineShare = 1f;

    /// <summary>
    /// How hard water drags the vehicle back is multiplied by this.
    /// </summary>
    [DataField]
    public float WaterDrag = 1f;

    /// <summary>
    /// Riders can't be knocked out in a crash.
    /// </summary>
    [DataField]
    public bool CrashProof;

    /// <summary>
    /// As a shut door: it stops shots at the chest of the rider beside it, as well as below, this often.
    /// </summary>
    [DataField]
    public bool CoversChest;

    [DataField]
    public float ChestChance = 0.6f;

    /// <summary>
    /// As a shut tailgate: it stops shots from behind at the legs of riders who get in over it.
    /// </summary>
    [DataField]
    public bool CoversRear;

    /// <summary>
    /// As a windshield: its chance to catch a shot from ahead, and how much it wears by one, instead of
    /// the vehicle's own.
    /// </summary>
    [DataField]
    public float? WindshieldChance;

    [DataField]
    public float? WindshieldWear;
}

/// <summary>
/// An open vehicle that takes upgrades: how much weight it carries free, how much it can take at most,
/// and the kits bolted on it.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUVehicleUpgradeSystem))]
public sealed partial class CMUVehicleUpgradesComponent : Component
{
    [DataField]
    public string Container = "vehicle-upgrades";

    /// <summary>
    /// Upgrade weight the vehicle carries without slowing.
    /// </summary>
    [DataField]
    public float FreeWeight = 60f;

    /// <summary>
    /// The most upgrade weight it takes.
    /// </summary>
    [DataField]
    public float MaxWeight = 160f;

    /// <summary>
    /// The share of top speed and acceleration lost to each unit of weight past the free load.
    /// </summary>
    [DataField]
    public float SpeedLossPerWeight = 0.004f;

    [DataField]
    public ProtoId<ToolQualityPrototype> KitQuality = "Anchoring";

    /// <summary>
    /// The fitted upgrades' weight and effects, summed up whenever one goes on or comes off.
    /// </summary>
    [ViewVariables]
    public float Weight;

    [ViewVariables]
    public float CrashDamage = 1f;

    [ViewVariables]
    public float CrashWheelDamage = 1f;

    [ViewVariables]
    public float EngineShare = 1f;

    [ViewVariables]
    public float WaterDrag = 1f;

    [ViewVariables]
    public bool CrashProof;
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleKitDoAfterEvent : DoAfterEvent
{
    /// <summary>
    /// Bolting on, or off.
    /// </summary>
    [DataField]
    public bool Fit;

    [DataField]
    public NetEntity Kit;

    public CMUVehicleKitDoAfterEvent()
    {
    }

    public CMUVehicleKitDoAfterEvent(bool fit, NetEntity kit)
    {
        Fit = fit;
        Kit = kit;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUVehicleKitDoAfterEvent(Fit, Kit);
    }
}
