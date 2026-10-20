using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>The arcade games on the ExtraNet.</summary>
[Serializable, NetSerializable]
public enum CMUArcadeGame : byte
{
    /// <summary>A runner: the hugger jumps the bullets fired at it, and a leap onto a colonist's face wins the run.</summary>
    HuggerRun,

    /// <summary>A reflex game: hit the blips on a motion tracker from the keypad, one key per sector.</summary>
    TrackerReflex,
}

[Serializable, NetSerializable]
public sealed class CMUArcadeScore(string name, int score)
{
    public readonly string Name = name;
    public readonly int Score = score;
}

/// <summary>Asks for a game's high scores; the server answers with <see cref="CMUArcadeStateMessage"/>.</summary>
[Serializable, NetSerializable]
public sealed class CMUArcadeRequestMessage(CMUArcadeGame game) : BoundUserInterfaceMessage
{
    public readonly CMUArcadeGame Game = game;
}

[Serializable, NetSerializable]
public sealed class CMUArcadeSubmitMessage(CMUArcadeGame game, int score) : BoundUserInterfaceMessage
{
    public readonly CMUArcadeGame Game = game;
    public readonly int Score = score;
}

[Serializable, NetSerializable]
public sealed class CMUArcadeStateMessage(CMUArcadeGame game, List<CMUArcadeScore> scores) : BoundUserInterfaceMessage
{
    public readonly CMUArcadeGame Game = game;
    public readonly List<CMUArcadeScore> Scores = scores;
}

public static class CMUArcadeLimits
{
    public const int Board = 10;

    /// <summary>Scores above this are not believed. Keeps the board sane, not honest.</summary>
    public const int MaxScore = 99999;
}

/// <summary>A match on the sportsbook. Seed and winner stay hidden until kickoff, when bets close.</summary>
[Serializable, NetSerializable]
public sealed class CMUBetMatch(
    int id,
    CMUSport sport,
    List<int> entrants,
    List<float> odds,
    TimeSpan kickoff,
    int seed,
    int winner,
    string? result)
{
    public readonly int Id = id;
    public readonly CMUSport Sport = sport;

    /// <summary>Indices into <see cref="CMUSports.Teams"/> or <see cref="CMUSports.Athletes"/>.</summary>
    public readonly List<int> Entrants = entrants;

    /// <summary>The payout multiplier for each entrant.</summary>
    public readonly List<float> Odds = odds;

    /// <summary>Bets close at kickoff.</summary>
    public readonly TimeSpan Kickoff = kickoff;

    public readonly int Seed = seed;

    /// <summary>The slot that wins, or -1 before kickoff.</summary>
    public readonly int Winner = winner;

    /// <summary>The final result once played, or null.</summary>
    public readonly string? Result = result;

    public TimeSpan End => Kickoff + TimeSpan.FromSeconds(CMUSports.Length(Sport));

    public string Name(int slot)
    {
        return slot >= 0 && slot < Entrants.Count ? CMUSports.Name(Sport, Entrants[slot]) : "?";
    }

    /// <summary>The same match with what the public may not know yet left out.</summary>
    public CMUBetMatch Public(TimeSpan now)
    {
        return now >= Kickoff ? this : new CMUBetMatch(Id, Sport, Entrants, Odds, Kickoff, 0, -1, Result);
    }

    public CMUBetMatch WithResult(string result)
    {
        return new CMUBetMatch(Id, Sport, Entrants, Odds, Kickoff, Seed, Winner, result);
    }
}

[Serializable, NetSerializable]
public sealed class CMUBetRequestMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUBetPlaceMessage(int match, int entrant, int amount) : BoundUserInterfaceMessage
{
    public readonly int Match = match;

    /// <summary>The slot in <see cref="CMUBetMatch.Entrants"/> the bet is on.</summary>
    public readonly int Entrant = entrant;
    public readonly int Amount = amount;
}

/// <summary>One of the logged in account's open bets.</summary>
[Serializable, NetSerializable]
public sealed class CMUBetSlip(int match, CMUSport sport, int entrant, string team, int amount)
{
    public readonly int Match = match;
    public readonly CMUSport Sport = sport;
    public readonly int Entrant = entrant;
    public readonly string Team = team;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class CMUBetStateMessage(List<CMUBetMatch> matches, List<CMUBetSlip> slips) : BoundUserInterfaceMessage
{
    /// <summary>For every sport: its live and upcoming matches, then its latest results.</summary>
    public readonly List<CMUBetMatch> Matches = matches;

    /// <summary>The logged in account's open bets.</summary>
    public readonly List<CMUBetSlip> Slips = slips;
}

public static class CMUBetLimits
{
    public const int MinBet = 10;
    public const int MaxBet = 2000;

    /// <summary>How many matches of each sport take bets at once. Every sport keeps its own schedule.</summary>
    public const int Upcoming = 1;

    /// <summary>How many played matches of each sport stay on the board to watch again.</summary>
    public const int Results = 4;

    /// <summary>The least time a new match takes bets before kickoff.</summary>
    public static readonly TimeSpan BettingWindow = TimeSpan.FromMinutes(2);

    /// <summary>The break between one match ending and the next kicking off.</summary>
    public static readonly TimeSpan Gap = TimeSpan.FromSeconds(30);
}
