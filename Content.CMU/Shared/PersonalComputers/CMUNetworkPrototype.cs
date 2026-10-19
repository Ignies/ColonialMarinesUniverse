using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// An ExtraNet network. Sites list the networks they are on, computers list the networks they can reach.
/// </summary>
[Prototype("cmuNetwork")]
public sealed partial class CMUNetworkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Name shown on the access denied page.</summary>
    [DataField(required: true)]
    public LocId Name;
}
