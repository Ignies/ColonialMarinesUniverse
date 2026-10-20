using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>A panel on the colony security network.</summary>
[Serializable, NetSerializable]
public enum CMUSecurityPanel : byte
{
    Map,
    Log,
}

/// <summary>Something done at a terminal that colony security wants to know about.</summary>
[Serializable, NetSerializable]
public enum CMUSecurityEvent : byte
{
    /// <summary>A card was refused at the reader for lack of access.</summary>
    LoginRefused,

    /// <summary>The terminal was tuned off the colony network. The last thing a dark terminal reports.</summary>
    WentDark,

    /// <summary>The terminal was tuned back onto the colony network.</summary>
    Rejoined,

    /// <summary>Someone opened the terminal's maintenance port with an access tuner to hack it.</summary>
    HackStarted,

    LogWiped,
    LogForged,

    /// <summary>An unhacked terminal looked up a live deep web address.</summary>
    DeepLookup,
}

/// <summary>How a terminal shows on the security map.</summary>
[Serializable, NetSerializable]
public enum CMUSecurityStatus : byte
{
    /// <summary>Switched on, waiting at its login screen.</summary>
    Online,
    LoggedIn,

    /// <summary>Powered, but switched off at its button.</summary>
    Off,
    Unpowered,

    /// <summary>Tuned off the colony network: shown where it was last seen.</summary>
    Dark,
}

/// <summary>A line in the unauthorized use log.</summary>
[Serializable, NetSerializable]
public sealed class CMUSecurityEntry(
    TimeSpan time,
    int terminalId,
    CMUSecurityEvent kind,
    string location,
    string? name = null,
    string? detail = null)
{
    public readonly TimeSpan Time = time;
    public readonly int TerminalId = terminalId;
    public readonly CMUSecurityEvent Kind = kind;
    public readonly string Location = location;

    /// <summary>The name on the card involved: the refused card, or the one in the reader. Null when there was none.</summary>
    public readonly string? Name = name;

    /// <summary>The address of a deep lookup; null for everything else.</summary>
    public readonly string? Detail = detail;
}

/// <summary>A terminal on the security map, in the grid's own coordinates.</summary>
[Serializable, NetSerializable]
public sealed class CMUSecurityTerminal(
    int terminalId,
    NetEntity entity,
    Vector2 position,
    CMUSecurityStatus status,
    bool hacked,
    string location)
{
    public readonly int TerminalId = terminalId;
    public readonly NetEntity Entity = entity;

    /// <summary>Where it stands, or for a dark terminal where it was when it dropped off.</summary>
    public readonly Vector2 Position = position;
    public readonly CMUSecurityStatus Status = status;

    /// <summary>Hacked into the deep web, and security saw the hack start.</summary>
    public readonly bool Hacked = hacked;

    /// <summary>The area at <see cref="Position"/>.</summary>
    public readonly string Location = location;
}

[Serializable, NetSerializable]
public sealed class CMUSecurityRequestMessage(CMUSecurityPanel panel) : BoundUserInterfaceMessage
{
    public readonly CMUSecurityPanel Panel = panel;
}

/// <summary>What one security panel shows. Only the lists that panel needs are filled.</summary>
[Serializable, NetSerializable]
public sealed class CMUSecurityStateMessage(
    CMUSecurityPanel panel,
    List<CMUSecurityEntry> entries,
    List<CMUSecurityTerminal> terminals,
    NetEntity? grid) : BoundUserInterfaceMessage
{
    public readonly CMUSecurityPanel Panel = panel;
    public readonly List<CMUSecurityEntry> Entries = entries;
    public readonly List<CMUSecurityTerminal> Terminals = terminals;
    public readonly NetEntity? Grid = grid;
}
