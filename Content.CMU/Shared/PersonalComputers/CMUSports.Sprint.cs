namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>The 100 metre dash. Deterministic from the seed and the winner.</summary>
public static partial class CMUSports
{
    /// <summary>A runner leaves the blocks. Side: the lane. Extra: the reaction to the gun, in milliseconds.</summary>
    public const int SprintReaction = 0;

    /// <summary>A runner breaks before the gun and the field is called back. Side: the lane.</summary>
    public const int SprintFalseStart = 1;

    /// <summary>A runner pulls a hamstring and limps home. Side: the lane. State: the time they were on for.</summary>
    public const int SprintHamstring = 2;

    /// <summary>
    /// A runner drifts into the next lane and clips the runner there, who goes down (an <see cref="CMUSportsAction.Injury"/>
    /// just after). Side: the lane that drifts. Extra: the lane that falls. State: the time the drifter was on for.
    /// </summary>
    public const int SprintCollision = 3;

    /// <summary>
    /// Five lanes. Times are in hundredths from the gun at <see cref="SprintStart"/> and include the reaction. A runner
    /// who falls (an <see cref="CMUSportsAction.Injury"/>: Extra how bad, State the time they were on for), pulls up or
    /// gets tangled loses time but still finishes at their <see cref="CMUSportsPlay.Score"/>; it is never the winner.
    /// </summary>
    private static CMUSportsPlay Sprint(System.Random random, int lanes, int winner)
    {
        var play = new CMUSportsPlay { Score = new int[lanes] };
        var best = (int) Math.Round(Range(random, 9.6, 10.4) * 100);
        for (var lane = 0; lane < lanes; lane++)
        {
            play.Score[lane] = lane == winner ? best : best + 5 + random.Next(110);
        }

        var reaction = new int[lanes];
        for (var lane = 0; lane < lanes; lane++)
        {
            reaction[lane] = 120 + random.Next(90);
            play.Events.Add(new CMUSportsEvent(SprintStart + reaction[lane] / 1000f, CMUSportsAction.Special, lane, SprintReaction, reaction[lane]));
        }

        if (random.NextDouble() < 0.3)
        {
            var jumper = random.Next(lanes);
            play.Events.Add(new CMUSportsEvent(Range(random, 4.7, 5.6), CMUSportsAction.Special, jumper, SprintFalseStart));
        }

        var losers = new List<int>();
        for (var lane = 0; lane < lanes; lane++)
        {
            if (lane != winner)
                losers.Add(lane);
        }

        var mishap = random.Next(100);
        if (mishap >= 35 && losers.Count > 0)
        {
            if (mishap < 65)
            {
                var lane = losers[random.Next(losers.Count)];
                var into = 0.3 + random.NextDouble() * 0.4;
                SprintFall(random, play, lane, SprintOn(play, reaction, lane, into));
            }
            else if (mishap < 80)
            {
                var lane = losers[random.Next(losers.Count)];
                var into = 0.25 + random.NextDouble() * 0.35;
                var time = SprintOn(play, reaction, lane, into);
                play.Events.Add(new CMUSportsEvent((float) time, CMUSportsAction.Special, lane, SprintHamstring, 0, play.Score[lane]));
                play.Score[lane] += 200 + random.Next(150);
            }
            else
            {
                // neighbours, neither of them the winner: the slower one drifts into the faster one's heels
                var pairs = new List<int>();
                for (var lane = 0; lane + 1 < lanes; lane++)
                {
                    if (lane != winner && lane + 1 != winner)
                        pairs.Add(lane);
                }

                if (pairs.Count > 0)
                {
                    var pair = pairs[random.Next(pairs.Count)];
                    var faller = play.Score[pair + 1] < play.Score[pair] ? pair + 1 : pair;
                    var drifter = faller == pair ? pair + 1 : pair;
                    var into = 0.3 + random.NextDouble() * 0.35;
                    var time = SprintOn(play, reaction, faller, into);
                    play.Events.Add(new CMUSportsEvent((float) time, CMUSportsAction.Special, drifter, SprintCollision, faller, play.Score[drifter]));
                    SprintFall(random, play, faller, time + 0.04);
                    play.Score[drifter] += 30 + random.Next(40);
                }
            }
        }

        for (var lane = 0; lane < lanes; lane++)
        {
            var time = play.Score[lane];
            play.Events.Add(new CMUSportsEvent(SprintStart + time / 100f, CMUSportsAction.Finish, lane, time));
        }

        play.Result = $"{best / 100f:0.00}s";
        return play;
    }

    /// <summary>Seconds from kickoff when a lane is <paramref name="into"/> of its way through its race.</summary>
    private static double SprintOn(CMUSportsPlay play, int[] reaction, int lane, double into)
    {
        return SprintStart + reaction[lane] / 1000.0 + into * (play.Score[lane] / 100.0 - reaction[lane] / 1000.0);
    }

    /// <summary>A runner goes down face first: the worse the fall, the longer on the track.</summary>
    private static void SprintFall(System.Random random, CMUSportsPlay play, int lane, double time)
    {
        var severity = 1 + random.Next(3);
        play.Events.Add(new CMUSportsEvent((float) time, CMUSportsAction.Injury, lane, 0, severity, play.Score[lane]));
        play.Score[lane] += 130 + severity * 30 + random.Next(60);
    }
}
