using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// Optional: lets several game maps share one folder of custom ExtraNet pages, e.g. every Stable Garrison map
/// uses <c>StableGarrison/</c>. Maps need no entry here: a map's own folder is its game map id, and
/// <c>{colony}</c> is always the map's name.
/// </summary>
[Prototype("cmuNetColony")]
public sealed partial class CMUNetColonyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Game map ids. Strings: game maps are a server only prototype kind, which the linter will not resolve.</summary>
    [DataField(required: true)]
    public HashSet<string> Maps = new();
}
