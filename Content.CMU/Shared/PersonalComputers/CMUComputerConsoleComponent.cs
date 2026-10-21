using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// A colony console running the PC OS. Using the console opens the OS; its own screens are programs on the desktop,
/// each opening the console's old interface in a window of its own while the machine is on and a card is in.
/// Added and removed at runtime by the console bridge, so console prototypes need no changes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUComputerConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<CMUConsoleApp> Apps = new();

    /// <summary>
    /// The key the console opened before the bridge took it over, put back when the bridge lets go. Null on a
    /// console that was never bridged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Enum? OriginalKey;

    /// <summary>
    /// The console was a PC of its own before the bridge, opened by a verb, like the Marshals' camera monitors: it keeps
    /// its PC when the bridge lets go.
    /// </summary>
    [DataField]
    public bool NativeComputer;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUConsoleApp
{
    /// <summary>The program's name on the desktop: a locale id or plain text, usually the console's own name.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>The console's own interface the program opens.</summary>
    [DataField(required: true)]
    public Enum Key = default!;
}

/// <summary>Opens the console program at this index in <see cref="CMUComputerConsoleComponent.Apps"/>.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerOpenConsoleMessage(int app) : BoundUserInterfaceMessage
{
    public readonly int App = app;
}

/// <summary>
/// Which consoles the bridge turns into PCs, placed anchored on a colony (planet) map: any whose prototype is, or
/// inherits from, an included prototype or one named like a computer (a keyword in its id), and none of the excluded
/// ones or one with an excluded keyword. So a new console named like one, or made from the usual parents, is picked up
/// without changes.
/// </summary>
[Prototype("cmuComputerBridge")]
public sealed partial class CMUComputerBridgePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<EntProtoId> Include = new();

    [DataField]
    public List<EntProtoId> Exclude = new();

    /// <summary>A prototype whose id, or a parent's, has one of these in it is a computer, e.g. Console, Monitor.</summary>
    [DataField]
    public List<string> Keywords = new();

    /// <summary>A prototype whose id, or a parent's, has one of these in it is never bridged, e.g. Dropship.</summary>
    [DataField]
    public List<string> ExcludeKeywords = new();

    /// <summary>The OS for consoles inheriting from a prototype; the first match up the parent chain wins.</summary>
    [DataField]
    public Dictionary<EntProtoId, ProtoId<CMUComputerOsPrototype>> Os = new();

    /// <summary>
    /// Networks added to the OS ones for consoles inheriting from a prototype, e.g. colony security's network on its
    /// own consoles. Every match up the parent chain adds its networks.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, List<ProtoId<CMUNetworkPrototype>>> Networks = new();
}
