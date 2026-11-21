using Content.Shared.DoAfter;
using Content.Shared.Item;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A vehicle gun mount that clamps a handheld gun and fires it: the gun's own rounds, rate and sound,
/// steadier for being on a mount. The gun only fires once it's screwed in, and can't be taken out until
/// it's unscrewed.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUGunCradleSystem))]
public sealed partial class CMUGunCradleComponent : Component
{
    [DataField]
    public string Slot = "cradle-gun";

    /// <summary>
    /// The cradled gun's spread is multiplied by this, by the gun's size: a heavy gun steadies most
    /// on a mount. Sizes not listed use <see cref="DefaultSpread"/>.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ItemSizePrototype>, float> SpreadBySize = new();

    [DataField]
    public float DefaultSpread = 0.75f;

    /// <summary>
    /// The cradled gun's recoil is multiplied by this.
    /// </summary>
    [DataField]
    public float Recoil = 0.5f;

    [DataField]
    public ProtoId<ToolQualityPrototype> ClampQuality = "Screwing";

    [DataField]
    public TimeSpan ClampDelay = TimeSpan.FromSeconds(3);
}

/// <summary>
/// A gun clamped in a cradle: it shoots as true as wielded, though nobody holds it.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUGunCradleSystem))]
public sealed partial class CMUCradledGunComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMUGunCradleClampDoAfterEvent : SimpleDoAfterEvent;
