using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.Whitelist;
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
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private readonly Dictionary<ProtoId<CMUNetSitePrototype>, List<CMUNetBoardPost>> _boards = new();
    private readonly Dictionary<(EntityUid Computer, EntityUid Actor), ProtoId<CMUNetSitePrototype>> _viewers = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextPost = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextComment = new();
    private readonly Dictionary<ProtoId<CMUNetSitePrototype>, CMUNetBoardHijack> _hijacks = new();
    private TimeSpan _nextHijack;
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
            subs.Event<CMUNetBoardRestoreMessage>(OnRestore);
            subs.Event<BoundUIClosedEvent>(OnClosed);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _boards.Clear();
        _viewers.Clear();
        _nextPost.Clear();
        _nextComment.Clear();
        _hijacks.Clear();
        _nextHijack = TimeSpan.Zero;
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

    private void OnRestore(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardRestoreMessage args)
    {
        if (!TryGetReachableBoard(ent, args.Site, out var site)
            || !CanPost(args.Actor, site)
            || !_hijacks.Remove(args.Site))
            return;

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} restored ExtraNet board {args.Site} after a hijack");
        Broadcast(args.Site);
    }

    /// <summary>
    /// Posts as someone who is not at a computer, such as the deep web's unknown hackers. The text is trusted:
    /// it is still shown as plain text, but it is not cut to the player limits.
    /// </summary>
    public void PostAs(ProtoId<CMUNetSitePrototype> site, string author, string role, string title, string body)
    {
        var posts = Board(site);
        posts.Insert(0, new CMUNetBoardPost(_nextId++, title, body, author, role, _timing.CurTime, new List<CMUNetBoardComment>()));
        if (posts.Count > CMUNetBoardLimits.PostsPerBoard)
            posts.RemoveAt(posts.Count - 1);

        Broadcast(site);
    }

    /// <summary>When the hijackable boards can next be seized.</summary>
    public TimeSpan NextHijack => _nextHijack;

    /// <summary>
    /// Covers the front page of every hijackable board with propaganda for <see cref="CMUNetBoardLimits.HijackLength"/>,
    /// signed by the faction. One hijack at a time for the whole colony, then a cooldown.
    /// </summary>
    public bool TryHijack(EntityUid actor, string faction, string title, string body)
    {
        var now = _timing.CurTime;
        title = Clean(title, CMUNetBoardLimits.TitleLength, false);
        body = Clean(body, CMUNetBoardLimits.BodyLength, true);
        if (now < _nextHijack || title.Length == 0 || body.Length == 0)
            return false;

        var seized = new List<ProtoId<CMUNetSitePrototype>>();
        foreach (var site in _prototype.EnumeratePrototypes<CMUNetSitePrototype>())
        {
            if (site.Board is not { Hijackable: true })
                continue;

            _hijacks[site.ID] = new CMUNetBoardHijack(faction, title, body, now + CMUNetBoardLimits.HijackLength);
            seized.Add(site.ID);
        }

        if (seized.Count == 0)
            return false;

        _nextHijack = now + CMUNetBoardLimits.HijackCooldown;
        _adminLog.Add(LogType.Chat, LogImpact.Medium,
            $"{ToPrettyString(actor):player} hijacked the ExtraNet boards for {faction}: {title}: {body}");
        foreach (var site in seized)
            Broadcast(site);

        return true;
    }

    /// <summary>The propaganda on a board's front page, if it is still up.</summary>
    public CMUNetBoardHijack? GetHijack(ProtoId<CMUNetSitePrototype> site)
    {
        if (!_hijacks.TryGetValue(site, out var hijack))
            return null;

        if (_timing.CurTime < hijack.Until)
            return hijack;

        _hijacks.Remove(site);
        return null;
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

        return _net.CanOpen(computer, site, _net.GetNetworks(computer));
    }

    private bool CanPost(EntityUid actor, CMUNetSitePrototype site)
    {
        if (IsAdmin(actor))
            return true;

        if (!CanComment(actor))
            return false;

        if (site.Board!.AnyonePosts)
            return true;

        if (site.Board!.PosterWhitelist is { } whitelist && _whitelist.IsWhitelistPass(whitelist, actor))
            return true;

        if (!_minds.TryGetMind(actor, out var mindId, out _))
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
            IsAdmin(actor),
            GetHijack(siteId));
        _ui.ServerSendUiMessage(computer, CMUPersonalComputerUiKey.Key, state, actor);
    }
}
