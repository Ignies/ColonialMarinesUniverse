namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>Zero-g ball in the cage. Deterministic from the seed and the winner.</summary>
public static partial class CMUSports
{
    /// <summary>
    /// A <see cref="CMUSportsAction.Special"/> shot at goal. Side: who shoots. Extra: the shooter. State: how it ends,
    /// <see cref="ZeroGScored"/>, <see cref="ZeroGPost"/> or <see cref="ZeroGSaved"/>. A scored shot reaches the net
    /// <see cref="ZeroGShotFlight"/> seconds later, at its <see cref="CMUSportsAction.Goal"/>.
    /// </summary>
    public const int ZeroGShot = 0;

    /// <summary>
    /// A <see cref="CMUSportsAction.Special"/> brush with the electrified cage wall. Side: the side of the player who
    /// touches it. Extra: that player. State: 0 the top wall, 1 the bottom wall.
    /// </summary>
    public const int ZeroGShock = 1;

    public const int ZeroGScored = 0;
    public const int ZeroGPost = 1;
    public const int ZeroGSaved = 2;

    /// <summary>How long a shot takes to reach the goal mouth, in seconds.</summary>
    public const float ZeroGShotFlight = 0.4f;

    /// <summary>How long after a player gets the ball the rough stuff happens to them, in seconds.</summary>
    private const float ZeroGScrapDelay = 0.6f;

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
                ZeroGScrap(random, play, time, holder.Side, holder.Player);
            }

            var shooter = random.Next(Squad);
            play.Events.Add(new CMUSportsEvent(goal - 1.5f, CMUSportsAction.Pass, side, shooter));
            play.Events.Add(new CMUSportsEvent(goal - ZeroGShotFlight, CMUSportsAction.Special, side, ZeroGShot, shooter, ZeroGScored));
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
            ZeroGScrap(random, play, time, holder.Side, holder.Player);
        }

        play.Result = $"{play.Score[0]} - {play.Score[1]}";
        return play;
    }

    /// <summary>
    /// What may happen to the player who just got the ball, before the next pass: a body check from an opponent that
    /// cracks their visor, someone drifting into the live cage wall, or a shot that misses. Never changes the score.
    /// </summary>
    private static void ZeroGScrap(System.Random random, CMUSportsPlay play, float time, int side, int player)
    {
        var at = time + ZeroGScrapDelay;
        var roll = random.NextDouble();
        if (roll < 0.11)
        {
            // Extra: how hard the hit was. State: the opponent who threw it.
            var severity = random.Next(1, 4);
            var attacker = random.Next(Squad);
            play.Events.Add(new CMUSportsEvent(at, CMUSportsAction.Injury, side, player, severity, attacker));
        }
        else if (roll < 0.16)
        {
            var victimSide = random.Next(2);
            var victim = random.Next(Squad);
            var wall = random.Next(2);
            play.Events.Add(new CMUSportsEvent(at, CMUSportsAction.Special, victimSide, ZeroGShock, victim, wall));
        }
        else if (roll < 0.25)
        {
            var outcome = random.Next(ZeroGPost, ZeroGSaved + 1);
            play.Events.Add(new CMUSportsEvent(at, CMUSportsAction.Special, side, ZeroGShot, player, outcome));
        }
    }
}
