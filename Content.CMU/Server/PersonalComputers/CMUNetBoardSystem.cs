using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
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
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private ColonyBankSystem _bank = default!;

    private readonly Dictionary<ProtoId<CMUNetSitePrototype>, List<CMUNetBoardPost>> _boards = new();
    private readonly Dictionary<(EntityUid Computer, EntityUid Actor), ProtoId<CMUNetSitePrototype>> _viewers = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextPost = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextComment = new();
    private readonly Dictionary<ProtoId<CMUNetSitePrototype>, CMUNetBoardHijack> _hijacks = new();
    private TimeSpan _nextHijack;
    private int _nextId = 1;

    // Who wrote each posting, which account holds each bounty, and the account behind each comment. Server only,
    // so account numbers never reach the board's readers.
    private readonly Dictionary<int, EntityUid> _posters = new();
    private readonly Dictionary<int, int> _escrow = new();
    private readonly Dictionary<CMUNetBoardComment, int> _commentAccounts = new(ReferenceEqualityComparer.Instance);

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
            subs.Event<CMUNetBoardPayoutMessage>(OnPayout);
            subs.Event<CMUNetBoardWithdrawMessage>(OnWithdraw);
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
        _posters.Clear();
        _escrow.Clear();
        _commentAccounts.Clear();
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
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Boards)
            || !TryGetReachableBoard(ent, args.Site, out var site)
            || !CanPost(args.Actor, site)
            || !CooledDown(_nextPost, args.Actor, CMUNetBoardLimits.PostCooldown))
            return;

        var title = Clean(args.Title, CMUNetBoardLimits.TitleLength, false);
        var body = Clean(args.Body, CMUNetBoardLimits.BodyLength, true);
        if (title.Length == 0 || body.Length == 0)
            return;

        // A bounty is taken from the logged in account up front, and held until it is paid out or withdrawn.
        var reward = 0;
        if (site.Board!.Bounties && args.Reward > 0 && _cfg.GetCVar(CMUPersonalComputerCVars.Bounties))
        {
            reward = Math.Min(args.Reward, CMUNetBoardLimits.MaxReward);
            if (!TryCharge(ent.Comp.SessionAccount, reward))
                return;
        }

        var (author, role) = Identify(args.Actor);
        var posts = Board(args.Site);
        var post = new CMUNetBoardPost(_nextId++, title, body, author, role, _timing.CurTime, new List<CMUNetBoardComment>(),
            ent.Comp.TerminalId, reward);
        posts.Insert(0, post);
        _posters[post.Id] = args.Actor;
        if (reward > 0)
            _escrow[post.Id] = ent.Comp.SessionAccount;

        if (posts.Count > CMUNetBoardLimits.PostsPerBoard)
            Drop(args.Site, posts[^1]);

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} posted on ExtraNet board {args.Site} with a {reward} bounty: {title}: {body}");
        Broadcast(args.Site);
    }

    private void OnComment(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardCommentMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Boards)
            || !TryGetReachableBoard(ent, args.Site, out _)
            || !CanComment(args.Actor)
            || !CooledDown(_nextComment, args.Actor, CMUNetBoardLimits.CommentCooldown))
            return;

        var body = Clean(args.Body, CMUNetBoardLimits.CommentLength, false);
        if (body.Length == 0 || FindPost(args.Site, args.Post) is not { } post)
            return;

        if (post.Comments.Count >= CMUNetBoardLimits.CommentsPerPost)
            post.Comments.RemoveAt(0);

        var (author, role) = Identify(args.Actor);
        var comment = new CMUNetBoardComment(author, role, body, _timing.CurTime, ent.Comp.TerminalId);
        post.Comments.Add(comment);
        if (ent.Comp.SessionAccount != 0)
            _commentAccounts[comment] = ent.Comp.SessionAccount;

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} commented on ExtraNet board {args.Site} posting \"{post.Title}\": {body}");
        Broadcast(args.Site);
    }

    private void OnDelete(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardDeleteMessage args)
    {
        if (!IsAdmin(args.Actor) || FindPost(args.Site, args.Post) is not { } post)
            return;

        Drop(args.Site, post);
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

    /// <summary>The poster hands the bounty to whoever wrote one of the comments, into the account they were logged in as.</summary>
    private void OnPayout(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardPayoutMessage args)
    {
        if (FindPost(args.Site, args.Post) is not { Reward: > 0 } post
            || !_posters.TryGetValue(post.Id, out var poster)
            || poster != args.Actor && !IsAdmin(args.Actor)
            || args.Comment < 0
            || args.Comment >= post.Comments.Count)
            return;

        var comment = post.Comments[args.Comment];
        if (!_commentAccounts.TryGetValue(comment, out var account) || _bank.FindAccount(account) is not { } payee)
            return;

        payee.card.AccountBalance += post.Reward;
        Dirty(payee.uid, payee.card);
        _bank.RecordTransaction(payee.uid, AtmHistoryKind.TransferIn, post.Reward, _escrow.GetValueOrDefault(post.Id));
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} paid a {post.Reward} bounty on \"{post.Title}\" to {comment.Author}");
        post.Reward = 0;
        post.PaidTo = comment.Author;
        _escrow.Remove(post.Id);
        Broadcast(args.Site);
    }

    private void OnWithdraw(Entity<CMUPersonalComputerComponent> ent, ref CMUNetBoardWithdrawMessage args)
    {
        if (FindPost(args.Site, args.Post) is not { Reward: > 0 } post
            || !_posters.TryGetValue(post.Id, out var poster)
            || poster != args.Actor && !IsAdmin(args.Actor))
            return;

        Refund(post);
        Broadcast(args.Site);
    }

    /// <summary>Takes money from an account for a bounty. Fails without a logged in account or enough money.</summary>
    private bool TryCharge(int account, int amount)
    {
        if (account == 0 || _bank.FindAccount(account) is not { } payer || payer.card.AccountBalance < amount)
            return false;

        payer.card.AccountBalance -= amount;
        Dirty(payer.uid, payer.card);
        _bank.RecordTransaction(payer.uid, AtmHistoryKind.TransferOut, amount);
        return true;
    }

    private void Refund(CMUNetBoardPost post)
    {
        if (post.Reward <= 0 || !_escrow.Remove(post.Id, out var account) || _bank.FindAccount(account) is not { } payer)
            return;

        payer.card.AccountBalance += post.Reward;
        Dirty(payer.uid, payer.card);
        _bank.RecordTransaction(payer.uid, AtmHistoryKind.TransferIn, post.Reward);
        post.Reward = 0;
    }

    /// <summary>Takes a posting off a board, giving back any bounty it still held.</summary>
    private void Drop(ProtoId<CMUNetSitePrototype> site, CMUNetBoardPost post)
    {
        Refund(post);
        Board(site).Remove(post);
        _posters.Remove(post.Id);
        foreach (var comment in post.Comments)
            _commentAccounts.Remove(comment);
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
            Drop(site, posts[^1]);

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
            GetHijack(siteId),
            Board(siteId).Where(p => _posters.GetValueOrDefault(p.Id) == actor).Select(p => p.Id).ToList());
        _ui.ServerSendUiMessage(computer, CMUPersonalComputerUiKey.Key, state, actor);
    }
}
