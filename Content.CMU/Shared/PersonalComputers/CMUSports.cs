using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>The sports the ExtraNet sportsbook takes bets on.</summary>
[Serializable, NetSerializable]
public enum CMUSport : byte
{
    /// <summary>A 100 metre dash, five lanes.</summary>
    Sprint,

    /// <summary>Three rounds, red corner against blue corner.</summary>
    Boxing,

    /// <summary>Six innings, home against away.</summary>
    Baseball,

    /// <summary>Three a side in a weightless cage, first to put the ball in the other goal.</summary>
    ZeroGBall,
}

public enum CMUSportsAction : byte
{
    /// <summary>A runner crosses the line. Value: the time in hundredths of a second.</summary>
    Finish,

    /// <summary>A punch. Value: 1 if it landed.</summary>
    Punch,

    /// <summary>Side goes down and gets back up.</summary>
    Knockdown,

    /// <summary>Side goes down and stays down.</summary>
    Knockout,

    /// <summary>A round or half inning starts. Value: its number. Side: who bats, for baseball.</summary>
    Bell,

    /// <summary>A plate appearance. Value: 0 out, 1 to 3 bases, 4 a home run. Extra: runs scored. State: bases | outs &lt;&lt; 4.</summary>
    AtBat,

    /// <summary>The ball goes to a player. Value: the player.</summary>
    Pass,

    /// <summary>Side scores. Value: the player who shot.</summary>
    Goal,
}

/// <summary>Something that happens during a match, in seconds from kickoff.</summary>
public readonly record struct CMUSportsEvent(float Time, CMUSportsAction Action, int Side, int Value, int Extra = 0, int State = 0);

/// <summary>A whole match, worked out in advance from a seed and the winner.</summary>
public sealed class CMUSportsPlay
{
    public readonly List<CMUSportsEvent> Events = new();
    public int[] Score = Array.Empty<int>();
    public string Result = string.Empty;
}

/// <summary>
/// The leagues: who plays, what they wear and how a match goes. Matches are deterministic from their seed and
/// winner, so the server settles bets on the same match every client watches.
/// </summary>
public static class CMUSports
{
    public const float SprintStart = 8;
    public const int BoxingRounds = 3;
    public const float RoundLength = 24;
    public const float RoundRest = 6;
    public const int Innings = 6;
    public const int Squad = 3;

    /// <summary>Kit colours. A team or lane wears the jumpsuit and shoes of its colour.</summary>
    public static readonly string[] Colours = { "Red", "Blue", "Green", "Yellow", "Orange", "Purple", "White", "Brown" };

    /// <summary>Teams, each in the colour at the same index.</summary>
    public static readonly string[] Teams =
    {
        "Gateway Comets", "Titan Dockers", "Hadley Hawks", "Thedus Tritons", "Sol Strikers", "Rim Runners",
        "Arcturus Aces", "Fiorina Furies",
    };

    public static readonly string[] Athletes =
    {
        "Dex Mbeki", "Ivo Laszlo", "Rana Holt", "Tomas Reyes", "Kit Okonkwo", "June Sato", "Marco Bell",
        "Ada Novak", "Sam Ferreira", "Lena Dubois", "Pike Hanlon", "Oni Adeyemi", "Viktor Saar", "Bea Quinlan",
    };

    public static bool UsesTeams(CMUSport sport)
    {
        return sport is CMUSport.Baseball or CMUSport.ZeroGBall;
    }

    public static int Entrants(CMUSport sport)
    {
        return sport == CMUSport.Sprint ? 5 : 2;
    }

    /// <summary>How long a match runs from kickoff, in seconds.</summary>
    public static float Length(CMUSport sport)
    {
        return sport switch
        {
            CMUSport.Sprint => 30,
            CMUSport.Boxing => BoxingRounds * (RoundLength + RoundRest) + 4,
            _ => 120,
        };
    }

    public static string Name(CMUSport sport, int entrant)
    {
        var names = UsesTeams(sport) ? Teams : Athletes;
        return entrant >= 0 && entrant < names.Length ? names[entrant] : "?";
    }

    /// <summary>The colour an entrant wears: a team's own, a sprinter's lane, a boxer's corner.</summary>
    public static int Colour(CMUSport sport, int slot, int entrant)
    {
        return sport switch
        {
            CMUSport.Boxing => slot == 0 ? 0 : 1,
            CMUSport.Sprint => slot % Colours.Length,
            _ => entrant % Colours.Length,
        };
    }

    /// <summary>The starting gear for a kit, like CMUSportsKitRunnerRed. Roles: Runner, Boxer, Player, Spacer.</summary>
    public static string Kit(string role, int colour)
    {
        return $"CMUSportsKit{role}{Colours[colour % Colours.Length]}";
    }

    public static CMUSportsPlay Play(CMUSport sport, int seed, int entrants, int winner)
    {
        var random = new System.Random(seed);
        var play = sport switch
        {
            CMUSport.Sprint => Sprint(random, entrants, winner),
            CMUSport.Boxing => Boxing(random, winner),
            CMUSport.Baseball => Baseball(random, winner),
            _ => ZeroG(random, winner),
        };
        play.Events.Sort((a, b) => a.Time.CompareTo(b.Time));
        return play;
    }

    private static float Range(System.Random random, double min, double max)
    {
        return (float) (min + random.NextDouble() * (max - min));
    }

    private static CMUSportsPlay Sprint(System.Random random, int lanes, int winner)
    {
        var play = new CMUSportsPlay { Score = new int[lanes] };
        var best = (int) Math.Round(Range(random, 9.6, 10.4) * 100);
        for (var lane = 0; lane < lanes; lane++)
        {
            var time = lane == winner ? best : best + 5 + random.Next(110);
            play.Score[lane] = time;
            play.Events.Add(new CMUSportsEvent(SprintStart + time / 100f, CMUSportsAction.Finish, lane, time));
        }

        play.Result = $"{best / 100f:0.00}s";
        return play;
    }

    private static CMUSportsPlay Boxing(System.Random random, int winner)
    {
        var play = new CMUSportsPlay { Score = new int[2] };
        var loser = 1 - winner;
        var koRound = random.NextDouble() < 0.45 ? random.Next(1, BoxingRounds + 1) : 0;
        var koTime = Range(random, 8, RoundLength - 2);
        var downRound = koRound == 0 && random.NextDouble() < 0.35 ? random.Next(1, BoxingRounds + 1) : 0;
        var downTime = Range(random, 6, RoundLength - 8);

        for (var round = 1; round <= BoxingRounds; round++)
        {
            var start = 4 + (round - 1) * (RoundLength + RoundRest);
            play.Events.Add(new CMUSportsEvent(start, CMUSportsAction.Bell, 0, round));
            var end = round == koRound ? koTime : RoundLength;
            var down = round == downRound ? downTime : -1;
            for (var time = 0.8f; time < end; time += Range(random, 0.7, 1.8))
            {
                if (down >= 0 && time > down - 0.5f && time < down + 4)
                    continue;

                var side = random.NextDouble() < 0.58 ? winner : loser;
                var landed = random.NextDouble() < (side == winner ? 0.55 : 0.4);
                if (landed)
                    play.Score[side]++;

                play.Events.Add(new CMUSportsEvent(start + time, CMUSportsAction.Punch, side, landed ? 1 : 0));
            }

            if (down >= 0)
            {
                play.Score[winner]++;
                play.Events.Add(new CMUSportsEvent(start + down - 0.3f, CMUSportsAction.Punch, winner, 1));
                play.Events.Add(new CMUSportsEvent(start + down, CMUSportsAction.Knockdown, loser, 0));
            }

            if (round != koRound)
                continue;

            play.Score[winner]++;
            play.Events.Add(new CMUSportsEvent(start + koTime, CMUSportsAction.Punch, winner, 1));
            play.Events.Add(new CMUSportsEvent(start + koTime + 0.3f, CMUSportsAction.Knockout, loser, round));
            play.Result = $"KO R{round}";
            return play;
        }

        play.Result = $"UD 30-{27 + random.Next(3)}";
        return play;
    }

    private static CMUSportsPlay Baseball(System.Random random, int winner)
    {
        // innings[side][inning] is the list of plate appearances for that half
        var innings = new List<int>[2][];
        for (var side = 0; side < 2; side++)
        {
            innings[side] = new List<int>[Innings];
            for (var inning = 0; inning < Innings; inning++)
            {
                var half = innings[side][inning] = new List<int>();
                var outs = 0;
                while (outs < 3)
                {
                    var roll = random.NextDouble();
                    var outcome = half.Count >= 8 || roll < 0.64 ? 0 : roll < 0.82 ? 1 : roll < 0.91 ? 2 : roll < 0.95 ? 3 : 4;
                    half.Add(outcome);
                    if (outcome == 0)
                        outs++;
                }
            }
        }

        var totals = new[] { Runs(innings[0]), Runs(innings[1]) };
        if (totals[winner] < totals[1 - winner])
            (innings[0], innings[1]) = (innings[1], innings[0]);

        if (totals[0] == totals[1])
        {
            var last = innings[winner][Innings - 1];
            last.Insert(last.Count - 1, 4);
        }

        // away (side 1) bats in the top of each inning, home (side 0) in the bottom
        var halves = new List<(int Side, int Inning)>();
        for (var inning = 0; inning < Innings; inning++)
        {
            halves.Add((1, inning));
            halves.Add((0, inning));
        }

        var appearances = 0;
        foreach (var (side, inning) in halves)
            appearances += innings[side][inning].Count;

        var play = new CMUSportsPlay { Score = new int[2] };
        var slot = (Length(CMUSport.Baseball) - 8) / (appearances + halves.Count);
        var time = 3f;
        foreach (var (side, inning) in halves)
        {
            if (inning == Innings - 1 && side == 0 && play.Score[0] > play.Score[1])
                break;

            play.Events.Add(new CMUSportsEvent(time, CMUSportsAction.Bell, side, inning + 1));
            time += slot;
            var bases = 0;
            var outs = 0;
            foreach (var outcome in innings[side][inning])
            {
                var runs = Advance(ref bases, outcome);
                if (outcome == 0)
                    outs++;

                play.Score[side] += runs;
                play.Events.Add(new CMUSportsEvent(time, CMUSportsAction.AtBat, side, outcome, runs, bases | outs << 4));
                time += slot;
            }
        }

        play.Result = $"{play.Score[0]} - {play.Score[1]}";
        return play;
    }

    private static int Runs(List<int>[] innings)
    {
        var total = 0;
        foreach (var half in innings)
        {
            var bases = 0;
            foreach (var outcome in half)
                total += Advance(ref bases, outcome);
        }

        return total;
    }

    /// <summary>Moves every runner as many bases as the batter took. Bits 0 to 2 are first to third.</summary>
    public static int Advance(ref int bases, int outcome)
    {
        if (outcome == 0)
            return 0;

        if (outcome == 4)
        {
            var home = Count(bases) + 1;
            bases = 0;
            return home;
        }

        bases = (bases << outcome) | (1 << (outcome - 1));
        var scored = Count(bases >> 3);
        bases &= 7;
        return scored;
    }

    private static int Count(int bits)
    {
        var count = 0;
        for (; bits != 0; bits >>= 1)
            count += bits & 1;

        return count;
    }

    private static CMUSportsPlay ZeroG(System.Random random, int winner)
    {
        var play = new CMUSportsPlay { Score = new int[2] };
        var length = Length(CMUSport.ZeroGBall);
        var winning = random.Next(1, 6);
        var losing = random.Next(0, winning);
        var goals = new List<int>();
        for (var i = 0; i < winning; i++)
            goals.Add(winner);
        for (var i = 0; i < losing; i++)
            goals.Add(1 - winner);

        // shuffle the order the goals go in, then give each its own stretch of the match
        for (var i = goals.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (goals[i], goals[j]) = (goals[j], goals[i]);
        }

        var stretch = (length - 10) / goals.Count;
        var time = 2f;
        var holder = (Side: random.Next(2), Player: 0);
        for (var i = 0; i < goals.Count; i++)
        {
            var side = goals[i];
            var goal = 6 + stretch * i + Range(random, stretch * 0.5, stretch * 0.9);
            play.Events.Add(new CMUSportsEvent(time, CMUSportsAction.Pass, holder.Side, holder.Player));
            for (time += Range(random, 1, 2); time < goal - 2.5f; time += Range(random, 1, 2.2))
            {
                holder = (random.NextDouble() < 0.5 ? holder.Side : 1 - holder.Side, random.Next(Squad));
                play.Events.Add(new CMUSportsEvent(time, CMUSportsAction.Pass, holder.Side, holder.Player));
            }

            var shooter = random.Next(Squad);
            play.Events.Add(new CMUSportsEvent(goal - 1.5f, CMUSportsAction.Pass, side, shooter));
            play.Events.Add(new CMUSportsEvent(goal, CMUSportsAction.Goal, side, shooter));
            play.Score[side]++;
            holder = (1 - side, 0);
            time = goal + 2.5f;
        }

        play.Events.Add(new CMUSportsEvent(time, CMUSportsAction.Pass, holder.Side, holder.Player));
        for (time += 1.5f; time < length - 1; time += Range(random, 1, 2.2))
        {
            holder = (random.NextDouble() < 0.5 ? holder.Side : 1 - holder.Side, random.Next(Squad));
            play.Events.Add(new CMUSportsEvent(time, CMUSportsAction.Pass, holder.Side, holder.Player));
        }

        play.Result = $"{play.Score[0]} - {play.Score[1]}";
        return play;
    }
}
