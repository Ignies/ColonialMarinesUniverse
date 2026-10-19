using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// Makes an ExtraNet site a board: key roles publish postings during the round, anyone who reaches the site
/// comments on them. Postings live for the round only.
/// </summary>
[DataDefinition]
public sealed partial class CMUNetBoardDefinition
{
    /// <summary>Jobs allowed to publish postings.</summary>
    [DataField]
    public HashSet<ProtoId<JobPrototype>> Posters = new();

    /// <summary>Anyone matching this may also publish, e.g. every CLF member whatever their job.</summary>
    [DataField]
    public EntityWhitelist? PosterWhitelist;

    /// <summary>Label of the publish button and composer, e.g. "File a report".</summary>
    [DataField(required: true)]
    public LocId PostLabel;

    /// <summary>Shown when there are no postings yet.</summary>
    [DataField(required: true)]
    public LocId Empty;

    /// <summary>A forum: anyone at a computer that reaches the site may post, not only the poster jobs.</summary>
    [DataField]
    public bool AnyonePosts;

    /// <summary>Unknown hackers dump stolen ATM accounts and wanted lists here at random.</summary>
    [DataField]
    public bool Leaks;

    /// <summary>Insurgents on a hacked computer can seize the board's front page for a while.</summary>
    [DataField]
    public bool Hijackable;
}

/// <summary>Propaganda covering a board's front page until <see cref="Until"/>, or until a poster restores it.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardHijack(string faction, string title, string body, TimeSpan until)
{
    public readonly string Faction = faction;
    public readonly string Title = title;
    public readonly string Body = body;
    public readonly TimeSpan Until = until;
}

[Serializable, NetSerializable]
public sealed class CMUNetBoardComment(string author, string role, string body, TimeSpan time)
{
    public readonly string Author = author;
    public readonly string Role = role;
    public readonly string Body = body;
    public readonly TimeSpan Time = time;
}

[Serializable, NetSerializable]
public sealed class CMUNetBoardPost(
    int id,
    string title,
    string body,
    string author,
    string role,
    TimeSpan time,
    List<CMUNetBoardComment> comments)
{
    public readonly int Id = id;
    public readonly string Title = title;
    public readonly string Body = body;
    public readonly string Author = author;
    public readonly string Role = role;
    public readonly TimeSpan Time = time;
    public readonly List<CMUNetBoardComment> Comments = comments;
}

/// <summary>Asks the server for a board's postings; it answers with <see cref="CMUNetBoardStateMessage"/>.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardRequestMessage(ProtoId<CMUNetSitePrototype> site) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
}

[Serializable, NetSerializable]
public sealed class CMUNetBoardPostMessage(ProtoId<CMUNetSitePrototype> site, string title, string body)
    : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
    public readonly string Title = title;
    public readonly string Body = body;
}

[Serializable, NetSerializable]
public sealed class CMUNetBoardCommentMessage(ProtoId<CMUNetSitePrototype> site, int post, string body)
    : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
    public readonly int Post = post;
    public readonly string Body = body;
}

/// <summary>Admin removal of a posting.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardDeleteMessage(ProtoId<CMUNetSitePrototype> site, int post) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
    public readonly int Post = post;
}

/// <summary>A board poster takes the front page back from propaganda.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardRestoreMessage(ProtoId<CMUNetSitePrototype> site) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
}

/// <summary>A board's postings, newest first, and what the viewer may do there.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardStateMessage(
    ProtoId<CMUNetSitePrototype> site,
    List<CMUNetBoardPost> posts,
    bool canPost,
    bool canComment,
    bool canModerate,
    CMUNetBoardHijack? hijack = null) : BoundUserInterfaceMessage
{
    public readonly CMUNetBoardHijack? Hijack = hijack;
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
    public readonly List<CMUNetBoardPost> Posts = posts;
    public readonly bool CanPost = canPost;
    public readonly bool CanComment = canComment;
    public readonly bool CanModerate = canModerate;
}

public static class CMUNetBoardLimits
{
    public const int TitleLength = 80;
    public const int BodyLength = 2000;
    public const int CommentLength = 300;
    public const int PostsPerBoard = 40;
    public const int CommentsPerPost = 60;
    public static readonly TimeSpan PostCooldown = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CommentCooldown = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan HijackLength = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan HijackCooldown = TimeSpan.FromMinutes(12);
}
