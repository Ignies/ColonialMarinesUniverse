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

    /// <summary>
    /// A plate appearance. Value: 0 out, 1 to 3 bases, 4 a home run, 5 hit by pitch. Extra: runs scored. State: bases
    /// | outs &lt;&lt; 4 | kind &lt;&lt; 8 | direction &lt;&lt; 11, see <see cref="CMUSports.BaseballKindShift"/>.
    /// </summary>
    AtBat,

    /// <summary>The ball goes to a player. Value: the player.</summary>
    Pass,

    /// <summary>Side scores. Value: the player who shot.</summary>
    Goal,

    /// <summary>Someone gets hurt and bleeds. Side: the hurt side or lane. Value: the player, or 0. Extra: how bad, 1 to 3.</summary>
    Injury,

    /// <summary>A moment only one sport knows. Value: the kind, from that sport's own constants. Extra, State: its data.</summary>
    Special,
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
public static partial class CMUSports
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

    /// <summary>The athletes who are women, by index into <see cref="Athletes"/>, for their screams and dolls.</summary>
    private static readonly HashSet<int> Women = new() { 2, 5, 7, 9, 13 };

    /// <summary>Whether an entrant is a woman. Team players all use the default doll, so they are not.</summary>
    public static bool Female(CMUSport sport, int entrant)
    {
        return !UsesTeams(sport) && Women.Contains(entrant);
    }

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
}
