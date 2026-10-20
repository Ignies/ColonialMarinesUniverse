namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// Three rounds in the ring, deterministic from the seed and the winner. Punches come in combinations; each round has
/// a planned winner who throws and lands more, the judges score it 10-9 (10-8 with a knockdown), and the winner
/// always takes the cards or knocks the other out. Cuts open and teeth fly from the head shots that landed.
/// </summary>
public static partial class CMUSports
{
    /// <summary>Punch kinds, in a punch event's Extra.</summary>
    public const int BoxingJab = 0;
    public const int BoxingCross = 1;
    public const int BoxingHook = 2;
    public const int BoxingUppercut = 3;

    /// <summary>A punch event's Value: slipped, landed, or caught on the gloves.</summary>
    public const int BoxingSlipped = 0;
    public const int BoxingLanded = 1;
    public const int BoxingBlocked = 2;

    /// <summary>A punch event's State when it went to the body rather than the head.</summary>
    public const int BoxingBody = 1;

    /// <summary>Special: Side loses a tooth. Extra: how many they have lost.</summary>
    public const int BoxingTooth = 1;

    /// <summary>Special: Side ties the other up until the referee breaks them, <see cref="BoxingClinchLength"/> later.</summary>
    public const int BoxingClinch = 2;

    /// <summary>Special at the bell ending a round: Side took it, Extra and State are the red and blue points.</summary>
    public const int BoxingCard = 3;

    public const float BoxingClinchLength = 1.8f;

    /// <summary>The referee starts counting this long after a boxer goes down, then counts once every step.</summary>
    public const float BoxingCountDelay = 0.8f;

    public const float BoxingCountStep = 0.6f;

    // what interrupts the punching in a round
    private const int BoxingFlashMoment = 0;
    private const int BoxingDownMoment = 1;
    private const int BoxingClinchMoment = 2;

    /// <summary>How long a knockdown stops the fight: the count the boxer beats, getting up, the gloves wiped.</summary>
    public static float BoxingDownLength(int count)
    {
        return BoxingCountDelay + count * BoxingCountStep + 1f;
    }

    private static CMUSportsPlay Boxing(System.Random random, int winner)
    {
        var play = new CMUSportsPlay { Score = new int[2] };
        var loser = 1 - winner;

        // the shape of the fight, drawn up front so every round can plan around it
        var koRoll = random.NextDouble();
        var koPick = random.Next(1, BoxingRounds + 1);
        var koTime = Range(random, 9, RoundLength - 2);
        var lostRoll = random.NextDouble();
        var lostPick = random.Next(1, BoxingRounds + 1);
        var downRoll = random.NextDouble();
        var downPick = random.Next(1, BoxingRounds + 1);
        var downTime = Range(random, 5, 15);
        var downCount = random.Next(3, 9);
        var flashRoll = random.NextDouble();
        var flashPick = random.Next(1, BoxingRounds + 1);
        var flashTime = Range(random, 4, 12);
        var flashCount = random.Next(3, 9);

        var koRound = koRoll < 0.45 ? koPick : 0;

        // the winner only goes down in a fight they end by knockout, so the cards never have to forgive it
        var flashRound = koRound > 0 && flashRoll < 0.3 ? Math.Min(flashPick, koRound) : 0;
        if (flashRound == koRound && flashTime + BoxingDownLength(flashCount) + 2 > koTime)
            flashRound = 0;

        // the one round the loser takes, if any: the one where they drop the winner
        var lost = flashRound > 0 ? flashRound : lostRoll < 0.4 ? lostPick : 0;
        var downRound = downRoll < 0.4 ? downPick : 0;
        if (downRound == lost && downRound != koRound)
            downRound = 0;

        if (downRound > 0 && downRound == koRound && downTime + BoxingDownLength(downCount) + 2 > koTime)
            downRound = 0;

        if (downRound > 0 && downRound == flashRound
            && BoxingOverlaps(flashTime, BoxingDownLength(flashCount), downTime, BoxingDownLength(downCount)))
            downRound = 0;

        var cuts = new int[2];
        var teeth = new int[2];
        var cards = new int[2];
        var moments = new List<(float Time, float Length, int Kind)>(3);
        for (var round = 1; round <= BoxingRounds; round++)
        {
            var start = 4 + (round - 1) * (RoundLength + RoundRest);
            play.Events.Add(new CMUSportsEvent(start, CMUSportsAction.Bell, 0, round));
            var boss = round == lost ? loser : winner;
            var knockout = round == koRound;
            var end = knockout ? koTime - 1 : RoundLength;
            var clinchRoll = random.NextDouble();
            var clinchTime = Range(random, 3, 19);
            var first = play.Events.Count;

            moments.Clear();
            if (round == flashRound)
                moments.Add((flashTime, BoxingDownLength(flashCount), BoxingFlashMoment));

            if (round == downRound)
                moments.Add((downTime, BoxingDownLength(downCount), BoxingDownMoment));

            var clinchLength = BoxingClinchLength + 0.4f;
            var clinch = clinchRoll < 0.5 && clinchTime + clinchLength + 1 < end;
            foreach (var moment in moments)
            {
                clinch &= !BoxingOverlaps(moment.Time, moment.Length, clinchTime, clinchLength);
            }

            if (clinch)
                moments.Add((clinchTime, clinchLength, BoxingClinchMoment));

            moments.Sort((a, b) => a.Time.CompareTo(b.Time));
            var time = 0.8f;
            foreach (var (at, length, kind) in moments)
            {
                BoxingExchanges(random, play, start, time, at - 0.6f, boss);
                time = at + length;
                if (kind == BoxingClinchMoment)
                {
                    play.Events.Add(new CMUSportsEvent(start + at, CMUSportsAction.Special, 1 - boss, BoxingClinch));
                    continue;
                }

                var hurt = kind == BoxingFlashMoment ? winner : loser;
                var heavy = random.Next(BoxingHook, BoxingUppercut + 1);
                play.Events.Add(new CMUSportsEvent(start + at - 0.3f, CMUSportsAction.Punch, 1 - hurt, BoxingLanded, heavy));
                play.Events.Add(new CMUSportsEvent(start + at, CMUSportsAction.Knockdown, hurt,
                    kind == BoxingFlashMoment ? flashCount : downCount));
            }

            BoxingExchanges(random, play, start, time, end, boss);
            if (knockout)
            {
                var finisher = random.Next(BoxingHook, BoxingUppercut + 1);
                play.Events.Add(new CMUSportsEvent(start + koTime - 0.5f, CMUSportsAction.Punch, winner, BoxingLanded, BoxingCross));
                play.Events.Add(new CMUSportsEvent(start + koTime, CMUSportsAction.Punch, winner, BoxingLanded, finisher));
            }
            else
            {
                BoxingSway(play, first, boss);
            }

            BoxingWounds(random, play, first, cuts, teeth);
            if (knockout)
            {
                play.Events.Add(new CMUSportsEvent(start + koTime + 0.3f, CMUSportsAction.Knockout, loser, round));
                play.Result = $"KO R{round}";
                BoxingCount(play);
                return play;
            }

            // a knockdown costs the round's loser another point
            var points = new[] { 9, 9 };
            points[boss] = 10;
            if (round == downRound)
                points[loser]--;

            if (round == flashRound)
                points[winner]--;

            cards[0] += points[0];
            cards[1] += points[1];
            // just after the bell, so nothing thrown in the last instant shares its time
            play.Events.Add(new CMUSportsEvent(start + RoundLength + 0.1f, CMUSportsAction.Special, boss, BoxingCard, points[0], points[1]));
        }

        play.Result = $"UD {cards[winner]}-{cards[loser]}";
        BoxingCount(play);
        return play;
    }

    /// <summary>Whether two stretches of a round, each starting a second early, come within half a second.</summary>
    private static bool BoxingOverlaps(float a, float aLength, float b, float bLength)
    {
        return a - 1 < b + bLength + 0.5f && b - 1 < a + aLength + 0.5f;
    }

    /// <summary>
    /// Trades combinations from <paramref name="time"/> until <paramref name="until"/>, seconds into the round. The
    /// round's <paramref name="boss"/> leads more often and lands more.
    /// </summary>
    private static void BoxingExchanges(System.Random random, CMUSportsPlay play, float start, float time, float until, int boss)
    {
        while (time < until)
        {
            var side = random.NextDouble() < 0.6 ? boss : 1 - boss;
            var combo = random.Next(1, 4);
            var hit = side == boss ? 0.5 : 0.36;
            for (var i = 0; i < combo && time < until; i++)
            {
                // a combination opens with the jab or the cross and follows with anything but the jab
                var kind = i == 0 ? random.Next(BoxingJab, BoxingHook) : random.Next(BoxingCross, BoxingUppercut + 1);
                var roll = random.NextDouble();
                var outcome = roll < hit ? BoxingLanded : roll < hit + 0.3 ? BoxingBlocked : BoxingSlipped;
                var body = random.NextDouble() < 0.22 ? BoxingBody : 0;
                play.Events.Add(new CMUSportsEvent(start + time, CMUSportsAction.Punch, side, outcome, kind, body));
                time += kind == BoxingJab ? 0.3f : 0.42f;
            }

            time += Range(random, 0.5, 1.5);
        }
    }

    /// <summary>
    /// Makes sure the round's boss landed more than the other, as the cards will say: turns the boss's latest misses
    /// into hits, then the other's latest hits into misses. Uses no randomness.
    /// </summary>
    private static void BoxingSway(CMUSportsPlay play, int first, int boss)
    {
        var landed = new int[2];
        for (var i = first; i < play.Events.Count; i++)
        {
            var ev = play.Events[i];
            if (ev.Action == CMUSportsAction.Punch && ev.Value == BoxingLanded)
                landed[ev.Side]++;
        }

        for (var i = play.Events.Count - 1; i >= first && landed[boss] <= landed[1 - boss]; i--)
        {
            var ev = play.Events[i];
            if (ev.Action != CMUSportsAction.Punch || ev.Side != boss || ev.Value == BoxingLanded)
                continue;

            play.Events[i] = ev with { Value = BoxingLanded };
            landed[boss]++;
        }

        for (var i = play.Events.Count - 1; i >= first && landed[boss] <= landed[1 - boss]; i--)
        {
            var ev = play.Events[i];
            if (ev.Action != CMUSportsAction.Punch || ev.Side == boss || ev.Value != BoxingLanded)
                continue;

            play.Events[i] = ev with { Value = BoxingSlipped };
            landed[1 - boss]--;
        }
    }

    /// <summary>
    /// The damage from a round's landed head shots, in the order they were thrown: cuts that open and get worse, up
    /// to 3, and up to two teeth knocked out by hooks and uppercuts.
    /// </summary>
    private static void BoxingWounds(System.Random random, CMUSportsPlay play, int first, int[] cuts, int[] teeth)
    {
        var count = play.Events.Count;
        for (var i = first; i < count; i++)
        {
            var ev = play.Events[i];
            if (ev.Action != CMUSportsAction.Punch || ev.Value != BoxingLanded || ev.State == BoxingBody)
                continue;

            var hurt = 1 - ev.Side;
            if (random.NextDouble() < (ev.Extra == BoxingJab ? 0.02 : 0.07) && cuts[hurt] < 3)
            {
                cuts[hurt]++;
                play.Events.Add(new CMUSportsEvent(ev.Time + 0.05f, CMUSportsAction.Injury, hurt, 0, cuts[hurt]));
            }

            if (ev.Extra < BoxingHook)
                continue;

            if (random.NextDouble() < 0.06 && teeth[hurt] < 2)
            {
                teeth[hurt]++;
                play.Events.Add(new CMUSportsEvent(ev.Time + 0.04f, CMUSportsAction.Special, hurt, BoxingTooth, teeth[hurt]));
            }
        }
    }

    /// <summary>The score is the punches each side landed.</summary>
    private static void BoxingCount(CMUSportsPlay play)
    {
        foreach (var ev in play.Events)
        {
            if (ev.Action == CMUSportsAction.Punch && ev.Value == BoxingLanded)
                play.Score[ev.Side]++;
        }
    }
}
