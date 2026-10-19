using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// The break room on the ExtraNet: arcade high score boards for the round, and a zero-g ball sportsbook where
/// colonists bet ATM credits on matches played every few minutes. A new arcade record makes the Herald wire.
/// </summary>
public sealed partial class CMUComputerFunSystem : EntitySystem
{
    private static readonly ProtoId<CMUNetSitePrototype> Herald = "CMUNetSiteColonyHerald";
    private static readonly string[] Teams =
    {
        "Gateway Comets", "Titan Dockers", "Hadley Hawks", "Thedus Tritons", "Sol Strikers", "Rim Runners",
        "Arcturus Aces", "Fiorina Furies",
    };

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private CMUNetBoardSystem _boards = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private sealed record Bet(int Account, string Name, int Match, bool Home, int Amount);

    private readonly Dictionary<CMUArcadeGame, List<CMUArcadeScore>> _scores = new();
    private readonly List<CMUBetMatch> _matches = new();
    private readonly List<Bet> _bets = new();
    private int _nextMatch = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _scores.Clear();
            _matches.Clear();
            _bets.Clear();
            _nextMatch = 1;
        });
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUArcadeRequestMessage>(OnArcadeRequest);
            subs.Event<CMUArcadeSubmitMessage>(OnArcadeSubmit);
            subs.Event<CMUBetRequestMessage>(OnBetRequest);
            subs.Event<CMUBetPlaceMessage>(OnBetPlace);
        });
    }

    private bool On(CVarDef<bool> feature)
    {
        return _cfg.GetCVar(CMUPersonalComputerCVars.Enabled) && _cfg.GetCVar(feature);
    }

    public List<CMUArcadeScore> Scores(CMUArcadeGame game)
    {
        if (!_scores.TryGetValue(game, out var list))
            _scores[game] = list = new List<CMUArcadeScore>();

        return list;
    }

    private void OnArcadeRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUArcadeRequestMessage args)
    {
        if (!On(CMUPersonalComputerCVars.Arcade))
            return;

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key,
            new CMUArcadeStateMessage(args.Game, new List<CMUArcadeScore>(Scores(args.Game))), args.Actor);
    }

    /// <summary>Scores go on the board under the session's name, or the player's if nobody is logged in.</summary>
    private void OnArcadeSubmit(Entity<CMUPersonalComputerComponent> ent, ref CMUArcadeSubmitMessage args)
    {
        if (!On(CMUPersonalComputerCVars.Arcade) || args.Score <= 0 || args.Score > CMUArcadeLimits.MaxScore)
            return;

        var name = ent.Comp.SessionUser ?? Name(args.Actor);
        var scores = Scores(args.Game);
        var record = scores.Count == 0 || args.Score > scores[0].Score;
        scores.Add(new CMUArcadeScore(name, args.Score));
        scores.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (scores.Count > CMUArcadeLimits.Board)
            scores.RemoveRange(CMUArcadeLimits.Board, scores.Count - CMUArcadeLimits.Board);

        if (record && On(CMUPersonalComputerCVars.Wire))
            _boards.PostAs(Herald, Loc.GetString("cmu-net-events-wire-author"), Loc.GetString("cmu-net-events-role"),
                Loc.GetString("cmu-arcade-record-title"),
                Loc.GetString("cmu-arcade-record-body", ("name", name), ("score", args.Score), ("game", Loc.GetString($"cmu-arcade-{args.Game}"))));

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key,
            new CMUArcadeStateMessage(args.Game, new List<CMUArcadeScore>(scores)), args.Actor);
    }

    private void OnBetRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUBetRequestMessage args)
    {
        if (!On(CMUPersonalComputerCVars.Betting))
            return;

        SendBets(ent, args.Actor);
    }

    /// <summary>Bets come out of the logged in account and only before kickoff.</summary>
    private void OnBetPlace(Entity<CMUPersonalComputerComponent> ent, ref CMUBetPlaceMessage args)
    {
        var matchId = args.Match;
        if (!On(CMUPersonalComputerCVars.Betting)
            || args.Amount < CMUBetLimits.MinBet
            || args.Amount > CMUBetLimits.MaxBet
            || ent.Comp.SessionAccount == 0
            || ent.Comp.SessionUser is not { } user
            || _matches.FirstOrDefault(m => m.Id == matchId) is not { Result: null } match
            || _timing.CurTime >= match.Kickoff
            || _bank.FindAccount(ent.Comp.SessionAccount) is not { } account
            || account.card.AccountBalance < args.Amount)
            return;

        account.card.AccountBalance -= args.Amount;
        Dirty(account.uid, account.card);
        _bank.RecordTransaction(account.uid, AtmHistoryKind.Purchase, args.Amount);
        _bets.Add(new Bet(ent.Comp.SessionAccount, user, match.Id, args.Home, args.Amount));
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} bet {args.Amount} on {(args.Home ? match.Home : match.Away)} as {user}");
        SendBets(ent, args.Actor);
    }

    private void SendBets(Entity<CMUPersonalComputerComponent> ent, EntityUid actor)
    {
        var slips = new List<string>();
        foreach (var bet in _bets.Where(b => b.Account == ent.Comp.SessionAccount && b.Account != 0))
        {
            var match = _matches.FirstOrDefault(m => m.Id == bet.Match);
            if (match != null)
                slips.Add(Loc.GetString("cmu-bet-slip", ("amount", bet.Amount), ("team", bet.Home ? match.Home : match.Away)));
        }

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key,
            new CMUBetStateMessage(_matches.TakeLast(6).Reverse().ToList(), slips), actor);
    }

    /// <summary>Schedules the next match when none is open, and plays matches whose time is up.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!On(CMUPersonalComputerCVars.Betting))
            return;

        var now = _timing.CurTime;
        if (_matches.All(m => m.Result != null))
            Schedule(now);

        for (var i = 0; i < _matches.Count; i++)
        {
            var match = _matches[i];
            if (match.Result != null || now < match.Kickoff + CMUBetLimits.MatchLength)
                continue;

            _matches[i] = Play(match);
        }
    }

    private void Schedule(TimeSpan now)
    {
        var teams = Teams.ToList();
        _random.Shuffle(teams);
        var homeOdds = (float) Math.Round(1.3 + _random.NextDouble() * 2, 1);
        var awayOdds = (float) Math.Round(1.3 + _random.NextDouble() * 2, 1);
        _matches.Add(new CMUBetMatch(_nextMatch++, teams[0], teams[1], homeOdds, awayOdds, now + CMUBetLimits.BettingWindow, null));
        if (_matches.Count > 12)
            _matches.RemoveAt(0);
    }

    /// <summary>The longer the odds, the less likely the win; winners get their stake times the odds.</summary>
    private CMUBetMatch Play(CMUBetMatch match)
    {
        var homeChance = (1 / match.HomeOdds) / (1 / match.HomeOdds + 1 / match.AwayOdds);
        var homeWins = _random.Prob(homeChance);
        var winnerGoals = _random.Next(1, 6);
        var loserGoals = _random.Next(0, winnerGoals);
        var result = homeWins ? $"{winnerGoals} - {loserGoals}" : $"{loserGoals} - {winnerGoals}";

        foreach (var bet in _bets.Where(b => b.Match == match.Id && b.Home == homeWins).ToList())
        {
            if (_bank.FindAccount(bet.Account) is not { } account)
                continue;

            var payout = (int) (bet.Amount * (homeWins ? match.HomeOdds : match.AwayOdds));
            account.card.AccountBalance += payout;
            Dirty(account.uid, account.card);
            _bank.RecordTransaction(account.uid, AtmHistoryKind.TransferIn, payout);
        }

        _bets.RemoveAll(b => b.Match == match.Id);
        return new CMUBetMatch(match.Id, match.Home, match.Away, match.HomeOdds, match.AwayOdds, match.Kickoff, result);
    }
}
