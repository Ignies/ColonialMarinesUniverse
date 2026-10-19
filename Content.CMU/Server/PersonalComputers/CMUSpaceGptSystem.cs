using System.Linq;
using Content.Server.Administration.Logs;
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

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// SpaceGPT, the colony's "artificial intelligence". Every question goes to the ghosts of the round with a few
/// answers drawn from a roster; they vote, and the winner goes back to whoever asked. With no ghost voting,
/// the network picks an answer itself.
/// </summary>
public sealed partial class CMUSpaceGptSystem : EntitySystem
{
    private static readonly ProtoId<LocalizedDatasetPrototype> Answers = "CMUSpaceGptAnswers";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private sealed class Poll
    {
        public required EntityUid Computer;
        public required EntityUid Asker;
        public required string Question;
        public required List<string> Choices;
        public required TimeSpan Ends;
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
            Ends = now + CMUSpaceGptLimits.Voting,
        };
        var id = _nextPoll++;
        _polls[id] = poll;

        var ghosts = Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
        RaiseNetworkEvent(new CMUSpaceGptPollEvent(id, question, new List<string>(poll.Choices), poll.Ends), ghosts);
        _adminLog.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(args.Actor):player} asked SpaceGPT: {question}");
    }

    /// <summary>One vote per ghost per question; a ghost may change their mind until voting ends.</summary>
    private void OnVote(CMUSpaceGptVoteEvent ev, EntitySessionEventArgs args)
    {
        if (!_polls.TryGetValue(ev.Poll, out var poll)
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
            if (now < poll.Ends)
                continue;

            _polls.Remove(id);
            Answer(poll);
        }
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
