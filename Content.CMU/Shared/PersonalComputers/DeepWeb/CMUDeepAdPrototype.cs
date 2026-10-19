using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// An in-universe product ad for the deep web's ad pages. The product is drawn from its entity's own sprite,
/// so a change to the item's art changes its ad too. Ad pages pick ads at random; adding one is only YAML.
/// </summary>
[Prototype("cmuDeepAd")]
public sealed partial class CMUDeepAdPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The product, drawn on the ad as the entity looks in game.</summary>
    [DataField]
    public EntProtoId? Entity;

    /// <summary>
    /// Instead of an item, a person dressed in this starting gear, e.g. a marine for a recruitment ad. Drawn the
    /// way the lobby previews characters, so it always wears the real uniform sprites.
    /// </summary>
    [DataField]
    public ProtoId<StartingGearPrototype>? Gear;

    /// <summary>Who sells it, e.g. Seegson.</summary>
    [DataField(required: true)]
    public LocId Brand;

    /// <summary>The big line, e.g. P-5000 POWER LOADER.</summary>
    [DataField(required: true)]
    public LocId Product;

    [DataField(required: true)]
    public LocId Slogan;

    /// <summary>Shown on a price tag; leave empty for none. Not localized: it is a number on a sticker.</summary>
    [DataField]
    public string Price = string.Empty;

    /// <summary>Background, top to bottom.</summary>
    [DataField]
    public Color Top = Color.FromHex("#0E3B5C");

    [DataField]
    public Color Bottom = Color.FromHex("#06182A");

    /// <summary>Frame, brand line and price tag.</summary>
    [DataField]
    public Color Accent = Color.FromHex("#FFD200");

    [DataField]
    public Color Text = Color.White;

    /// <summary>How big the product sprite is drawn.</summary>
    [DataField]
    public float Scale = 3;

    /// <summary>An animated state of the ad badges RSI shown in the corner, or empty for none.</summary>
    [DataField]
    public string Badge = string.Empty;
}
