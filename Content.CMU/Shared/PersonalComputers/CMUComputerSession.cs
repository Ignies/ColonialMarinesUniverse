using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>A line in a computer's log, read by engineers with a multitool.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerLogEntry(TimeSpan time, string text)
{
    public readonly TimeSpan Time = time;
    public readonly string Text = text;
}

/// <summary>Tells the server where the browser went, for the log and for the Marshals' lookup alerts.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetVisitMessage(string address, bool found) : BoundUserInterfaceMessage
{
    public readonly string Address = address;
    public readonly bool Found = found;
}

/// <summary>Ends the session from the start menu, which hands the card back.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerLogoutMessage : BoundUserInterfaceMessage;

/// <summary>The power button on the front panel.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerPowerButtonMessage : BoundUserInterfaceMessage;

/// <summary>A click on the card reader: puts in the user's card, or takes the one inside back out.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerCardSlotMessage : BoundUserInterfaceMessage;

/// <summary>The radial menu a multitool opens on a computer.</summary>
[Serializable, NetSerializable]
public enum CMUComputerToolUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUComputerToolAction : byte
{
    Logs,
    Hack,

    /// <summary>Tunes the terminal off the colony network, or back onto it.</summary>
    Retune,
}

[Serializable, NetSerializable]
public sealed class CMUComputerToolMessage(CMUComputerToolAction action) : BoundUserInterfaceMessage
{
    public readonly CMUComputerToolAction Action = action;
}

/// <summary>The log viewer opened from the multitool menu.</summary>
[Serializable, NetSerializable]
public enum CMUComputerLogUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUComputerLogState(int terminalId, List<CMUComputerLogEntry> entries, bool canTamper = false)
    : BoundUserInterfaceState
{
    public readonly int TerminalId = terminalId;
    public readonly List<CMUComputerLogEntry> Entries = entries;

    /// <summary>Whether the reader knows enough to wipe or forge the log.</summary>
    public readonly bool CanTamper = canTamper;
}

public static class CMUComputerLimits
{
    public const int LogEntries = 120;
    public const int ForgedLength = 120;
    public static readonly TimeSpan TraceTime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan TraceCooldown = TimeSpan.FromMinutes(2);
}

/// <summary>A panel on the Colonial Marshal Bureau network.</summary>
[Serializable, NetSerializable]
public enum CMUCmbPanel : byte
{
    Alerts,
    Map,
    Faxes,
    Records,
}

/// <summary>An unhacked terminal looked up a live deep web address.</summary>
[Serializable, NetSerializable]
public sealed class CMUCmbAlert(TimeSpan time, int terminalId, string address, string location, string? trace = null)
{
    public readonly TimeSpan Time = time;
    public readonly int TerminalId = terminalId;
    public readonly string Address = address;
    public readonly string Location = location;

    /// <summary>For a finished trace: who was logged in, or that the session ended first. Null for lookups.</summary>
    public readonly string? Trace = trace;
}

/// <summary>A Marshal starts tracing a terminal from the map.</summary>
[Serializable, NetSerializable]
public sealed class CMUCmbTraceMessage(NetEntity terminal) : BoundUserInterfaceMessage
{
    public readonly NetEntity Terminal = terminal;
}

/// <summary>Clears a terminal's log; the clearing itself is logged.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerLogWipeMessage : BoundUserInterfaceMessage;

/// <summary>Adds a made up line to a terminal's log.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerLogForgeMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

/// <summary>A terminal pinned on the Marshals' map, in the grid's own coordinates.</summary>
[Serializable, NetSerializable]
public sealed class CMUCmbTerminal(int terminalId, NetEntity entity, Vector2 position, bool alerted)
{
    public readonly int TerminalId = terminalId;
    public readonly NetEntity Entity = entity;
    public readonly Vector2 Position = position;
    public readonly bool Alerted = alerted;
}

[Serializable, NetSerializable]
public sealed class CMUCmbFax(TimeSpan time, string from, string to, string title, string content)
{
    public readonly TimeSpan Time = time;
    public readonly string From = from;
    public readonly string To = to;
    public readonly string Title = title;
    public readonly string Content = content;
}

[Serializable, NetSerializable]
public sealed class CMUCmbRecord(string name, string status, string reason, List<string> history)
{
    public readonly string Name = name;
    public readonly string Status = status;
    public readonly string Reason = reason;
    public readonly List<string> History = history;
}

[Serializable, NetSerializable]
public sealed class CMUCmbRequestMessage(CMUCmbPanel panel) : BoundUserInterfaceMessage
{
    public readonly CMUCmbPanel Panel = panel;
}

/// <summary>What one CMB panel shows. Only the lists that panel needs are filled.</summary>
[Serializable, NetSerializable]
public sealed class CMUCmbStateMessage(
    CMUCmbPanel panel,
    List<CMUCmbAlert> alerts,
    List<CMUCmbTerminal> terminals,
    List<CMUCmbFax> faxes,
    List<CMUCmbRecord> records,
    NetEntity? grid) : BoundUserInterfaceMessage
{
    public readonly CMUCmbPanel Panel = panel;
    public readonly List<CMUCmbAlert> Alerts = alerts;
    public readonly List<CMUCmbTerminal> Terminals = terminals;
    public readonly List<CMUCmbFax> Faxes = faxes;
    public readonly List<CMUCmbRecord> Records = records;
    public readonly NetEntity? Grid = grid;
}
