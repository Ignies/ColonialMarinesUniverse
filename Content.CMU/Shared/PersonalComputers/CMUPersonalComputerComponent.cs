using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// A computer that boots an OS and browses the ExtraNet. Pages ship with the client, so browsing is client side.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPersonalComputerComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<CMUComputerOsPrototype> Os = "CMUOsSeegson";

    /// <summary>Overrides the OS networks when set, e.g. a colony PC wired into a corporate network.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<CMUNetworkPrototype>>? Networks;

    /// <summary>Overrides the OS home page when set.</summary>
    [DataField, AutoNetworkedField]
    public string? HomePage;

    /// <summary>
    /// Folder of this computer's colony pages: a <see cref="CMUNetColonyPrototype"/> id, or the round's game map
    /// id when no colony lists the map. Set by the server from the round's map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Colony;

    /// <summary>What <c>{colony}</c> becomes in pages: the round's map name.</summary>
    [DataField, AutoNetworkedField]
    public string? ColonyName;

    /// <summary>Documents on this computer, in addition to the OS ones. Mappers use it for notes left on a PC.</summary>
    [DataField, AutoNetworkedField]
    public List<CMUComputerDocument> Documents = new();

    /// <summary>Pages saved to the desktop with the browser's save link button. Lasts the round.</summary>
    [DataField, AutoNetworkedField]
    public List<CMUNetShortcut> Shortcuts = new();

    /// <summary>This round's terminal number, e.g. 23 for Terminal #23. Given out by the server at map init.</summary>
    [DataField, AutoNetworkedField]
    public int TerminalId;

    /// <summary>Whose ID card opened the session, or null while the terminal waits at its login screen.</summary>
    [DataField, AutoNetworkedField]
    public string? SessionUser;

    [DataField, AutoNetworkedField]
    public string? SessionJob;

    /// <summary>Whether the desktop waits for an ID card. Off for kiosks.</summary>
    [DataField, AutoNetworkedField]
    public bool RequireLogin = true;

    /// <summary>
    /// For consoles whose click already opens something else, like the Marshals' camera monitors: the terminal
    /// opens from a verb instead.
    /// </summary>
    [DataField]
    public bool OpenVerb;

    /// <summary>What happened at this terminal this round. Server only; engineers read it with a multitool.</summary>
    [ViewVariables]
    public List<CMUComputerLogEntry> Log = new();
}

/// <summary>A page saved to the desktop.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUNetShortcut
{
    [DataField(required: true)]
    public string Address = string.Empty;

    [DataField]
    public string Title = string.Empty;
}

/// <summary>Saves the page on screen to the desktop.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetSaveShortcutMessage(string address, string title) : BoundUserInterfaceMessage
{
    public readonly string Address = address;
    public readonly string Title = title;
}

[Serializable, NetSerializable]
public sealed class CMUNetRemoveShortcutMessage(string address) : BoundUserInterfaceMessage
{
    public readonly string Address = address;
}

public static class CMUNetShortcutLimits
{
    public const int Count = 10;
    public const int AddressLength = 96;
    public const int TitleLength = 32;
}

/// <summary>A read only document in the Files app, written in the same format as ExtraNet pages.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUComputerDocument
{
    /// <summary>File name shown in Files, e.g. <c>README.TXT</c>. Not localized: it is a file name.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public ResPath Path;
}

[Serializable, NetSerializable]
public enum CMUPersonalComputerUiKey : byte
{
    Key,
}
