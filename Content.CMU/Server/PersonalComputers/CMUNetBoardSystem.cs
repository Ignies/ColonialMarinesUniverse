using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Round postings on ExtraNet board sites. Only the board's poster jobs publish; anyone at a computer that
/// reaches the site comments. Authors are taken from the speaker, never from the message, and everything
/// written is admin logged.
/// </summary>
public sealed partial class CMUNetBoardSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly Dictionary<ProtoId<CMUNetSitePrototype>, List<CMUNetBoardPost>> _boards = new();
    private readonly Dictionary<(EntityUid Computer, EntityUid Actor), ProtoId<CMUNetSitePrototype>> _viewers = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextPost = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextComment = new();
    private int _nextId = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUNetBoardRequestMessage>(OnRequest);
            subs.Event<CMUNetBoardPostMessage>(OnPost);
            subs.Event<CMUNetBoardCommentMessage>(OnComment);
            subs.Event<CMUNetBoardDeleteMessage>(OnDelete);
            subs.Event<BoundUIClosedEvent>(OnClosed);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _boards.Clear();
        _viewers.Clear();
        _nextPost.Clear();
        _nextComment.Clear();
        _nextId = 1;
    }

    private void OnClosed(Entity<CMUPersonalComputerComponent> ent, ref BoundUIClosedEvent args)
    {
        _viewers.Remove((ent.Owner, args.Actor));
    }

    private void OnRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardRequestMessage args)
    {
        if (!TryGetReachableBoard(ent, args.Site, out _))
            return;

        _viewers[(ent.Owner, args.Actor)] = args.Site;
        SendState(ent.Owner, args.Actor, args.Site);
    }

    private void OnPost(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardPostMessage args)
    {
        if (!TryGetReachableBoard(ent, args.Site, out var site)
            || !CanPost(args.Actor, site)
            || !CooledDown(_nextPost, args.Actor, CMUNetBoardLimits.PostCooldown))
            return;

        var title = Clean(args.Title, CMUNetBoardLimits.TitleLength, false);
        var body = Clean(args.Body, CMUNetBoardLimits.BodyLength, true);
        if (title.Length == 0 || body.Length == 0)
            return;

        var (author, role) = Identify(args.Actor);
        var posts = Board(args.Site);
        posts.Insert(0, new CMUNetBoardPost(_nextId++, title, body, author, role, _timing.CurTime, new List<CMUNetBoardComment>()));
        if (posts.Count > CMUNetBoardLimits.PostsPerBoard)
            posts.RemoveAt(posts.Count - 1);

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} posted on ExtraNet board {args.Site}: {title}: {body}");
        Broadcast(args.Site);
    }

    private void OnComment(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardCommentMessage args)
    {
        if (!TryGetReachableBoard(ent, args.Site, out _)
            || !CanComment(args.Actor)
            || !CooledDown(_nextComment, args.Actor, CMUNetBoardLimits.CommentCooldown))
            return;

        var body = Clean(args.Body, CMUNetBoardLimits.CommentLength, false);
        if (body.Length == 0 || FindPost(args.Site, args.Post) is not { } post)
            return;

        if (post.Comments.Count >= CMUNetBoardLimits.CommentsPerPost)
            post.Comments.RemoveAt(0);

        var (author, role) = Identify(args.Actor);
        post.Comments.Add(new CMUNetBoardComment(author, role, body, _timing.CurTime));
        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} commented on ExtraNet board {args.Site} posting \"{post.Title}\": {body}");
        Broadcast(args.Site);
    }

    private void OnDelete(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardDeleteMessage args)
    {
        if (!IsAdmin(args.Actor) || FindPost(args.Site, args.Post) is not { } post)
            return;

        Board(args.Site).Remove(post);
        _adminLog.Add(LogType.Chat, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} deleted ExtraNet board {args.Site} posting \"{post.Title}\" by {post.Author}");
        Broadcast(args.Site);
    }

    /// <summary>The board's postings this round, newest first.</summary>
    public IReadOnlyList<CMUNetBoardPost> GetPosts(ProtoId<CMUNetSitePrototype> site)
    {
        return Board(site);
    }

    private bool TryGetReachableBoard(
        Entity<CMUPersonalComputerComponent> computer,
        ProtoId<CMUNetSitePrototype> siteId,
        out CMUNetSitePrototype site)
    {
        if (!_prototype.TryIndex(siteId, out site!) || site.Board == null)
            return false;

        return CMUExtraNetSystem.CanReach(site, _net.GetNetworks(computer.Comp));
    }

    private bool CanPost(EntityUid actor, CMUNetSitePrototype site)
    {
        if (IsAdmin(actor))
            return true;

        if (!CanComment(actor) || !_minds.TryGetMind(actor, out var mindId, out _))
            return false;

        return _jobs.MindTryGetJobId(mindId, out var job)
            && job is { } jobId
            && site.Board!.Posters.Contains(jobId);
    }

    private bool CanComment(EntityUid actor)
    {
        return !HasComp<GhostComponent>(actor);
    }

    private bool IsAdmin(EntityUid actor)
    {
        return TryComp(actor, out ActorComponent? player) && _admins.IsAdmin(player.PlayerSession);
    }

    private bool CooledDown(Dictionary<EntityUid, TimeSpan> next, EntityUid actor, TimeSpan cooldown)
    {
        var now = _timing.CurTime;
        if (next.TryGetValue(actor, out var at) && now < at)
            return false;

        next[actor] = now + cooldown;
        return true;
    }

    private (string Author, string Role) Identify(EntityUid actor)
    {
        var role = string.Empty;
        if (_minds.TryGetMind(actor, out var mindId, out _))
            _jobs.MindTryGetJobName(mindId, out role);

        return (MetaData(actor).EntityName, role);
    }

    /// <summary>Trims, caps the length and, for titles and comments, folds lines into one.</summary>
    private static string Clean(string text, int max, bool multiline)
    {
        text = text.Trim();
        if (!multiline)
            text = text.Replace('\r', ' ').Replace('\n', ' ');

        return text.Length > max ? text[..max] : text;
    }

    private List<CMUNetBoardPost> Board(ProtoId<CMUNetSitePrototype> site)
    {
        if (!_boards.TryGetValue(site, out var posts))
            _boards[site] = posts = new List<CMUNetBoardPost>();

        return posts;
    }

    private CMUNetBoardPost? FindPost(ProtoId<CMUNetSitePrototype> site, int id)
    {
        foreach (var post in Board(site))
        {
            if (post.Id == id)
                return post;
        }

        return null;
    }

    private void Broadcast(ProtoId<CMUNetSitePrototype> site)
    {
        var stale = new List<(EntityUid, EntityUid)>();
        foreach (var ((computer, actor), viewing) in _viewers)
        {
            if (viewing != site)
                continue;

            if (!_ui.IsUiOpen(computer, CMUPersonalComputerUiKey.Key, actor))
            {
                stale.Add((computer, actor));
                continue;
            }

            SendState(computer, actor, site);
        }

        foreach (var key in stale)
            _viewers.Remove(key);
    }

    private void SendState(EntityUid computer, EntityUid actor, ProtoId<CMUNetSitePrototype> siteId)
    {
        if (!_prototype.TryIndex(siteId, out var site))
            return;

        var state = new CMUNetBoardStateMessage(
            siteId,
            new List<CMUNetBoardPost>(Board(siteId)),
            CanPost(actor, site),
            CanComment(actor),
            IsAdmin(actor));
        _ui.ServerSendUiMessage(computer, CMUPersonalComputerUiKey.Key, state, actor);
    }
}
