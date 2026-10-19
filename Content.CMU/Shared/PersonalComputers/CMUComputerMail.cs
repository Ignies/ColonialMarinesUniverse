using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>A mail between colonists, addressed by name. Mailboxes belong to names, so any terminal reads them.</summary>
[Serializable, NetSerializable]
public sealed class CMUMailMessage(int id, string from, string to, string subject, string body, TimeSpan time)
{
    public readonly int Id = id;
    public readonly string From = from;
    public readonly string To = to;
    public readonly string Subject = subject;
    public readonly string Body = body;
    public readonly TimeSpan Time = time;
}

/// <summary>Asks for the logged in user's mailbox; the server answers with <see cref="CMUMailStateMessage"/>.</summary>
[Serializable, NetSerializable]
public sealed class CMUMailRequestMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUMailSendMessage(string to, string subject, string body) : BoundUserInterfaceMessage
{
    public readonly string To = to;
    public readonly string Subject = subject;
    public readonly string Body = body;
}

[Serializable, NetSerializable]
public sealed class CMUMailStateMessage(List<CMUMailMessage> inbox, List<CMUMailMessage> sent, List<string> directory)
    : BoundUserInterfaceMessage
{
    public readonly List<CMUMailMessage> Inbox = inbox;
    public readonly List<CMUMailMessage> Sent = sent;

    /// <summary>Everyone in the colony with an ID card, to address mail to.</summary>
    public readonly List<string> Directory = directory;
}

public static class CMUMailLimits
{
    public const int SubjectLength = 80;
    public const int BodyLength = 1500;
    public const int PerBox = 50;
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(10);
}

/// <summary>A note saved in the Files program of one terminal, readable by whoever uses it next.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUComputerNote
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public string Author = string.Empty;

    [DataField]
    public string Text = string.Empty;
}

[Serializable, NetSerializable]
public sealed class CMUNoteSaveMessage(string name, string text) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class CMUNoteDeleteMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

public static class CMUNoteLimits
{
    public const int Count = 12;
    public const int NameLength = 32;
    public const int TextLength = 2000;
}

/// <summary>
/// A desktop wallpaper hidden somewhere on the ExtraNet. Pages offer it with a <c>&lt;CMUNetWallpaper Id="..."/&gt;</c>
/// tag; whoever finds it can put it on their terminal's desktop.
/// </summary>
[Prototype("cmuWallpaper")]
public sealed partial class CMUWallpaperPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ResPath Path;
}

[Serializable, NetSerializable]
public sealed class CMUNetSetWallpaperMessage(ProtoId<CMUWallpaperPrototype>? wallpaper) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUWallpaperPrototype>? Wallpaper = wallpaper;
}
