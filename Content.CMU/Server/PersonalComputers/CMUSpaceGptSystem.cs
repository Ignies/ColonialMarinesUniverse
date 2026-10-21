using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// SpaceGPT, the colony's "artificial intelligence". Every question goes to the ghosts of the round with a few
/// answers drawn from a roster; they vote, and the winner goes back to whoever asked. With no ghost voting,
/// the network picks an answer itself. Admins see each question first, in admin chat, and have a few seconds before
/// the ghosts do: they can follow the terminal, hold the question, rewrite its answers, or deny it, which answers with
/// SpaceGPT's safeties. While a question is held, the asker's screen shows SpaceGPT pretending to think.
/// </summary>
public sealed partial class CMUSpaceGptSystem : EntitySystem
{
    private static readonly ProtoId<LocalizedDatasetPrototype> Answers = "CMUSpaceGptAnswers";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private enum PollState : byte
    {
        /// <summary>Admins have a moment with it before the ghosts get it.</summary>
        Waiting,

        /// <summary>With the ghosts, being voted on.</summary>
        Voting,

        /// <summary>An admin is holding it back.</summary>
        Held,
    }

    private sealed class Poll
    {
        public required EntityUid Computer;
        public required EntityUid Asker;
        public required string Question;
        public required List<string> Choices;

        /// <summary>When it goes to the ghosts while waiting; when voting ends while voting.</summary>
        public required TimeSpan Ends;
        public PollState State;

        /// <summary>Voting time left when held during the vote; zero if held before the ghosts saw it.</summary>
        public TimeSpan Left;
        public readonly Dictionary<NetUserId, int> Votes = new();
    }

    private readonly Dictionary<int, Poll> _polls = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextAsk = new();
    private int _nextPoll = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _polls.Clear();
            _nextAsk.Clear();
        });
        SubscribeNetworkEvent<CMUSpaceGptVoteEvent>(OnVote);
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUSpaceGptAskMessage>(OnAsk);
        });
    }

    /// <summary>Questions still waiting for the dead to vote.</summary>
    public int OpenPolls => _polls.Count;

    /// <summary>The most recent question's id.</summary>
    public int LastPoll => _nextPoll - 1;

    private void OnAsk(Entity<CMUPersonalComputerComponent> ent, ref CMUSpaceGptAskMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Enabled) || !_cfg.GetCVar(CMUSpaceGptCVars.Enabled))
            return;

        var now = _timing.CurTime;
        if (_nextAsk.TryGetValue(args.Actor, out var ready) && now < ready)
            return;

        var question = args.Question.Trim().Replace('\n', ' ');
        if (question.Length == 0)
            return;

        if (question.Length > CMUSpaceGptLimits.QuestionLength)
            question = question[..CMUSpaceGptLimits.QuestionLength];

        _nextAsk[args.Actor] = now + CMUSpaceGptLimits.Cooldown;
        var roster = _prototype.Index(Answers).Values.Select(id => Loc.GetString(id)).ToList();
        _random.Shuffle(roster);
        var poll = new Poll
        {
            Computer = ent,
            Asker = args.Actor,
            Question = question,
            Choices = roster.Take(CMUSpaceGptLimits.Choices).ToList(),
            Ends = now + CMUSpaceGptLimits.AdminDelay,
            State = PollState.Waiting,
        };
        var id = _nextPoll++;
        _polls[id] = poll;

        _adminLog.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(args.Actor):player} asked SpaceGPT: {question}");
        var links = string.Join(" ",
            Link("cmu-spacegpt-admin-options", $"spacegpt options {id}"),
            Link("cmu-spacegpt-admin-follow", $"tpto {GetNetEntity(ent)}"),
            Link("cmu-spacegpt-admin-hold", $"spacegpt hold {id}"),
            Link("cmu-spacegpt-admin-deny", $"spacegpt deny {id}"));
        ToAdmins(Loc.GetString("cmu-spacegpt-admin-asked",
            ("name", FormattedMessage.EscapeText(Name(args.Actor))),
            ("terminal", ent.Comp.TerminalId),
            ("question", FormattedMessage.EscapeText(question)),
            ("links", links)));
    }

    /// <summary>One vote per ghost per question; a ghost may change their mind until voting ends.</summary>
    private void OnVote(CMUSpaceGptVoteEvent ev, EntitySessionEventArgs args)
    {
        if (!_polls.TryGetValue(ev.Poll, out var poll)
            || poll.State != PollState.Voting
            || ev.Answer < 0
            || ev.Answer >= poll.Choices.Count
            || args.SenderSession.AttachedEntity is not { } ghost
            || !HasComp<GhostComponent>(ghost))
            return;

        poll.Votes[args.SenderSession.UserId] = ev.Answer;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_polls.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (id, poll) in _polls.ToList())
        {
            if (poll.State == PollState.Held || now < poll.Ends)
                continue;

            if (poll.State == PollState.Waiting)
            {
                StartVoting(id, poll);
                continue;
            }

            _polls.Remove(id);
            RaiseNetworkEvent(new CMUSpaceGptCancelEvent(id), Ghosts());
            Answer(poll);
        }
    }

    private Filter Ghosts()
    {
        return Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
    }

    /// <summary>Hands the question to the ghosts, for the time left on it.</summary>
    private void StartVoting(int id, Poll poll, TimeSpan? left = null)
    {
        poll.State = PollState.Voting;
        poll.Ends = _timing.CurTime + (left ?? CMUSpaceGptLimits.Voting);
        RaiseNetworkEvent(new CMUSpaceGptPollEvent(id, poll.Question, new List<string>(poll.Choices), poll.Ends, GetNetEntity(poll.Computer)), Ghosts());
    }

    /// <summary>The question and its answers, for an admin rewriting them.</summary>
    public bool TryGetPoll(int id, out string asker, out string question, out List<string> options)
    {
        asker = question = string.Empty;
        options = new List<string>();
        if (!_polls.TryGetValue(id, out var poll))
            return false;

        asker = Exists(poll.Asker) ? Name(poll.Asker) : "?";
        question = poll.Question;
        options = new List<string>(poll.Choices);
        return true;
    }

    /// <summary>Holds a question back from the ghosts, or freezes its vote. Its asker sees SpaceGPT stall.</summary>
    public bool Hold(int id, string admin)
    {
        if (!_polls.TryGetValue(id, out var poll) || poll.State == PollState.Held)
            return false;

        if (poll.State == PollState.Voting)
        {
            poll.Left = poll.Ends - _timing.CurTime;
            RaiseNetworkEvent(new CMUSpaceGptCancelEvent(id), Ghosts());
        }
        else
        {
            poll.Left = TimeSpan.Zero;
        }

        poll.State = PollState.Held;
        TellAsker(poll, true);
        ToAdmins(Loc.GetString("cmu-spacegpt-admin-held", ("admin", FormattedMessage.EscapeText(admin)), ("id", id),
            ("links", Link("cmu-spacegpt-admin-release", $"spacegpt release {id}") + " " + Link("cmu-spacegpt-admin-deny", $"spacegpt deny {id}"))));
        return true;
    }

    /// <summary>Lets a held question go to the ghosts, with what was left of its vote.</summary>
    public bool Release(int id, string admin)
    {
        if (!_polls.TryGetValue(id, out var poll) || poll.State != PollState.Held)
            return false;

        TellAsker(poll, false);
        StartVoting(id, poll, poll.Left > TimeSpan.Zero ? poll.Left : null);
        ToAdmins(Loc.GetString("cmu-spacegpt-admin-released", ("admin", FormattedMessage.EscapeText(admin)), ("id", id)));
        return true;
    }

    /// <summary>Gives the question new answers for the ghosts to choose from, then lets it go.</summary>
    public bool SetOptions(int id, List<string> options, string admin)
    {
        if (!_polls.TryGetValue(id, out var poll))
            return false;

        var cleaned = options
            .Select(o => o.Trim().Replace('\n', ' '))
            .Where(o => o.Length > 0)
            .Select(o => o.Length > CMUSpaceGptLimits.AnswerLength ? o[..CMUSpaceGptLimits.AnswerLength] : o)
            .Take(CMUSpaceGptLimits.Choices)
            .ToList();
        if (cleaned.Count == 0)
            return false;

        poll.Choices = cleaned;
        poll.Votes.Clear();
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"{admin} set the SpaceGPT answers to \"{poll.Question}\": {string.Join(" | ", cleaned)}");
        if (poll.State == PollState.Held)
            Release(id, admin);
        else if (poll.State == PollState.Voting)
            StartVoting(id, poll, poll.Ends - _timing.CurTime);

        return true;
    }

    /// <summary>Answers with SpaceGPT's safeties instead of the ghosts.</summary>
    public bool Deny(int id, string admin)
    {
        if (!_polls.Remove(id, out var poll))
            return false;

        RaiseNetworkEvent(new CMUSpaceGptCancelEvent(id), Ghosts());
        TellAsker(poll, false);
        if (Exists(poll.Computer))
        {
            _ui.ServerSendUiMessage(poll.Computer, CMUPersonalComputerUiKey.Key,
                new CMUSpaceGptAnswerMessage(poll.Question, Loc.GetString("cmu-spacegpt-denied"), -1), poll.Asker);
        }

        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"{admin} denied the SpaceGPT question \"{poll.Question}\"");
        ToAdmins(Loc.GetString("cmu-spacegpt-admin-denied", ("admin", FormattedMessage.EscapeText(admin)), ("id", id)));
        return true;
    }

    private void TellAsker(Poll poll, bool held)
    {
        if (Exists(poll.Computer))
            _ui.ServerSendUiMessage(poll.Computer, CMUPersonalComputerUiKey.Key, new CMUSpaceGptHoldMessage(held), poll.Asker);
    }

    private string Link(string label, string command)
    {
        return $"[cmdlink=\"{FormattedMessage.EscapeText(Loc.GetString(label))}\" command=\"{command}\"/]";
    }

    /// <summary>To admin chat, with its command links working: the usual announcement escapes them.</summary>
    private void ToAdmins(string message)
    {
        var wrapped = Loc.GetString("chat-manager-send-admin-announcement-wrap-message",
            ("adminChannelName", Loc.GetString("chat-manager-admin-channel-name")), ("message", message));
        var clients = _admins.ActiveAdmins.Select(p => p.Channel);
        _chat.ChatMessageToMany(ChatChannel.Admin, message, wrapped, default, false, true, clients);
    }

    private void Answer(Poll poll)
    {
        var tally = new int[poll.Choices.Count];
        foreach (var vote in poll.Votes.Values)
            tally[vote]++;

        var best = tally.Max();
        var winners = Enumerable.Range(0, tally.Length).Where(i => tally[i] == best).ToList();
        var answer = poll.Choices[_random.Pick(winners)];
        if (!Exists(poll.Computer))
            return;

        _ui.ServerSendUiMessage(poll.Computer, CMUPersonalComputerUiKey.Key,
            new CMUSpaceGptAnswerMessage(poll.Question, answer, best), poll.Asker);
    }
}
