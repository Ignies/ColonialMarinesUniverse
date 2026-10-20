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
/// The break room on the ExtraNet: arcade high score boards for the round, and a sportsbook where colonists bet
/// ATM credits on sprints, boxing, baseball and zero-g ball. Every sport runs its own schedule, a match every few
/// minutes, so each has its own tab on the page. A new arcade record makes the Herald wire.
/// </summary>
public sealed partial class CMUComputerFunSystem : EntitySystem
{
    private static readonly ProtoId<CMUNetSitePrototype> Herald = "CMUNetSiteColonyHerald";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private CMUNetBoardSystem _boards = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private sealed record Bet(int Account, string Name, int Match, int Entrant, int Amount);

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
            || args.Entrant < 0
            || args.Entrant >= match.Entrants.Count
            || _timing.CurTime >= match.Kickoff
            || _bank.FindAccount(ent.Comp.SessionAccount) is not { } account
            || account.card.AccountBalance < args.Amount)
            return;

        account.card.AccountBalance -= args.Amount;
        Dirty(account.uid, account.card);
        _bank.RecordTransaction(account.uid, AtmHistoryKind.Purchase, args.Amount);
        _bets.Add(new Bet(ent.Comp.SessionAccount, user, match.Id, args.Entrant, args.Amount));
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} bet {args.Amount} on {match.Name(args.Entrant)} as {user}");
        SendBets(ent, args.Actor);
    }

    private void SendBets(Entity<CMUPersonalComputerComponent> ent, EntityUid actor)
    {
        var slips = new List<CMUBetSlip>();
        foreach (var bet in _bets.Where(b => b.Account == ent.Comp.SessionAccount && b.Account != 0))
        {
            var match = _matches.FirstOrDefault(m => m.Id == bet.Match);
            if (match != null)
                slips.Add(new CMUBetSlip(match.Id, match.Sport, bet.Entrant, match.Name(bet.Entrant), bet.Amount));
        }

        var now = _timing.CurTime;
        var shown = new List<CMUBetMatch>();
        foreach (var sport in Enum.GetValues<CMUSport>())
        {
            var played = _matches.Where(m => m.Sport == sport && m.Result != null).TakeLast(CMUBetLimits.Results).Reverse();
            shown.AddRange(_matches.Where(m => m.Sport == sport && m.Result == null).Concat(played).Select(m => m.Public(now)));
        }
        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key, new CMUBetStateMessage(shown, slips), actor);
    }

    /// <summary>Keeps every sport taking bets on its next match, reveals each at kickoff and settles it when it ends.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!On(CMUPersonalComputerCVars.Betting))
            return;

        var now = _timing.CurTime;
        foreach (var sport in Enum.GetValues<CMUSport>())
        {
            while (_matches.Count(m => m.Sport == sport && m.Kickoff > now) < CMUBetLimits.Upcoming)
                Schedule(sport, now);
        }

        for (var i = 0; i < _matches.Count; i++)
        {
            var match = _matches[i];
            if (match.Result != null || now < match.End)
                continue;

            _matches[i] = Play(match);
        }
    }

    /// <summary>
    /// Kicks off after the sport's last scheduled match ends. Odds come from each entrant's chance with a house cut,
    /// and the winner is drawn from those chances now.
    /// </summary>
    private void Schedule(CMUSport sport, TimeSpan now)
    {
        var last = _matches.LastOrDefault(m => m.Sport == sport);
        var kickoff = now + CMUBetLimits.BettingWindow;
        if (last != null && last.End + CMUBetLimits.Gap > kickoff)
            kickoff = last.End + CMUBetLimits.Gap;

        var roster = Enumerable.Range(0, CMUSports.UsesTeams(sport) ? CMUSports.Teams.Length : CMUSports.Athletes.Length).ToList();
        _random.Shuffle(roster);
        var entrants = roster.Take(CMUSports.Entrants(sport)).ToList();

        var weights = entrants.Select(_ => 0.5f + _random.NextFloat() * 1.5f).ToList();
        var total = weights.Sum();
        var odds = weights.Select(w => (float) Math.Max(1.1, Math.Round(total / w * 0.9, 1))).ToList();
        var roll = _random.NextFloat() * total;
        var winner = 0;
        for (; winner < weights.Count - 1; winner++)
        {
            roll -= weights[winner];
            if (roll < 0)
                break;
        }

        _matches.Add(new CMUBetMatch(_nextMatch++, sport, entrants, odds, kickoff, _random.Next(), winner, null));

        // played matches past the board fall off; open ones still hold bets
        var played = _matches.Where(m => m.Sport == sport && m.Result != null).ToList();
        for (var i = 0; i < played.Count - CMUBetLimits.Results; i++)
            _matches.Remove(played[i]);
    }

    public IReadOnlyList<CMUBetMatch> Matches => _matches;

    /// <summary>Plays a match right away and settles its bets, for admins and tests.</summary>
    public void PlayNow(int id)
    {
        var index = _matches.FindIndex(m => m.Id == id && m.Result == null);
        if (index >= 0)
            _matches[index] = Play(_matches[index]);
    }

    /// <summary>Winners get their stake times the odds; everyone else already paid.</summary>
    private CMUBetMatch Play(CMUBetMatch match)
    {
        var play = CMUSports.Play(match.Sport, match.Seed, match.Entrants.Count, match.Winner);
        foreach (var bet in _bets.Where(b => b.Match == match.Id && b.Entrant == match.Winner).ToList())
        {
            if (_bank.FindAccount(bet.Account) is not { } account)
                continue;

            var payout = (int) (bet.Amount * match.Odds[match.Winner]);
            account.card.AccountBalance += payout;
            Dirty(account.uid, account.card);
            _bank.RecordTransaction(account.uid, AtmHistoryKind.TransferIn, payout);
        }

        _bets.RemoveAll(b => b.Match == match.Id);
        return match.WithResult(play.Result);
    }
}
