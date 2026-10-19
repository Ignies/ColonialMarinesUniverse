using Content.Shared.Roles;
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

    /// <summary>Label of the publish button and composer, e.g. "File a report".</summary>
    [DataField(required: true)]
    public LocId PostLabel;

    /// <summary>Shown when there are no postings yet.</summary>
    [DataField(required: true)]
    public LocId Empty;
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
    List<CMUNetBoardComment> comments,
    int? photo = null)
{
    public readonly int Id = id;
    public readonly string Title = title;
    public readonly string Body = body;
    public readonly string Author = author;
    public readonly string Role = role;
    public readonly TimeSpan Time = time;
    public readonly List<CMUNetBoardComment> Comments = comments;

    /// <summary>A photo print's id, shown above the body.</summary>
    public readonly int? Photo = photo;
}

/// <summary>Asks the server for a board's postings; it answers with <see cref="CMUNetBoardStateMessage"/>.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardRequestMessage(ProtoId<CMUNetSitePrototype> site) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
}

/// <summary>Publishes a posting; with <see cref="AttachPhoto"/> the photo print the poster holds goes with it.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardPostMessage(ProtoId<CMUNetSitePrototype> site, string title, string body, bool attachPhoto = false)
    : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUNetSitePrototype> Site = site;
    public readonly string Title = title;
    public readonly string Body = body;
    public readonly bool AttachPhoto = attachPhoto;
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

/// <summary>A board's postings, newest first, and what the viewer may do there.</summary>
[Serializable, NetSerializable]
public sealed class CMUNetBoardStateMessage(
    ProtoId<CMUNetSitePrototype> site,
    List<CMUNetBoardPost> posts,
    bool canPost,
    bool canComment,
    bool canModerate) : BoundUserInterfaceMessage
{
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
}
