namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>Six innings, home against away. Deterministic from the seed and the winner.</summary>
public static partial class CMUSports
{
    /// <summary>An <see cref="CMUSportsAction.AtBat"/> value besides 0 to 4: the pitch hits the batter, who takes first.</summary>
    public const int BaseballHitByPitch = 5;

    /// <summary>Where an at-bat's kind sits in its State, above the bases and outs: 0 to 3, see the out kinds.</summary>
    public const int BaseballKindShift = 8;

    /// <summary>Where an at-bat's direction sits in its State: 0 down the left field line to 4 down the right.</summary>
    public const int BaseballDirectionShift = 11;

    /// <summary>Out kinds. Hits use the same bits for how the ball flies: grounder, liner, fly ball, blooper.</summary>
    public const int BaseballStrikeout = 0;
    public const int BaseballGroundout = 1;
    public const int BaseballFlyout = 2;
    public const int BaseballPopout = 3;

    /// <summary>A <see cref="CMUSportsAction.Special"/>: the bat shatters on contact. Side: who batted. Extra: who the shard hits.</summary>
    public const int BaseballBrokenBat = 1;

    /// <summary>A Special: a foul ball flies into the stands and hits a fan. Extra: 0 the third base stand, 1 the first. State: which fan.</summary>
    public const int BaseballFanHit = 2;

    /// <summary>A Special: the home side takes the lead in the last inning and the game ends there. Extra: the runs.</summary>
    public const int BaseballWalkOff = 3;

    /// <summary>Who gets hurt, in an Injury's Value and a broken bat's Extra.</summary>
    public const int BaseballPitcher = 0;
    public const int BaseballBatter = 1;
    public const int BaseballCatcher = 2;
    public const int BaseballRunner = 3;
    public const int BaseballThirdBase = 4;

    /// <summary>Seconds before the first pitch and after the last play, for the anthem and the celebration.</summary>
    private const float BaseballLead = 3f;
    private const float BaseballTail = 7f;

    /// <summary>The time a foul ball into the stands adds before the real pitch, in tenths of a step.</summary>
    private const int BaseballFoulWeight = 9;

    /// <summary>One plate appearance as drawn: its outcome, kind and direction (kind * 5 + direction), and a roll for mishaps.</summary>
    private readonly record struct BaseballAppearance(int Outcome, int Detail, double Luck);

    /// <summary>One step of the game as played: a half inning starting, or a plate appearance and what it did.</summary>
    private readonly record struct BaseballStep(int Side, int Inning, bool Bell, BaseballAppearance At, int Runs, int Before, int State, bool WalkOff, int Weight);

    private static CMUSportsPlay Baseball(System.Random random, int winner)
    {
        // innings[side][inning] is the list of plate appearances for that half; three rolls each, always in this order
        var innings = new List<BaseballAppearance>[2][];
        for (var side = 0; side < 2; side++)
        {
            innings[side] = new List<BaseballAppearance>[Innings];
            for (var inning = 0; inning < Innings; inning++)
            {
                var half = innings[side][inning] = new List<BaseballAppearance>();
                var outs = 0;
                while (outs < 3)
                {
                    var roll = random.NextDouble();
                    var detail = (int) (random.NextDouble() * 20);
                    var luck = random.NextDouble();
                    var outcome = half.Count >= 8 || roll < 0.62 ? 0
                        : roll < 0.655 ? BaseballHitByPitch
                        : roll < 0.82 ? 1
                        : roll < 0.91 ? 2
                        : roll < 0.95 ? 3
                        : 4;
                    half.Add(new BaseballAppearance(outcome, detail, luck));
                    if (outcome == 0)
                        outs++;
                }
            }
        }

        var totals = new[] { Runs(innings[0]), Runs(innings[1]) };
        if (totals[winner] < totals[1 - winner])
            (innings[0], innings[1]) = (innings[1], innings[0]);

        // a tie goes to the winner with a home run just before their last out, to straightaway centre
        if (totals[0] == totals[1])
        {
            var last = innings[winner][Innings - 1];
            last.Insert(last.Count - 1, new BaseballAppearance(4, 2, 0.5));
        }

        // away (side 1) bats in the top of each inning, home (side 0) in the bottom
        var play = new CMUSportsPlay { Score = new int[2] };
        var steps = new List<BaseballStep>();
        var over = false;
        for (var inning = 0; inning < Innings && !over; inning++)
        {
            for (var side = 1; side >= 0 && !over; side--)
            {
                var final = inning == Innings - 1 && side == 0;
                if (final && play.Score[0] > play.Score[1])
                    break;

                steps.Add(new BaseballStep(side, inning, true, default, 0, 0, 0, false, 9));
                var bases = 0;
                var outs = 0;
                foreach (var at in innings[side][inning])
                {
                    var before = bases;
                    var runs = Advance(ref bases, at.Outcome);
                    if (at.Outcome == 0)
                        outs++;

                    play.Score[side] += runs;
                    // the home side never bats on once it leads in the last inning
                    over = final && play.Score[0] > play.Score[1];
                    var step = new BaseballStep(side, inning, false, at, runs, before, bases | outs << 4, over, 0);
                    steps.Add(step with { Weight = BaseballWeight(step) });
                    if (over)
                        break;
                }
            }
        }

        var total = 0;
        foreach (var step in steps)
            total += step.Weight;

        // every step gets time by its weight, so a home run trot lasts longer than a strikeout
        var unit = (Length(CMUSport.Baseball) - BaseballLead - BaseballTail) / total;
        var elapsed = 0;
        foreach (var step in steps)
        {
            var start = BaseballLead + unit * elapsed;
            elapsed += step.Weight;
            if (step.Bell)
            {
                play.Events.Add(new CMUSportsEvent(start, CMUSportsAction.Bell, step.Side, step.Inning + 1));
                continue;
            }

            BaseballEvents(play, step, start, unit);
        }

        play.Result = $"{play.Score[0]} - {play.Score[1]}";
        return play;
    }

    /// <summary>
    /// The events of one plate appearance starting at <paramref name="start"/>. Each lands at its own time, so the
    /// sort keeps them in this order everywhere.
    /// </summary>
    private static void BaseballEvents(CMUSportsPlay play, BaseballStep step, float start, float unit)
    {
        var at = step.At;
        var side = step.Side;
        var kind = at.Detail / 5;
        var direction = at.Detail % 5;
        var contact = start;
        if (BaseballFoul(at))
        {
            play.Events.Add(new CMUSportsEvent(start + unit * 5, CMUSportsAction.Special, side, BaseballFanHit,
                (int) (at.Luck * 1000) % 2, (int) (at.Luck * 100000) % 64));
            contact = start + unit * BaseballFoulWeight;
        }

        play.Events.Add(new CMUSportsEvent(contact, CMUSportsAction.AtBat, side, at.Outcome, step.Runs,
            step.State | kind << BaseballKindShift | direction << BaseballDirectionShift));

        if (step.WalkOff)
            play.Events.Add(new CMUSportsEvent(contact + 0.02f, CMUSportsAction.Special, side, BaseballWalkOff, step.Runs));

        if (at.Outcome == BaseballHitByPitch)
            play.Events.Add(new CMUSportsEvent(contact + 0.05f, CMUSportsAction.Injury, side, BaseballBatter, 1 + (int) (at.Luck * 3)));

        if (BaseballBroken(at))
        {
            var victim = at.Luck < 0.08 ? BaseballPitcher : BaseballThirdBase;
            play.Events.Add(new CMUSportsEvent(contact + 0.01f, CMUSportsAction.Special, side, BaseballBrokenBat, victim));
            play.Events.Add(new CMUSportsEvent(contact + 0.3f, CMUSportsAction.Injury, 1 - side, victim, 1 + (int) (at.Luck * 100) % 3));
        }

        if (BaseballCollision(step))
        {
            // the lead runner reaches the plate later the further back he started
            var lead = step.Before >= 4 ? 3 : step.Before >= 2 ? 2 : 1;
            var crash = contact + unit * (3 + 3 * (4 - lead));
            play.Events.Add(new CMUSportsEvent(crash, CMUSportsAction.Injury, 1 - side, BaseballCatcher, 1 + (int) (at.Luck * 10) % 3));
            play.Events.Add(new CMUSportsEvent(crash + 0.03f, CMUSportsAction.Injury, side, BaseballRunner, 1 + (int) (at.Luck * 40) % 3));
        }
    }

    private static bool BaseballFoul(BaseballAppearance at)
    {
        return at.Luck >= 0.94;
    }

    private static bool BaseballBroken(BaseballAppearance at)
    {
        return at.Outcome == 0 && at.Detail / 5 == BaseballGroundout && at.Luck < 0.16;
    }

    private static bool BaseballCollision(BaseballStep step)
    {
        return step.At.Outcome is >= 1 and <= 3 && step.Runs > 0 && step.At.Luck < 0.35;
    }

    /// <summary>How long a plate appearance takes, in tenths of a step: a long hit takes longer to run out.</summary>
    private static int BaseballWeight(BaseballStep step)
    {
        var at = step.At;
        var weight = at.Outcome switch
        {
            0 => (at.Detail / 5) switch
            {
                BaseballStrikeout => 8,
                BaseballGroundout => 10,
                BaseballFlyout => 12,
                _ => 10,
            },
            1 => 16,
            2 => 18,
            3 => 21,
            4 => 28,
            _ => 20,
        };

        if (BaseballFoul(at))
            weight += BaseballFoulWeight;

        if (BaseballBroken(at))
            weight += 4;

        if (BaseballCollision(step))
            weight += 12;

        return weight;
    }

    private static int Runs(List<BaseballAppearance>[] innings)
    {
        var total = 0;
        foreach (var half in innings)
        {
            var bases = 0;
            foreach (var at in half)
                total += Advance(ref bases, at.Outcome);
        }

        return total;
    }

    /// <summary>
    /// Moves every runner as many bases as the batter took, or only the forced ones when the batter is hit by the
    /// pitch. Bits 0 to 2 are first to third. Returns the runs scored.
    /// </summary>
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

        if (outcome == BaseballHitByPitch)
        {
            // the batter fills the lowest empty bag, pushing up everyone in front of it
            bases |= ~bases & (bases + 1);
        }
        else
        {
            bases = (bases << outcome) | (1 << (outcome - 1));
        }

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
}
