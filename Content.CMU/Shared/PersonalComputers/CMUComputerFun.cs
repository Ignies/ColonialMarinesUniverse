using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>The arcade games on the ExtraNet.</summary>
[Serializable, NetSerializable]
public enum CMUArcadeGame : byte
{
    /// <summary>A snake game: steer the hugger to the eggs without biting its own tail.</summary>
    HuggerRun,

    /// <summary>A reflex game: click the blips on a motion tracker before they fade.</summary>
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
    public const int MaxScore = 9999;
}

/// <summary>A zero-g ball match taking bets.</summary>
[Serializable, NetSerializable]
public sealed class CMUBetMatch(int id, string home, string away, float homeOdds, float awayOdds, TimeSpan kickoff, string? result)
{
    public readonly int Id = id;
    public readonly string Home = home;
    public readonly string Away = away;
    public readonly float HomeOdds = homeOdds;
    public readonly float AwayOdds = awayOdds;

    /// <summary>Bets close at kickoff.</summary>
    public readonly TimeSpan Kickoff = kickoff;

    /// <summary>The final score once played, or null.</summary>
    public readonly string? Result = result;
}

[Serializable, NetSerializable]
public sealed class CMUBetRequestMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUBetPlaceMessage(int match, bool home, int amount) : BoundUserInterfaceMessage
{
    public readonly int Match = match;
    public readonly bool Home = home;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class CMUBetStateMessage(List<CMUBetMatch> matches, List<string> slips) : BoundUserInterfaceMessage
{
    /// <summary>The open match first, then the latest results.</summary>
    public readonly List<CMUBetMatch> Matches = matches;

    /// <summary>The logged in account's bets, as lines.</summary>
    public readonly List<string> Slips = slips;
}

public static class CMUBetLimits
{
    public const int MinBet = 10;
    public const int MaxBet = 2000;
    public static readonly TimeSpan BettingWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MatchLength = TimeSpan.FromMinutes(2);
}
