using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// What YouTape makes its random tapes from: the grammar the stories, titles and channel names are told in, and the
/// backgrounds, outfits, props and ads each kind of scene draws on. Every tape is built from its seed on the client,
/// so the same seed is the same tape for everyone. Rules are expanded like Tracery: <c>#rule#</c> is replaced by a
/// random line of that rule, which may hold rules of its own; <c>{colony}</c> becomes the round's map name.
/// </summary>
[Prototype("cmuTapeGrammar")]
public sealed partial class CMUTapeGrammarPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<string, List<string>> Rules = new();

    /// <summary>Background textures by scene kind: studio, field, weather, ad, food, bunk, wild, card.</summary>
    [DataField(required: true)]
    public Dictionary<string, List<string>> Backgrounds = new();

    /// <summary>Starting gear the people on tape wear, by role: anchor, reporter, presenter, host, guest.</summary>
    [DataField(required: true)]
    public Dictionary<string, List<string>> Outfits = new();

    /// <summary>Entities shown as props, by scene kind.</summary>
    [DataField]
    public Dictionary<string, List<EntProtoId>> Props = new();

    /// <summary>Ad pictures shown in the ad breaks.</summary>
    [DataField]
    public List<string> Ads = new();

    /// <summary>Weather icons, by condition: sun, cloud, rain, storm, snow, fog, wind, dust, night.</summary>
    [DataField]
    public Dictionary<string, string> Weather = new();
}

/// <summary>A broadcasting camera on the TV network, as YouTape's live page lists it.</summary>
[Serializable, NetSerializable]
public sealed class CMUTapeLiveFeed(NetEntity camera, string holder, string location)
{
    public readonly NetEntity Camera = camera;

    /// <summary>Who carries it, or empty when it is set down.</summary>
    public readonly string Holder = holder;
    public readonly string Location = location;
}

[Serializable, NetSerializable]
public sealed class CMUTapeLiveRequestMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUTapeLiveStateMessage(List<CMUTapeLiveFeed> feeds) : BoundUserInterfaceMessage
{
    public readonly List<CMUTapeLiveFeed> Feeds = feeds;
}

/// <summary>Opens the TV network's cameras on this terminal, to watch a broadcast live.</summary>
[Serializable, NetSerializable]
public sealed class CMUTapeWatchLiveMessage(NetEntity camera) : BoundUserInterfaceMessage
{
    public readonly NetEntity Camera = camera;
}
