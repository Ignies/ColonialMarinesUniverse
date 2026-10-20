using System.Numerics;
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

    /// <summary>The machine's casing when set, e.g. a Marshals' desk running the colony OS; else the OS's own.</summary>
    [DataField, AutoNetworkedField]
    public CMUComputerCasing? Casing;

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

    /// <summary>
    /// The round's GOVFOR platoon, a <c>PlatoonPrototype</c> id, or null before one is picked. Sites with
    /// <see cref="CMUNetSitePrototype.PlatoonPages"/> serve that platoon's own pages. Kept current by the server.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Platoon;

    /// <summary>Documents on this computer, in addition to the OS ones. Mappers use it for notes left on a PC.</summary>
    [DataField, AutoNetworkedField]
    public List<CMUComputerDocument> Documents = new();

    /// <summary>Pages saved to the desktop with the browser's save link button. Lasts the round.</summary>
    [DataField, AutoNetworkedField]
    public List<CMUNetShortcut> Shortcuts = new();

    /// <summary>The reader on the front panel. A card stays in it for the whole session.</summary>
    public const string CardSlotId = "cmu_pc_card";

    /// <summary>Switched on with the power button. Losing power switches it off.</summary>
    [DataField, AutoNetworkedField]
    public bool On;

    /// <summary>
    /// When the machine was last switched on. Someone who walks up after it booted finds it running, with no
    /// boot to sit through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PoweredAt;

    /// <summary>
    /// The programs left open on screen, so whoever looks at the terminal next, the same player or another, finds
    /// it as it was left. Cleared when the session ends or the machine goes off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CMUComputerScreenWindow> Screen = new();

    /// <summary>The card in the reader, as a prototype for the panel to draw; null when the reader is empty.</summary>
    [DataField, AutoNetworkedField]
    public string? CardPrototype;

    /// <summary>Whether the terminal's network goes through the colony sensor tower. Off for wired consoles.</summary>
    [DataField]
    public bool NeedsTower = true;

    /// <summary>Whether the terminal reaches the network right now. Kept up to date by the server.</summary>
    [DataField, AutoNetworkedField]
    public bool Carrier = true;

    /// <summary>
    /// Tuned off the colony network with an access tuner: it reaches no colony network, only its own files and the
    /// deep web if hacked, and nothing it does is reported live. Mappers can leave a terminal dark.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Dark;

    /// <summary>This round's terminal number, e.g. 23 for Terminal #23. Given out by the server at map init.</summary>
    [DataField, AutoNetworkedField]
    public int TerminalId;

    /// <summary>Whose ID card opened the session, or null while the terminal waits at its login screen.</summary>
    [DataField, AutoNetworkedField]
    public string? SessionUser;

    [DataField, AutoNetworkedField]
    public string? SessionJob;

    /// <summary>When a Marshal trace on this terminal completes; zero when nobody is tracing it. Shown on screen.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TraceEnds;

    [DataField, AutoNetworkedField]
    public TimeSpan TraceStarted;

    /// <summary>The bank account of the card that opened the session, for bounties and purchases. Server only.</summary>
    [ViewVariables]
    public int SessionAccount;

    /// <summary>Whether the desktop waits for an ID card. Off for kiosks.</summary>
    [DataField, AutoNetworkedField]
    public bool RequireLogin = true;

    /// <summary>
    /// For consoles whose click already opens something else, like the Marshals' camera monitors: the terminal
    /// opens from a verb instead.
    /// </summary>
    [DataField]
    public bool OpenVerb;

    /// <summary>Notes saved in this terminal's Files program. Anyone at the terminal can read or delete them.</summary>
    [DataField, AutoNetworkedField]
    public List<CMUComputerNote> Notes = new();

    /// <summary>A wallpaper someone found on the ExtraNet and put on this desktop, over the OS one.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CMUWallpaperPrototype>? Wallpaper;

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

/// <summary>A program window left open on a terminal's screen.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUComputerScreenWindow
{
    /// <summary>The program, by name, e.g. Browser.</summary>
    [DataField(required: true)]
    public string App = string.Empty;

    /// <summary>The page on screen, for the browsers.</summary>
    [DataField]
    public string? Address;

    [DataField]
    public Vector2 Position;

    [DataField]
    public Vector2 Size;

    [DataField]
    public bool Minimized;
}

/// <summary>What the terminal's screen shows now, sent when it changes.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerScreenMessage(List<CMUComputerScreenWindow> windows) : BoundUserInterfaceMessage
{
    public readonly List<CMUComputerScreenWindow> Windows = windows;
}

public static class CMUComputerScreenLimits
{
    public const int Windows = 8;
    public const int AppLength = 24;
    public const int AddressLength = 256;

    /// <summary>Bigger than any screen; positions and sizes are clamped to it.</summary>
    public const float Extent = 4096;
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
