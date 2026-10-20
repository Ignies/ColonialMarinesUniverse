using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Baseball: the ballpark seen from high behind home plate, six innings. The pitcher winds up and deals, the bat
/// cracks, fielders run the ball down and throw it in, runners slide into the bags, and home runs leave the park
/// under fireworks with the crowd on its feet. Beanballs, broken bats, foul balls into the stands and collisions at
/// the plate draw blood that stays in the dirt. Everything is posed from the match's events, so a late joiner sees
/// the same field.
/// </summary>
public sealed class CMUBaseballArena : CMUSportsArena
{
    #region Sounds

    private const string KnockSound = "/Audio/Weapons/dodgeball.ogg";
    private const string BrokenBatSound = "/Audio/Effects/wood_destroy2.ogg";
    private const string BounceSound = "/Audio/Effects/Footsteps/bounce.ogg";
    private const string CrashSound = "/Audio/Effects/Footsteps/largethud.ogg";
    private const string SmackSound = "/Audio/Effects/Footsteps/meatslap.ogg";
    private const string PierceSound = "/Audio/_RMC14/Weapons/pierce.ogg";
    private const string HornSound = "/Audio/Items/airhorn.ogg";
    private const string ChimeSound = "/Audio/Effects/voteding.ogg";

    /// <summary>The crack of the bat, the stadium's "charge!" fanfare, the crowd and the thud of a body going down.</summary>
    private const string HitSound = "/Audio/CMU14/PersonalComputers/Games/baseball_hit.ogg";
    private const string ChargeSound = "/Audio/CMU14/PersonalComputers/Games/baseball_charge.ogg";
    private const string SmallCheerSound = "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_small.ogg";
    private const string FallSound = "/Audio/CMU14/PersonalComputers/Games/fall.ogg";

    /// <summary>Big cheers, long enough that only one plays at a time.</summary>
    private static readonly string[] CheerSounds =
    {
        "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_1.ogg", "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_2.ogg",
    };

    /// <summary>Pitches, swings and throws.</summary>
    private static readonly string[] SwooshSounds =
    {
        "/Audio/CMU14/PersonalComputers/Games/swoosh_1.ogg", "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg",
        "/Audio/CMU14/PersonalComputers/Games/swoosh_3.ogg",
    };

    private static readonly string[] SlideSounds =
    {
        "/Audio/Effects/Footsteps/asteroid1.ogg", "/Audio/Effects/Footsteps/asteroid2.ogg",
        "/Audio/Effects/Footsteps/asteroid3.ogg",
    };

    private static readonly string[] MittSounds =
    {
        "/Audio/Weapons/boxingpunch1.ogg", "/Audio/Weapons/boxingpunch2.ogg", "/Audio/Weapons/boxingpunch3.ogg",
    };

    private static readonly string[] BeanSounds =
    {
        "/Audio/_RMC14/Weapons/genhit1.ogg", "/Audio/_RMC14/Weapons/genhit2.ogg", "/Audio/_RMC14/Weapons/genhit3.ogg",
    };

    private static readonly string[] MeatSounds =
    {
        "/Audio/Weapons/Guns/Hits/bullet_meat1.ogg", "/Audio/Weapons/Guns/Hits/bullet_meat2.ogg",
        "/Audio/Weapons/Guns/Hits/bullet_meat3.ogg", "/Audio/Weapons/Guns/Hits/bullet_meat4.ogg",
    };

    private static readonly string[] ClapSounds =
    {
        "/Audio/Effects/Emotes/clap1.ogg", "/Audio/Effects/Emotes/clap2.ogg", "/Audio/Effects/Emotes/clap3.ogg",
        "/Audio/Effects/Emotes/clap4.ogg",
    };

    private static readonly string[] WhistleSounds =
    {
        "/Audio/Voice/Human/whistle_1.ogg", "/Audio/Voice/Human/whistle_2.ogg", "/Audio/Voice/Human/whistle_3.ogg",
        "/Audio/Voice/Human/whistle_4.ogg",
    };

    private static readonly string[] FireworkSounds = { "/Audio/Effects/explosionsmallfar.ogg" };

    #endregion

    #region Field geometry

    private const string BatRsi = "/Textures/Objects/Weapons/Melee/baseball_bat.rsi";

    // The field is laid out in true units of 90 ft (home to first), x to the right and y out to centre field, and
    // projected so that things shrink with distance. Home plate sits near the bottom of the arena.
    private const float HomeX = 260;
    private const float HomeY = 206;
    private const float DepthScale = 75f;
    private const float DepthFall = 0.3346f;
    private const float WidthScale = 139.9f;

    /// <summary>How far foul territory runs out from the lines to the stands.</summary>
    private const float FoulWidth = 0.45f;

    // Roles, the same for both sides. A runner's role is Batter plus the base he started the play on.
    private const int Pitcher = 0;
    private const int Catcher = 1;
    private const int First = 2;
    private const int Second = 3;
    private const int Shortstop = 4;
    private const int Third = 5;
    private const int Left = 6;
    private const int Centre = 7;
    private const int Right = 8;
    private const int Batter = 9;
    private const int OnDeck = 13;
    private const int Roles = 14;

    /// <summary>Who is added first, so the far side of the field is drawn under the near side.</summary>
    private static readonly int[] DrawOrder =
    {
        Centre, Left, Right, Second, Shortstop, Batter + 2, First, Third, Batter + 1, Batter + 3, Pitcher, OnDeck, Batter,
        Catcher,
    };

    /// <summary>Where each fielder plays, by role.</summary>
    private static readonly Vector2[] Spots =
    {
        new(0, 0.672f), new(0, -0.17f), new(0.80f, 0.98f), new(0.36f, 1.50f), new(-0.36f, 1.50f),
        new(-0.80f, 0.98f), new(-1.75f, 2.70f), new(0, 3.30f), new(1.75f, 2.70f),
    };

    /// <summary>Home, first, second, third.</summary>
    private static readonly Vector2[] BaseSpots = { new(0, 0), new(0.7071f, 0.7071f), new(0, 1.4142f), new(-0.7071f, 0.7071f) };

    private static readonly Vector2 BatterSpot = new(-0.12f, 0);
    private static readonly Vector2 Zone = new(-0.02f, 0.05f);
    private static readonly Vector2 Mitt = new(0, -0.13f);
    private static readonly Vector2 Hand = new(0.03f, 0.62f);

    /// <summary>Dugouts and on-deck circles by side: home on the first base side, away on the third.</summary>
    private static readonly Vector2[] Dugouts = { new(0.70f, 0.14f), new(-0.70f, 0.14f) };
    private static readonly Vector2[] Circles = { new(0.50f, -0.04f), new(-0.50f, -0.04f) };

    private static readonly Direction[] PathFacing = { Direction.East, Direction.North, Direction.West, Direction.South };

    // Broadcast boxes over the stands, in arena units: the score bug, the outfield board and the radar gun.
    private static readonly Vector4 BugBox = new(6, 30, 112, 32);
    private static readonly Vector4 BoardBox = new(194, 28, 132, 38);
    private static readonly Vector4 RadarBox = new(430, 30, 84, 16);

    private static readonly Vector2[] Surface;
    private static readonly Vector2[] Track;
    private static readonly Vector2[] Wall;
    private static readonly Vector2[] WallTop;
    private static readonly Vector2[][] Stripes;
    private static readonly Vector2[] Dirt;
    private static readonly Vector2[] Infield;
    private static readonly Vector2[] PlateCircle;
    private static readonly Vector2[] Mound;
    private static readonly Vector2[] Rubber;
    private static readonly Vector2[] PlateShape;
    private static readonly Vector2[][] Bags;
    private static readonly Vector2[][] Rails;
    private static readonly Vector2[][] Dens;
    private static readonly Vector2[] Chalk;
    private static readonly Vector2[] Poles;

    /// <summary>A fan in the stands: where, how big, shirt and skin, and the phase they bounce at.</summary>
    private readonly record struct Seat(Vector2 At, float Size, int Shirt, int Skin, float Phase);

    private static readonly Seat[] Seats;

    /// <summary>The fans in the lower stands a foul ball can reach: third base side, then first.</summary>
    private static readonly int[][] FoulSeats;

    private static readonly Color[] Skins =
    {
        Color.FromHex("#F1C9A5"), Color.FromHex("#C68E63"), Color.FromHex("#8D5A3B"), Color.FromHex("#E0AC7E"),
    };

    private static readonly Color[] FireworkColours =
    {
        Color.FromHex("#FFD24A"), Color.FromHex("#FF4A4A"), Color.FromHex("#5AE0FF"), Color.FromHex("#FFFFFF"),
        Color.FromHex("#9DFF5A"),
    };

    private static readonly Color Grass = Color.FromHex("#3B8636");
    private static readonly Color Mown = Color.FromHex("#45943E");
    private static readonly Color Clay = Color.FromHex("#B6804F");
    private static readonly Color Chalky = Color.FromHex("#F2F0E6");
    private static readonly Color Wood = Color.FromHex("#9A6B3A");

    private static readonly string[] Digits = Enumerable.Range(0, 100).Select(i => i.ToString()).ToArray();
    private static readonly string[] Columns = { "1", "2", "3", "4", "5", "6" };

    static CMUBaseballArena()
    {
        var leftLine = new Vector2(-0.7071f, 0.7071f);
        var rightLine = new Vector2(0.7071f, 0.7071f);
        var leftOut = new Vector2(-0.7071f, -0.7071f);
        var rightOut = new Vector2(0.7071f, -0.7071f);
        var pole = Fence(45);

        // the whole playing surface as one fan around home: stands fronts, the fence, the backstop
        var surface = new List<Vector2> { Project(Vector2.Zero) };
        var backLeft = leftLine * -0.25f + leftOut * FoulWidth;
        surface.Add(Project(backLeft));
        surface.Add(Project(leftLine * pole + leftOut * FoulWidth));
        for (var a = -45; a <= 45; a += 3)
            surface.Add(Project(Polar(a, Fence(a))));

        surface.Add(Project(rightLine * pole + rightOut * FoulWidth));
        surface.Add(Project(rightLine * -0.25f + rightOut * FoulWidth));
        for (var a = 164; a <= 196; a += 8)
            surface.Add(Project(Polar(a, backLeft.Length())));

        surface.Add(surface[1]);
        Surface = surface.ToArray();

        var track = new List<Vector2>();
        var wall = new List<Vector2>();
        var top = new List<Vector2>();
        for (var a = -45; a <= 45; a += 3)
        {
            var outer = Project(Polar(a, Fence(a)));
            track.Add(Project(Polar(a, Fence(a) - 0.14f)));
            track.Add(outer);
            var high = outer - new Vector2(0, 7 * Near(outer.Y));
            wall.Add(outer);
            wall.Add(high);
            top.Add(high);
        }

        Track = track.ToArray();
        Wall = wall.ToArray();
        WallTop = top.ToArray();

        var stripes = new List<Vector2[]>();
        for (var k = 0; k < 12; k += 2)
        {
            var wedge = new List<Vector2> { Project(Vector2.Zero) };
            for (var a = -45f + k * 7.5f; a <= -45f + (k + 1) * 7.5f + 0.01f; a += 2.5f)
                wedge.Add(Project(Polar(a, Fence(a) - 0.14f)));

            stripes.Add(wedge.ToArray());
        }

        Stripes = stripes.ToArray();

        // the infield dirt: the arc behind the bags around the mound, closing down the lines to the plate
        var mound = Spots[Pitcher];
        var dirt = new List<Vector2> { Project(mound), Project(new Vector2(1.006f, 1.006f)) };
        for (var a = 72f; a >= -72f; a -= 8)
            dirt.Add(Project(mound + Polar(a, 1.06f)));

        dirt.Add(Project(new Vector2(-1.006f, 1.006f)));
        dirt.Add(Project(new Vector2(0, -0.14f)));
        dirt.Add(dirt[1]);
        Dirt = dirt.ToArray();

        Infield = Fan(new Vector2(0, 0.70f), new Vector2(0, 0.17f), new Vector2(0.58f, 0.735f), new Vector2(0, 1.25f), new Vector2(-0.58f, 0.735f));
        PlateCircle = Ring(Vector2.Zero, 0.17f, 20);
        Mound = Ring(mound, 0.1f, 16);
        Rubber = Fan(mound, new Vector2(-0.035f, 0.665f), new Vector2(0.035f, 0.665f), new Vector2(0.035f, 0.68f), new Vector2(-0.035f, 0.68f));
        PlateShape = Fan(new Vector2(0, -0.015f), new Vector2(-0.028f, 0), new Vector2(0.028f, 0), new Vector2(0.028f, -0.02f), new Vector2(0, -0.042f), new Vector2(-0.028f, -0.02f));
        Bags = new Vector2[3][];
        for (var i = 0; i < 3; i++)
        {
            var bag = BaseSpots[i + 1];
            Bags[i] = Fan(bag, bag + new Vector2(0.036f, 0), bag + new Vector2(0, 0.036f), bag - new Vector2(0.036f, 0), bag - new Vector2(0, 0.036f));
        }

        // the front walls of the lower stands, and the dugouts set into them
        Rails = new Vector2[2][];
        Dens = new Vector2[2][];
        for (var side = 0; side < 2; side++)
        {
            var line = side == 0 ? rightLine : leftLine;
            var away = side == 0 ? rightOut : leftOut;
            var near = Project(line * -0.25f + away * FoulWidth);
            var far = Project(line * pole + away * FoulWidth);
            Rails[side] = new[] { near, near - new Vector2(0, 6 * Near(near.Y)), far, far - new Vector2(0, 6 * Near(far.Y)) };
            var a = line * 0.22f + away * FoulWidth;
            var b = line * 0.62f + away * FoulWidth;
            Dens[side] = new[]
            {
                Project(a), Project(b), Project(b + away * 0.14f), Project(a + away * 0.14f), Project(a),
            };
        }

        // batter's boxes and the foul lines, as pairs of points
        var chalk = new List<Vector2>();
        foreach (var x in new[] { -0.17f, 0.05f })
        {
            var box = new[] { new Vector2(x, -0.08f), new Vector2(x + 0.12f, -0.08f), new Vector2(x + 0.12f, 0.08f), new Vector2(x, 0.08f) };
            for (var i = 0; i < 4; i++)
            {
                chalk.Add(Project(box[i]));
                chalk.Add(Project(box[(i + 1) % 4]));
            }
        }

        chalk.Add(Project(new Vector2(-0.12f, 0.12f)));
        chalk.Add(Project(leftLine * pole));
        chalk.Add(Project(new Vector2(0.12f, 0.12f)));
        chalk.Add(Project(rightLine * pole));
        Chalk = chalk.ToArray();
        Poles = new[] { Project(leftLine * pole), Project(rightLine * pole) };

        // fans fill every seat the field, the fence and the broadcast boxes leave free
        var seats = new List<Seat>();
        var index = 0;
        for (var row = 0; ; row++)
        {
            var y = Bar + 3 + row * 7f;
            if (y > ArenaHeight - 2)
                break;

            for (var x = 3 + row % 2 * 3.5f; x < ArenaWidth - 2; x += 7, index++)
            {
                var at = new Vector2(x, y);
                if (Inside(at) || Inside(at + new Vector2(0, 6)) || Inside(at + new Vector2(5, 3)) || Inside(at + new Vector2(-5, 3)))
                    continue;

                if (In(BugBox, at) || In(BoardBox, at) || In(RadarBox, at))
                    continue;

                var hash = Hash(index);
                seats.Add(new Seat(at, 1.6f + 1.4f * Near(y), hash % 10, (hash >> 8) % Skins.Length, hash % 628 / 100f));
            }
        }

        Seats = seats.ToArray();
        var foul = new[] { new List<int>(), new List<int>() };
        for (var i = 0; i < Seats.Length; i++)
        {
            var at = Seats[i].At;
            if (at.Y > 120 && at.Y < ArenaHeight - 10)
                foul[at.X < HomeX ? 0 : 1].Add(i);
        }

        FoulSeats = new[] { foul[0].ToArray(), foul[1].ToArray() };
    }

    private static Vector2 Project(Vector2 ground)
    {
        var depth = 1 + DepthFall * ground.Y;
        return new Vector2(HomeX + ground.X * WidthScale / depth, HomeY - DepthScale * ground.Y / depth);
    }

    private static Vector2 Unproject(Vector2 screen)
    {
        var rise = HomeY - screen.Y;
        var y = rise / (DepthScale - DepthFall * rise);
        return new Vector2((screen.X - HomeX) * (1 + DepthFall * y) / WidthScale, y);
    }

    /// <summary>How big things look at a height on screen: 1 at the plate, smaller out towards the fence.</summary>
    private static float Near(float screenY)
    {
        return 1 - DepthFall / DepthScale * (HomeY - screenY);
    }

    /// <summary>A point out from home, by degrees off straightaway centre (negative towards left field).</summary>
    private static Vector2 Polar(float degrees, float distance)
    {
        var a = degrees * MathF.PI / 180;
        return new Vector2(MathF.Sin(a) * distance, MathF.Cos(a) * distance);
    }

    /// <summary>How far the fence is at an angle: 400 ft to centre, 330 down the lines.</summary>
    private static float Fence(float degrees)
    {
        return 4.44f - 0.77f * MathF.Pow(MathF.Min(1, MathF.Abs(degrees) / 45), 1.5f);
    }

    private static Vector2[] Fan(Vector2 centre, params Vector2[] points)
    {
        var fan = new Vector2[points.Length + 2];
        fan[0] = Project(centre);
        for (var i = 0; i < points.Length; i++)
            fan[i + 1] = Project(points[i]);

        fan[^1] = fan[1];
        return fan;
    }

    private static Vector2[] Ring(Vector2 centre, float radius, int segments)
    {
        var points = new Vector2[segments];
        for (var i = 0; i < segments; i++)
            points[i] = centre + Polar(360f * i / segments, radius);

        return Fan(centre, points);
    }

    /// <summary>Whether a spot on screen is on the playing surface rather than up in the stands.</summary>
    private static bool Inside(Vector2 at)
    {
        var inside = false;
        for (int i = 1, j = Surface.Length - 1; i < Surface.Length; j = i++)
        {
            var a = Surface[i];
            var b = Surface[j];
            if (a.Y > at.Y != b.Y > at.Y && at.X < (b.X - a.X) * (at.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }

        return inside;
    }

    private static bool In(Vector4 box, Vector2 at)
    {
        return at.X > box.X - 3 && at.X < box.X + box.Z + 3 && at.Y > box.Y - 3 && at.Y < box.Y + box.W + 3;
    }

    private static int Hash(int i)
    {
        unchecked
        {
            var h = (uint) i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (int) (h & 0x7FFFFFFF);
        }
    }

    #endregion

    #region The match as the arena plays it

    /// <summary>A half inning: when its bell rings, who bats, and its first plate appearance.</summary>
    private sealed class Half
    {
        public float Start;
        public int Side;
        public int Inning;
        public int First;
        public float Intro;
    }

    /// <summary>A fielder running somewhere during a play, in choreography seconds after contact.</summary>
    private struct Move
    {
        public int Role;
        public Vector2 To;
        public float From;
        public float By;
    }

    /// <summary>A stretch of the ball's flight, in choreography seconds after contact. Heights are screen pixels at the plate.</summary>
    private struct Flight
    {
        public float From;
        public float To;
        public Vector2 A;
        public Vector2 B;
        public float HeightA;
        public float HeightB;
        public float Arc;
        public int Hops;
    }

    /// <summary>
    /// One plate appearance worked out from the events: what happened, when the pitches come, and how everyone
    /// moves. The choreography is written in its own seconds after contact and stretched by <see cref="Pace"/> to
    /// fill the time the match gives it.
    /// </summary>
    private sealed class Plate
    {
        public int Number;
        public Half Half = default!;
        public int Side;
        public int Outcome;
        public int Runs;
        public int Kind;
        public int Direction;
        public int Before;
        public int After;
        public int Outs;
        public int Home;
        public int Away;

        public float Start;
        public float Windup;
        public float Release;
        public float Contact;
        public float End;
        public float Pace = 1;
        public float Finish = 1;

        public bool Fouled;
        public float Foul;
        public float FoulWindup;
        public float FoulRelease;
        public float FanTime;
        public int FanSeat;

        public bool Broken;
        public float Shard = float.NaN;
        public int ShardVictim;
        public float Crash = float.NaN;
        public int CrashBase;
        public int Severity;
        public bool WalkOff;
        public bool LastOfHalf;
        public bool LastOfGame;

        /// <summary>Whether the organ plays the charge once this play has run out.</summary>
        public bool Charge;

        public readonly Flight[] Ball = new Flight[3];
        public int Flights;
        public readonly Move[] Moves = new Move[8];
        public int MoveCount;
        public int Catching = -1;
        public float Watch = float.NaN;
        public float Landed = float.NaN;
        public float Caught = float.NaN;
        public float Thrown = float.NaN;
        public int ThrowBase;
        public bool Safe;
        public float Slide = float.NaN;
        public float BatterFrom = 0.18f;
        public float RunFrom = 0.05f;
        public float PerBase = 0.55f;
        public float Mob = float.NaN;

        public string Call = string.Empty;
        public Color CallColour = Color.White;
        public float CallAt;
        public string Line = string.Empty;
        public float LineAt;
        public string Speed = string.Empty;
        public string Distance = string.Empty;

        /// <summary>Real time of a choreography moment.</summary>
        public float Real(float tau)
        {
            return Contact + tau * Pace;
        }

        public void AddMove(int role, Vector2 to, float from, float by)
        {
            if (MoveCount < Moves.Length)
                Moves[MoveCount++] = new Move { Role = role, To = to, From = from, By = by };
        }

        public void AddFlight(float from, float to, Vector2 a, Vector2 b, float heightA, float heightB, float arc, int hops = 0)
        {
            if (Flights < Ball.Length)
                Ball[Flights++] = new Flight { From = from, To = to, A = a, B = b, HeightA = heightA, HeightB = heightB, Arc = arc, Hops = hops };
        }

        public bool Has(int startBase)
        {
            return startBase == 0 || (Before & 1 << (startBase - 1)) != 0;
        }
    }

    /// <summary>Where and how a doll stands this frame.</summary>
    private struct Body
    {
        public Vector2 Ground;
        public Direction Facing;
        public float Lift;
        public float Squash;
        public float Lie;
        public float Rotation;
        public float Alpha;
    }

    #endregion

    private readonly int[,] _dolls = new int[2, Roles];
    private readonly bool[] _used = new bool[2 * Roles];
    private readonly Vector2[] _scales = new Vector2[2 * Roles];
    private readonly float[] _alphas = new float[2 * Roles];
    private readonly Vector2[] _feet = new Vector2[2 * Roles];
    private readonly Vector2[] _buffer = new Vector2[64];
    private readonly List<Plate> _plates = new();
    private readonly List<Half> _halves = new();
    private int[] _plateOf = Array.Empty<int>();
    private readonly bool[] _hurtFans = new bool[Seats.Length];
    private readonly Color[] _shirts = new Color[10];
    private readonly int[,] _line = new int[2, CMUSports.Innings];
    private readonly bool[,] _played = new bool[2, CMUSports.Innings];
    private readonly int[] _runs = new int[2];
    private readonly int[] _hits = new int[2];
    private readonly string[] _abbreviations = { string.Empty, string.Empty };
    private readonly string[] _teams = { string.Empty, string.Empty };
    private readonly string[,] _cards = new string[2, CMUSports.Innings];
    private readonly string[,] _batting = new string[2, CMUSports.Innings];
    private readonly string[,] _bugs = new string[2, CMUSports.Innings];
    private readonly Vector2[] _trail = new Vector2[6];
    private readonly float[] _trailSize = new float[6];

    private bool _hasPlay;
    private float _t;
    private float _lastT = float.NaN;
    private bool _cueing;
    private float _clock;
    private float _lastClock;
    private float _excite;
    private float _cheerUntil;
    private float _smallUntil;
    private Half? _half;
    private Plate? _plate;
    private int _outs;
    private int _bases;
    private bool _ballOn;
    private Vector2 _ballAt;
    private Vector2 _ballShadow;
    private float _ballSize;
    private float _ballHeight;
    private int _trailCount;
    private bool _batOn;
    private Vector2 _batAt;
    private float _batAngle;
    private float _batSize;
    private bool _shardOn;
    private Vector2 _shardAt;
    private float _shardAngle;
    private float _shardSize;
    private string _radar = string.Empty;
    private Color _awayColour = Color.White;
    private Color _homeColour = Color.White;

    private string _strikeout = string.Empty;
    private string _out = string.Empty;
    private string _caught = string.Empty;
    private string _single = string.Empty;
    private string _double = string.Empty;
    private string _triple = string.Empty;
    private string _homer = string.Empty;
    private string _beanball = string.Empty;
    private string _takesFirst = string.Empty;
    private string _brokenBat = string.Empty;
    private string _fanDown = string.Empty;
    private string _collision = string.Empty;
    private string _safe = string.Empty;
    private string _walkOff = string.Empty;
    private string _playBall = string.Empty;
    private string _matchup = string.Empty;
    private string _final = string.Empty;
    private string _outsLabel = string.Empty;
    private string _runsLabel = string.Empty;
    private string _hitsLabel = string.Empty;
    private readonly string[] _runsScore = new string[5];

    protected override void Build(CMUBetMatch match)
    {
        for (var i = 0; i < 2 * Roles; i++)
        {
            _scales[i] = Vector2.One;
            _alphas[i] = 1;
        }

        foreach (var role in DrawOrder)
        {
            for (var side = 0; side < 2; side++)
            {
                _dolls[side, role] = AddDoll(CMUSports.Kit("Player", CMUSports.Colour(match.Sport, side, match.Entrants[side])), 1);
            }
        }

        _homeColour = KitColour(match, 0);
        _awayColour = KitColour(match, 1);
        for (var side = 0; side < 2; side++)
        {
            _teams[side] = Short(match.Name(side));
            _abbreviations[side] = _teams[side].Length > 3 ? _teams[side][..3] : _teams[side];
        }

        // most of the crowd wears the home colours, a corner of it the visitors'
        var neutral = new[] { "#E8E8E8", "#3A4A6A", "#7A6A55", "#222428", "#8A3030", "#5A7A4A" };
        for (var i = 0; i < _shirts.Length; i++)
            _shirts[i] = i < 3 ? _homeColour : i == 3 ? _awayColour : Color.FromHex(neutral[i - 4]);

        for (var inning = 0; inning < CMUSports.Innings; inning++)
        {
            var ordinal = Loc.GetString("cmu-game-baseball-ordinal", ("inning", inning + 1));
            for (var side = 0; side < 2; side++)
            {
                _cards[side, inning] = Loc.GetString(side == 1 ? "cmu-game-baseball-top" : "cmu-game-baseball-bottom", ("inning", ordinal));
                _batting[side, inning] = Loc.GetString("cmu-game-baseball-at-bat", ("team", _teams[side]));
                _bugs[side, inning] = $"{(side == 1 ? "▲" : "▼")} {ordinal}";
            }
        }

        _strikeout = Loc.GetString("cmu-game-baseball-strikeout");
        _out = Loc.GetString("cmu-game-baseball-out");
        _caught = Loc.GetString("cmu-game-baseball-caught");
        _single = Loc.GetString("cmu-game-baseball-single");
        _double = Loc.GetString("cmu-game-baseball-double");
        _triple = Loc.GetString("cmu-game-baseball-triple");
        _homer = Loc.GetString("cmu-game-baseball-homer");
        _beanball = Loc.GetString("cmu-game-baseball-beanball");
        _takesFirst = Loc.GetString("cmu-game-baseball-takes-first");
        _brokenBat = Loc.GetString("cmu-game-baseball-broken-bat");
        _fanDown = Loc.GetString("cmu-game-baseball-fan-down");
        _collision = Loc.GetString("cmu-game-baseball-collision");
        _safe = Loc.GetString("cmu-game-baseball-safe");
        _walkOff = Loc.GetString("cmu-game-baseball-walk-off");
        _playBall = Loc.GetString("cmu-game-baseball-play-ball");
        _matchup = Loc.GetString("cmu-game-baseball-matchup", ("away", _teams[1]), ("home", _teams[0]));
        _final = Loc.GetString("cmu-sport-final");
        _outsLabel = Loc.GetString("cmu-game-baseball-outs");
        _runsLabel = Loc.GetString("cmu-game-baseball-runs-column");
        _hitsLabel = Loc.GetString("cmu-game-baseball-hits-column");
        for (var runs = 1; runs < _runsScore.Length; runs++)
            _runsScore[runs] = Loc.GetString("cmu-game-baseball-runs", ("runs", runs));

        Array.Clear(_hurtFans);
        _excite = 0;
        _cheerUntil = 0;
        _smallUntil = 0;
        _lastT = float.NaN;
        _hasPlay = match.Winner >= 0;
        // the same match the base plays, worked out here too so events can be placed on the field as they come
        Prepare(_hasPlay ? CMUSports.Play(match.Sport, match.Seed, match.Entrants.Count, match.Winner) : null);
    }

    #region Preparing the plays

    private void Prepare(CMUSportsPlay? play)
    {
        _plates.Clear();
        _halves.Clear();
        _plateOf = play == null ? Array.Empty<int>() : new int[play.Events.Count];
        if (play == null)
            return;

        Half? half = null;
        Plate? last = null;
        var bases = 0;
        var home = 0;
        var away = 0;
        var fan = -1;
        for (var i = 0; i < play.Events.Count; i++)
        {
            var ev = play.Events[i];
            _plateOf[i] = -1;
            switch (ev.Action)
            {
                case CMUSportsAction.Bell:
                    if (last != null)
                        last.LastOfHalf = true;

                    half = new Half { Start = ev.Time, Side = ev.Side, Inning = Math.Clamp(ev.Value, 1, CMUSports.Innings), First = _plates.Count };
                    _halves.Add(half);
                    bases = 0;
                    break;

                case CMUSportsAction.AtBat when half != null:
                    var plate = new Plate
                    {
                        Number = _plates.Count,
                        Half = half,
                        Side = ev.Side,
                        Outcome = ev.Value,
                        Runs = ev.Extra,
                        Before = bases,
                        After = ev.State & 7,
                        Outs = ev.State >> 4 & 3,
                        Kind = ev.State >> CMUSports.BaseballKindShift & 3,
                        Direction = Math.Clamp(ev.State >> CMUSports.BaseballDirectionShift & 7, 0, 4),
                        Contact = ev.Time,
                    };

                    bases = plate.After;
                    if (ev.Side == 0)
                        home += ev.Extra;
                    else
                        away += ev.Extra;

                    plate.Home = home;
                    plate.Away = away;
                    if (fan >= 0)
                    {
                        var hit = play.Events[fan];
                        plate.Fouled = true;
                        plate.FanTime = hit.Time;
                        var stand = FoulSeats[Math.Clamp(hit.Extra, 0, 1)];
                        plate.FanSeat = stand.Length > 0 ? stand[hit.State % stand.Length] : 0;
                        _plateOf[fan] = plate.Number;
                        fan = -1;
                    }

                    _plates.Add(plate);
                    _plateOf[i] = plate.Number;
                    last = plate;
                    break;

                case CMUSportsAction.Special when ev.Value == CMUSports.BaseballFanHit:
                    fan = i;
                    break;

                default:
                    if (last == null)
                        break;

                    _plateOf[i] = last.Number;
                    if (ev.Action == CMUSportsAction.Special && ev.Value == CMUSports.BaseballBrokenBat)
                    {
                        last.Broken = true;
                        last.ShardVictim = ev.Extra;
                    }
                    else if (ev.Action == CMUSportsAction.Special && ev.Value == CMUSports.BaseballWalkOff)
                    {
                        last.WalkOff = true;
                    }
                    else if (ev.Action == CMUSportsAction.Injury)
                    {
                        switch (ev.Value)
                        {
                            case CMUSports.BaseballBatter:
                                last.Severity = ev.Extra;
                                break;
                            case CMUSports.BaseballPitcher or CMUSports.BaseballThirdBase:
                                last.Shard = ev.Time;
                                break;
                            case CMUSports.BaseballCatcher:
                                last.Crash = ev.Time;
                                break;
                        }
                    }

                    break;
            }
        }

        if (last != null)
        {
            last.LastOfHalf = true;
            last.LastOfGame = true;
        }

        var length = CMUSports.Length(CMUSport.Baseball);
        for (var n = 0; n < _plates.Count; n++)
        {
            var plate = _plates[n];
            var previous = n > 0 && _plates[n - 1].Half == plate.Half ? _plates[n - 1].Contact : plate.Half.Start;
            if (plate.Fouled)
            {
                // the fan hit sits five tenths into the step, the real pitch nine
                plate.Foul = plate.FanTime - (plate.Contact - plate.FanTime) * 1.25f;
                var lead = Lead(plate.Foul - previous);
                plate.FoulWindup = plate.Foul - lead;
                plate.FoulRelease = plate.Foul - PitchTime(lead);
                var again = Math.Clamp(plate.Contact - plate.FanTime - 0.05f, 0.25f, 0.55f);
                plate.Windup = plate.Contact - again;
                plate.Release = plate.Contact - PitchTime(again);
                plate.Start = plate.FoulWindup;
            }
            else
            {
                var lead = Lead(plate.Contact - previous);
                plate.Windup = plate.Contact - lead;
                plate.Release = plate.Contact - PitchTime(lead);
                plate.Start = plate.Windup;
            }

            if (plate.Number == plate.Half.First)
                plate.Half.Intro = plate.Start;
        }

        // the charge: the first time in a late home half that a runner reaches scoring position
        Half? charged = null;
        foreach (var plate in _plates)
        {
            if (plate.Side == 0 && plate.Half.Inning >= 4 && plate.Half != charged && (plate.After & 6) != 0 && plate.Outs < 3 && !plate.WalkOff)
            {
                plate.Charge = true;
                charged = plate.Half;
            }
        }

        for (var n = 0; n < _plates.Count; n++)
        {
            var plate = _plates[n];
            if (n + 1 < _plates.Count && _plates[n + 1].Half == plate.Half)
                plate.End = _plates[n + 1].Start;
            else
                plate.End = NextHalf(plate.Half)?.Start ?? length;

            Choreograph(plate);
            var span = Math.Min(plate.End, length) - plate.Contact;
            plate.Pace = Math.Clamp(span / plate.Finish, 0.4f, 1.25f);
            if (!float.IsNaN(plate.Crash))
            {
                // the lead runner reaches the plate exactly when the match says he flattens the catcher
                var arrive = plate.RunFrom + (4 - plate.CrashBase) * plate.PerBase;
                plate.Pace = Math.Clamp((plate.Crash - plate.Contact) / arrive, 0.4f, 2.5f);
            }
        }
    }

    private Half? NextHalf(Half half)
    {
        var index = _halves.IndexOf(half);
        return index >= 0 && index + 1 < _halves.Count ? _halves[index + 1] : null;
    }

    /// <summary>How long before the ball reaches the plate the pitcher starts his windup.</summary>
    private static float Lead(float gap)
    {
        return Math.Clamp(gap * 0.36f, 0.25f, 0.55f);
    }

    private static float PitchTime(float lead)
    {
        return Math.Min(0.24f, lead * 0.42f);
    }

    /// <summary>Works out who runs where and how the ball flies for one plate appearance.</summary>
    private void Choreograph(Plate p)
    {
        var dir = p.Direction;
        p.Speed = Loc.GetString("cmu-game-baseball-mph", ("speed", 86 + Hash(p.Number * 7 + p.Half.Inning) % 15));
        switch (p.Outcome)
        {
            case 0:
                ChoreographOut(p, dir);
                break;

            case CMUSports.BaseballHitByPitch:
                p.Call = _beanball;
                p.CallColour = Color.FromHex("#FF5050");
                p.CallAt = 0.05f;
                p.Line = _takesFirst;
                p.LineAt = 0.8f;
                p.AddFlight(0, 0.45f, BatterSpot + new Vector2(0.03f, 0), new Vector2(-0.32f, 0.22f), Wound(p.Kind), 0, 8, 1);
                p.AddMove(Catcher, new Vector2(-0.26f, 0.16f), 0.3f, 0.7f);
                p.AddMove(Catcher, Spots[Catcher], 1.0f, 1.5f);
                p.RunFrom = 0.8f;
                p.BatterFrom = 0.8f;
                p.PerBase = 0.75f;
                p.Finish = 1.85f;
                break;

            case 4:
                ChoreographHomeRun(p, dir);
                break;

            default:
                ChoreographHit(p, dir);
                break;
        }

        if (p.WalkOff || p.LastOfGame)
        {
            var settled = p.Outcome == 0
                ? (float.IsNaN(p.Caught) ? 0.15f : p.Caught) + 0.35f
                : LastArrival(p) + 0.25f;
            p.Mob = settled;
            p.Finish = Math.Max(p.Finish, settled + 0.4f);
            if (p.WalkOff)
            {
                p.Line = _walkOff;
                p.LineAt = settled;
            }
        }
    }

    private void ChoreographOut(Plate p, int dir)
    {
        p.CallColour = Color.White;
        switch (p.Kind)
        {
            case CMUSports.BaseballStrikeout:
                p.Call = _strikeout;
                p.CallAt = 0.02f;
                p.Caught = 0;
                p.AddFlight(0.32f, 0.62f, Mitt, Spots[Pitcher] - new Vector2(0, 0.04f), 14, 22, 10);
                p.BatterFrom = 0.4f;
                p.Finish = 0.7f;
                return;

            case CMUSports.BaseballGroundout:
            {
                var fielder = dir switch { 0 => Third, 1 => Shortstop, 2 => Pitcher, 3 => Second, _ => First };
                var spot = Spots[fielder];
                var meet = spot - Vector2.Normalize(spot) * (fielder == Pitcher ? 0.06f : 0.12f) + new Vector2(fielder == Pitcher ? 0.04f : 0, 0);
                var bag = BaseSpots[1] + new Vector2(0.01f, 0.03f);
                p.AddFlight(0, 0.36f, Zone, meet, 15, 3, 0, 3);
                p.AddMove(fielder, meet, 0.03f, 0.36f);
                if (fielder == First)
                {
                    p.AddMove(First, bag, 0.42f, 0.66f);
                    p.Caught = 0.66f;
                }
                else
                {
                    p.AddFlight(0.44f, 0.7f, meet, bag, 16, 16, 6);
                    p.AddMove(First, bag, 0.08f, 0.4f);
                    p.Caught = 0.7f;
                    p.AddMove(fielder, spot, 0.72f, 1.0f);
                }

                p.Catching = First;
                p.AddMove(First, Spots[First], 0.75f, 1.0f);
                p.Call = _out;
                p.CallAt = p.Caught;
                p.BatterFrom = 0.12f;
                p.PerBase = 0.7f;
                p.Finish = 1.0f;
                return;
            }

            case CMUSports.BaseballFlyout:
            {
                var fielder = dir switch { 0 or 1 => Left, 2 => Centre, _ => Right };
                var angle = dir switch { 0 => -30f, 1 => -17f, 2 => 0f, 3 => 17f, _ => 30f };
                var landing = Polar(angle, dir == 2 ? 3.0f : dir is 1 or 3 ? 2.85f : 2.6f);
                p.AddFlight(0, 0.8f, Zone, landing, 15, 10, 70);
                p.AddMove(fielder, landing, 0.05f, 0.74f);
                p.Catching = fielder;
                p.Caught = 0.8f;
                p.Watch = 0.8f;
                p.AddFlight(0.88f, 1.15f, landing, BaseSpots[2], 12, 14, 18);
                p.AddMove(Shortstop, BaseSpots[2] + new Vector2(-0.05f, -0.03f), 0.75f, 1.05f);
                p.AddMove(Shortstop, Spots[Shortstop], 1.15f, 1.3f);
                p.AddMove(fielder, Spots[fielder], 0.9f, 1.3f);
                p.Call = _caught;
                p.CallAt = 0.8f;
                p.BatterFrom = 0.12f;
                p.PerBase = 0.9f;
                p.Finish = 1.3f;
                return;
            }

            default:
            {
                var fielder = dir switch { 0 => Third, 1 => Shortstop, 2 => Catcher, 3 => Second, _ => First };
                var spot = Spots[fielder];
                var landing = fielder == Catcher ? new Vector2(0.1f, -0.14f) : spot - Vector2.Normalize(spot) * 0.15f;
                p.AddFlight(0, 0.8f, Zone, landing, 15, 12, 90);
                p.AddMove(fielder, landing, 0.08f, 0.65f);
                p.AddMove(fielder, spot, 0.85f, 1.05f);
                p.Catching = fielder;
                p.Caught = 0.8f;
                p.Watch = 0.8f;
                p.Call = _out;
                p.CallAt = 0.8f;
                p.BatterFrom = 0.12f;
                p.PerBase = 1f;
                p.Finish = 1.05f;
                return;
            }
        }
    }

    private void ChoreographHit(Plate p, int dir)
    {
        var bases = p.Outcome;
        var angle = dir switch { 0 => -36f, 1 => -18f, 2 => 0f, 3 => 18f, _ => 36f };
        if (bases == 3 && dir == 2)
            angle = p.Kind % 2 == 0 ? -22 : 22;

        var fence = Fence(angle) - 0.14f;
        Vector2 stop;
        float rolled;
        switch (p.Kind)
        {
            case 0:
            {
                // a grounder skips through the infield and rolls on
                var distance = MathF.Min(bases switch { 1 => 2.2f, 2 => 3.6f, _ => 3.9f }, fence);
                stop = Polar(angle, distance);
                rolled = 0.5f + 0.08f * bases;
                p.AddFlight(0, rolled, Zone, stop, 15, 0, 0, 4 + bases);
                break;
            }

            default:
            {
                var (land, arc, time) = p.Kind switch
                {
                    1 => (bases switch { 1 => 2.0f, 2 => 2.9f, _ => 3.2f }, 16f, 0.4f + 0.05f * bases),
                    2 => (bases switch { 1 => 2.3f, 2 => 3.3f, _ => 3.6f }, 50f, 0.7f + 0.05f * bases),
                    _ => (bases switch { 1 => 1.75f, 2 => 2.5f, _ => 3.0f }, 32f, 0.6f),
                };

                land = MathF.Min(land, fence);
                var on = land + bases switch { 1 => 0.3f, 2 => 0.55f, _ => 0.7f };
                // off the wall it comes back a little
                var distance = on > fence ? fence - 0.15f : on;
                var landing = Polar(angle, land);
                stop = Polar(angle + (on > fence ? 4 : 0), distance);
                p.AddFlight(0, time, Zone, landing, 15, 0, arc);
                p.AddFlight(time, time + 0.3f, landing, stop, 0, 0, 7, 2);
                p.Landed = time;
                p.Watch = time;
                rolled = time + 0.3f;
                break;
            }
        }

        var chaser = Left;
        var best = float.MaxValue;
        foreach (var fielder in new[] { Left, Centre, Right })
        {
            var distance = Vector2.Distance(Spots[fielder], stop);
            if (distance < best)
            {
                best = distance;
                chaser = fielder;
            }
        }

        var pick = rolled + 0.05f;
        p.AddMove(chaser, stop, 0.08f, pick);
        p.Catching = chaser;
        p.BatterFrom = 0.15f;
        p.PerBase = 0.5f;
        var arrive = p.BatterFrom + bases * p.PerBase;
        var target = bases == 3 ? 3 : 2;
        var receiver = target == 3 ? Third : angle < 0 ? Second : Shortstop;
        var bag = BaseSpots[target] + new Vector2(target == 3 ? 0.03f : 0, -0.03f);
        p.AddMove(receiver, bag, 0.2f, 0.8f);
        var thrown = pick + 0.12f + 0.4f;
        if (bases >= 2)
            thrown = Math.Max(thrown, arrive + 0.08f);

        p.AddFlight(pick + 0.12f, thrown, stop, bag, 14, 15, 22);
        p.Thrown = thrown;
        p.ThrowBase = target;
        p.Safe = bases >= 2 && thrown - arrive < 0.4f;
        if (bases >= 2)
            p.Slide = arrive - 0.14f;

        p.AddMove(chaser, Spots[chaser], thrown, thrown + 0.45f);
        p.AddMove(receiver, Spots[receiver], thrown + 0.1f, thrown + 0.5f);

        if (!float.IsNaN(p.Crash) || p.Runs > 0 && p.Has(3))
        {
            // the catcher stands up and sets himself on the plate for the throw that never comes
            p.AddMove(Catcher, new Vector2(0.03f, 0.02f), 0.25f, 0.5f);
            p.AddMove(Catcher, Spots[Catcher], LastArrival(p) + 0.75f, LastArrival(p) + 1.1f);
        }

        p.CrashBase = p.Before >= 4 ? 3 : p.Before >= 2 ? 2 : 1;
        p.Call = bases switch { 1 => _single, 2 => _double, _ => _triple };
        p.CallColour = Color.FromHex("#7CFF00");
        p.CallAt = 0.22f;
        if (p.Runs > 0)
        {
            p.Line = _runsScore[Math.Clamp(p.Runs, 1, _runsScore.Length - 1)];
            p.LineAt = LastArrival(p);
        }

        p.Finish = Math.Max(thrown + 0.5f, Math.Max(arrive + 0.35f, LastArrival(p) + 0.45f));
        if (!float.IsNaN(p.Crash))
            p.Finish = Math.Max(p.Finish, p.RunFrom + (4 - p.CrashBase) * p.PerBase + 0.9f);
    }

    private void ChoreographHomeRun(Plate p, int dir)
    {
        var angle = dir switch { 0 => -30f, 1 => -15f, 2 => 0f, 3 => 15f, _ => 30f };
        var carry = Fence(angle) + 0.7f + 0.15f * p.Kind;
        p.AddFlight(0, 1.2f, Zone, Polar(angle, carry), 15, 3, 68 + 10 * p.Kind);
        p.Landed = 1.2f;
        p.Watch = 1.2f;
        var chaser = angle < -8 ? Left : angle > 8 ? Right : Centre;
        p.AddMove(chaser, Polar(angle, Fence(angle) - 0.07f), 0.08f, 0.98f);
        p.AddMove(chaser, Spots[chaser], 1.8f, 2.6f);
        p.Catching = chaser;
        p.BatterFrom = 0.3f;
        p.RunFrom = 0.2f;
        p.PerBase = 0.55f;
        p.Call = _homer;
        p.CallColour = Color.Gold;
        p.CallAt = 0.3f;
        p.Line = p.Runs > 1 ? _runsScore[Math.Clamp(p.Runs, 1, _runsScore.Length - 1)] : string.Empty;
        p.LineAt = 1.2f;
        p.Distance = Loc.GetString("cmu-game-baseball-feet", ("feet", (int) (carry * 90)));
        p.Finish = p.BatterFrom + 4 * p.PerBase + 0.3f;
    }

    /// <summary>When the last runner home crosses the plate, or when the batter stops if nobody scores.</summary>
    private static float LastArrival(Plate p)
    {
        var last = 0f;
        for (var k = 0; k <= 3; k++)
        {
            if (!p.Has(k))
                continue;

            var bases = Bases(p, k);
            var from = k == 0 ? p.BatterFrom : p.RunFrom;
            if (bases > 0)
                last = Math.Max(last, from + bases * p.PerBase);
        }

        return last;
    }

    /// <summary>How many bases the runner who started on <paramref name="k"/> (0 the batter) takes on the play.</summary>
    private static int Bases(Plate p, int k)
    {
        switch (p.Outcome)
        {
            case 0:
                return 0;
            case 4:
                return 4 - k;
            case CMUSports.BaseballHitByPitch:
                // forced only when every bag behind him is full
                for (var b = 1; b <= k; b++)
                {
                    if ((p.Before & 1 << (b - 1)) == 0)
                        return 0;
                }

                return 1;
            default:
                return Math.Min(p.Outcome, 4 - k);
        }
    }

    /// <summary>How high the pitch hits the batter, by where: head, shoulder, ribs, leg.</summary>
    private static float Wound(int kind)
    {
        return kind switch { 0 => 27, 1 => 22, 2 => 17, _ => 8 };
    }

    #endregion

    #region Events

    protected override void OnEvent(CMUBetMatch match, CMUSportsEvent ev, int index, bool live)
    {
        var plate = index >= 0 && index < _plateOf.Length && _plateOf[index] >= 0 ? _plates[_plateOf[index]] : null;
        switch (ev.Action)
        {
            case CMUSportsAction.Bell:
                Sound(ChimeSound, -6);
                break;

            case CMUSportsAction.AtBat when plate != null:
                Contact(plate);
                break;

            case CMUSportsAction.Injury when plate != null:
                Hurt(match, plate, ev);
                break;

            case CMUSportsAction.Special when plate != null:
                if (ev.Value == CMUSports.BaseballBrokenBat)
                {
                    Sound(BrokenBatSound, 2);
                    var at = Air(Zone, 15);
                    Burst(at, 16, Wood, 130, 0.6f, 2.2f, -MathF.PI / 2, 2.6f, gravity: 380, floor: Project(BatterSpot).Y + 6);
                    Burst(at, 8, Color.FromHex("#E8D2A8"), 160, 0.25f, 1, streak: true);
                    Pop(_brokenBat, at - new Vector2(_brokenBat.Length * 3.1f, 22), Color.FromHex("#FFB050"), false, 1.1f, 14);
                }
                else if (ev.Value == CMUSports.BaseballFanHit)
                {
                    FanHit(plate);
                }
                else if (ev.Value == CMUSports.BaseballWalkOff)
                {
                    Sound(HornSound, 2);
                    SoundAny(WhistleSounds, -2);
                    BigCheer();
                    _excite = 1;
                }

                break;
        }
    }

    /// <summary>Bat on ball, or ball on batter.</summary>
    private void Contact(Plate plate)
    {
        if (plate.Outcome == CMUSports.BaseballHitByPitch)
        {
            var body = Air(BatterSpot + new Vector2(0.03f, 0), Wound(plate.Kind));
            SoundAny(BeanSounds, 3);
            SoundAny(MeatSounds, -2);
            Shake(3.5f, 0.3f);
            Flash(Color.White.WithAlpha(0.3f), 0.12f);
            Burst(body, 10, Color.White, 80, 0.2f, 1.2f, streak: true);
            return;
        }

        if (plate.Outcome == 0 && plate.Kind == CMUSports.BaseballStrikeout)
        {
            SoundAny(MittSounds, 0);
            Sound(SwooshSounds[0], -2, 0.8f);
            return;
        }

        var homer = plate.Outcome == 4;
        var at = Air(Zone, 15);
        if (!plate.Broken)
            Crack(homer ? 3 : plate.Outcome > 0 ? 1 : -1, homer ? 0.94f : 1);
        else
            Sound(KnockSound, -3);
        Burst(at, homer ? 14 : 7, Color.White, homer ? 140 : 90, 0.22f, 1.3f, streak: true);
        if (homer)
        {
            Shake(2.5f, 0.3f);
            Flash(Color.White.WithAlpha(0.28f), 0.15f);
            _excite = Math.Max(_excite, 0.6f);
        }
        else if (plate.Outcome > 0)
        {
            _excite = Math.Max(_excite, 0.45f);
        }
    }

    /// <summary>Someone bleeds. The pool lands whether or not anyone watched; the spray and the scream only live.</summary>
    private void Hurt(CMUBetMatch match, Plate plate, CMUSportsEvent ev)
    {
        var amount = Math.Clamp(ev.Extra, 1, 3);
        var female = CMUSports.Female(match.Sport, match.Entrants[Math.Clamp(ev.Side, 0, 1)]);
        Vector2 wound;
        Vector2 foot;
        var away = -MathF.PI / 2;
        switch (ev.Value)
        {
            case CMUSports.BaseballBatter:
                foot = BatterSpot;
                wound = Air(foot, Wound(plate.Kind));
                away = -MathF.PI * 0.8f;
                SoundAny(MeatSounds, 0);
                Pop("!", wound - new Vector2(0, 8), Color.FromHex("#FF5050"), true, 0.5f, 10);
                break;

            case CMUSports.BaseballPitcher or CMUSports.BaseballThirdBase:
            {
                var role = ev.Value == CMUSports.BaseballPitcher ? Pitcher : Third;
                foot = FielderBody(plate, 1 - plate.Side, role, ev.Time).Ground;
                wound = Air(foot, 23);
                away = -MathF.PI * 0.35f;
                Sound(PierceSound, 2);
                SoundAny(MeatSounds, 0);
                Shake(2, 0.25f);
                break;
            }

            case CMUSports.BaseballCatcher:
                foot = new Vector2(0.03f, -0.03f);
                wound = Air(foot, 16);
                away = MathF.PI * 1.15f;
                Sound(CrashSound, 4);
                SoundAny(BeanSounds, 2);
                Sound(SmackSound, 0);
                Shake(5.5f, 0.45f);
                Flash(Color.White.WithAlpha(0.35f), 0.15f);
                Pop(_collision, new Vector2(HomeX, 150), Color.FromHex("#FF5050"), true, 1.4f, 16);
                break;

            default:
                foot = new Vector2(-0.04f, 0.02f);
                wound = Air(foot, 12);
                away = -MathF.PI * 0.15f;
                break;
        }

        var floor = Project(foot);
        var near = Near(floor.Y);
        Splat(floor + new Vector2(0, 1), (8 + amount * 4) * near);
        for (var i = 0; i < amount; i++)
            Splat(floor + new Vector2((FxFloat() - 0.5f) * 18 * near, (FxFloat() - 0.3f) * 6 * near), (3 + FxFloat() * 3) * near, DarkBlood);

        if (ev.Value == CMUSports.BaseballBatter)
        {
            // he walks it off to first, dripping
            for (var i = 1; i <= 4; i++)
                Splat(Project(Vector2.Lerp(BatterSpot, BaseSpots[1], i / 5f)) + new Vector2(0, 1), (2.5f + FxFloat() * 2) * near);
        }

        Gush(wound, floor.Y + 2, amount, away);
        Sound(FallSound, 2);
        Scream(female, amount >= 3 ? 3 : 0);
    }

    /// <summary>A spray of blood from a wound: a few heavy drops that stain where they land, and a mist that does not.</summary>
    private void Gush(Vector2 at, float floor, int amount, float direction)
    {
        Burst(at, 4 + amount * 3, Blood, 60 + amount * 40, 1.3f, 1.8f + amount * 0.4f, direction, 1.7f, gravity: 420, floor: floor, stains: true);
        Burst(at, 6 + amount * 6, Blood, 50 + amount * 35, 0.5f, 1.3f, direction, 2.2f, gravity: 200, drag: 2.5f);
        Burst(at, 2 + amount, DarkBlood, 30 + amount * 20, 0.9f, 2.4f + amount * 0.5f, direction, 1.0f, gravity: 420, floor: floor, stains: true);
    }

    private void FanHit(Plate plate)
    {
        if (Seats.Length == 0)
            return;

        var index = Math.Clamp(plate.FanSeat, 0, Seats.Length - 1);
        var seat = Seats[index];
        _hurtFans[index] = true;
        var at = seat.At - new Vector2(0, seat.Size);
        Splat(seat.At + new Vector2(0, 2), 9 * Near(seat.At.Y) + 4);
        Splat(seat.At + new Vector2((FxFloat() - 0.5f) * 10, 4), 4, DarkBlood);
        Gush(at, seat.At.Y + 4, 2, -MathF.PI / 2);
        Sound(SmackSound, 2);
        SoundAny(BeanSounds, 0);
        Scream(FxFloat() < 0.4f, 0);
        Shake(1.2f, 0.2f);
        Pop(_fanDown, at - new Vector2(_fanDown.Length * 3.1f, 14), Color.FromHex("#FF5050"), false, 1.3f, 14);
    }

    #endregion

    #region Every frame

    protected override void UpdateMatch(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        _clock = (float) Timing.RealTime.TotalSeconds;
        var delta = Math.Clamp(_clock - _lastClock, 0, 0.1f);
        _lastClock = _clock;
        _excite = Math.Max(0, _excite - delta * 0.28f);

        var step = float.IsNaN(_lastT) ? -1 : t - _lastT;
        _cueing = step > 0 && step < 0.3f;
        _t = t;
        Array.Clear(_used);
        _ballOn = false;
        _batOn = false;
        _shardOn = false;
        _radar = string.Empty;

        _half = null;
        for (var i = _halves.Count - 1; i >= 0; i--)
        {
            if (_halves[i].Start <= t)
            {
                _half = _halves[i];
                break;
            }
        }

        _plate = null;
        if (_half != null)
        {
            for (var n = _half.First; n < _plates.Count && _plates[n].Half == _half && _plates[n].Start <= t; n++)
                _plate = _plates[n];
        }

        var batting = _half?.Side ?? 1;
        var fielding = 1 - batting;
        _bases = _plate == null ? 0 : t >= _plate.Contact + 0.4f * _plate.Pace ? _plate.After : _plate.Before;
        _outs = _plate == null ? 0 : t >= _plate.Contact ? _plate.Outs : _plate.Outs - (_plate.Outcome == 0 ? 1 : 0);

        for (var role = 0; role <= Right; role++)
            Stand(fielding, role, FielderBody(_plate, fielding, role, t));

        PoseBatters(batting, t);
        PoseBall(t);
        PoseBat(t);
        PoseShard(t);
        if (_cueing)
            Cues(t);

        Scoreboard(t);
        Calls(t);

        for (var i = 0; i < 2; i++)
        {
            for (var role = 0; role < Roles; role++)
            {
                var doll = _dolls[i, role];
                if (!_used[doll])
                    Hide(doll);
            }
        }

        _lastT = t;
    }

    /// <summary>Poses a doll standing on a spot of the field, scaled for how far away it is.</summary>
    private void Stand(int side, int role, Body body)
    {
        if (body.Alpha <= 0.01f)
            return;

        var doll = _dolls[side, role];
        var at = Project(body.Ground);
        var near = Near(at.Y);
        var scale = MathF.Round(1.05f * MathF.Pow(Math.Max(near, 0.2f), 0.7f) * 20) / 20;
        var squash = body.Squash <= 0 ? 1 : body.Squash;
        var size = new Vector2(scale, scale * squash);
        var view = DollView(doll);
        if (_scales[doll] != size)
        {
            _scales[doll] = size;
            view.Scale = size;
            view.SetSize = new Vector2(32 * scale, 32 * scale);
        }

        var alpha = MathF.Round(Math.Clamp(body.Alpha, 0, 1) * 20) / 20;
        if (MathF.Abs(_alphas[doll] - alpha) > 0.01f)
        {
            _alphas[doll] = alpha;
            view.Modulate = Color.White.WithAlpha(alpha);
        }

        // the feet stay on the ground: a standing doll's feet sit 14 px under its middle, a lying one's 4
        var drop = (14 * squash * (1 - body.Lie) + 4 * body.Lie) * scale;
        var rotation = body.Rotation + body.Lie * 90;
        Pose(doll, at.X, at.Y - drop - body.Lift * scale, body.Facing, rotation);
        _used[doll] = true;
        _feet[doll] = at;
    }

    private static Body Standing(Vector2 ground, Direction facing)
    {
        return new Body { Ground = ground, Facing = facing, Squash = 1, Alpha = 1 };
    }

    private static float Smooth(float x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    private static Direction Facing(Vector2 from, Vector2 to)
    {
        var a = Project(from);
        var b = Project(to);
        var d = b - a;
        if (MathF.Abs(d.X) > MathF.Abs(d.Y))
            return d.X > 0 ? Direction.East : Direction.West;

        return d.Y > 0 ? Direction.South : Direction.North;
    }

    /// <summary>The bounce of a running stride.</summary>
    private float Stride(float phase = 0)
    {
        return MathF.Abs(MathF.Sin(_clock * 17 + phase)) * 1.6f;
    }

    /// <summary>A point along the base paths: 0 home, 1 first, 2 second, 3 third, 4 home again.</summary>
    private static Vector2 Path(float u)
    {
        u = Math.Clamp(u, 0, 4);
        var leg = Math.Min((int) u, 3);
        return Vector2.Lerp(BaseSpots[leg], BaseSpots[(leg + 1) % 4], u - leg);
    }

    private Vector2 Air(Vector2 ground, float height)
    {
        var at = Project(ground);
        return at - new Vector2(0, height * Near(at.Y));
    }

    private Body FielderBody(Plate? plate, int side, int role, float t)
    {
        var spot = Spots[role];
        var body = Standing(spot, role == Catcher ? Direction.North : Direction.South);
        var half = plate?.Half ?? _half;

        // a new half: the side takes the field from its dugout
        if (plate == null)
        {
            var jog = Arrival(half, t);
            if (jog < 1)
            {
                body.Ground = Vector2.Lerp(Dugouts[side], spot, jog);
                body.Facing = Facing(Dugouts[side], spot);
                body.Lift = Stride(role);
                return body;
            }

            if (role == Catcher)
                body.Squash = 0.74f;

            return body;
        }

        if (role == Catcher)
            body.Squash = 0.74f;
        else if (t >= plate.Release - 0.3f && t < plate.Contact + 0.15f)
            body.Squash = 0.93f;

        if (role == Pitcher)
            Delivery(plate, t, ref body);

        if (t >= plate.Contact)
            Aftermath(plate, role, t, ref body);

        if (plate.LastOfGame && !float.IsNaN(plate.Mob))
        {
            var mob = plate.Real(plate.Mob);
            if (!plate.WalkOff)
            {
                // the side in the field won: everyone piles onto the mound
                var f = Smooth((t - mob) / 0.8f);
                if (f > 0)
                {
                    var slot = Spots[Pitcher] + Polar(role * 40, role == Pitcher ? 0 : 0.14f);
                    body.Ground = Vector2.Lerp(body.Ground, slot, f);
                    body.Facing = f < 1 ? Facing(spot, slot) : role % 2 == 0 ? Direction.South : Direction.East;
                    body.Lift = f < 1 ? Stride(role) : MathF.Abs(MathF.Sin(_clock * 7 + role)) * 6;
                    body.Squash = 1;
                    body.Lie = 0;
                    body.Rotation = 0;
                }
            }
            else
            {
                Leave(ref body, side, Smooth((t - mob - 0.3f) / 1.6f), role);
            }
        }
        else if (plate.LastOfHalf)
        {
            Leave(ref body, side, Smooth((t - plate.Real(plate.Finish) - 0.1f) / 1.1f), role);
        }

        return body;
    }

    /// <summary>
    /// How far a side has come out of its dugout at the start of a half, 0 to 1. The first side takes the field
    /// before the first bell, while the crowd settles.
    /// </summary>
    private float Arrival(Half? half, float t)
    {
        if (!_hasPlay)
            return 1;

        if (half == null || _halves.Count > 0 && half == _halves[0])
            return Smooth((t - 0.2f) / 2.2f);

        return Smooth((t - half.Start) / Math.Clamp((half.Intro - half.Start) * 0.85f, 0.3f, 1.1f));
    }

    /// <summary>Jogs a player off to his dugout, fading as he goes in.</summary>
    private void Leave(ref Body body, int side, float f, int phase)
    {
        if (f <= 0)
            return;

        var from = body.Ground;
        body.Ground = Vector2.Lerp(from, Dugouts[side], f);
        body.Facing = Facing(from, Dugouts[side]);
        body.Lift = f < 1 ? Stride(phase) : 0;
        body.Squash = 1;
        body.Lie = 0;
        body.Rotation = 0;
        body.Alpha = Math.Min(body.Alpha, f < 0.7f ? 1 : (1 - f) / 0.3f);
    }

    /// <summary>The pitcher's windup: set, leg kick facing third, stride and follow-through.</summary>
    private static void Delivery(Plate plate, float t, ref Body body)
    {
        var foul = plate.Fouled && t < plate.Windup;
        var windup = foul ? plate.FoulWindup : plate.Windup;
        var release = foul ? plate.FoulRelease : plate.Release;
        if (t < windup)
            return;

        var set = windup + (release - windup) * 0.3f;
        var kick = release - 0.12f;
        if (t < set)
        {
            body.Facing = Direction.South;
            body.Squash = 0.97f;
        }
        else if (t < kick)
        {
            var f = (t - set) / Math.Max(0.01f, kick - set);
            body.Facing = Direction.West;
            body.Lift = 4.5f * MathF.Sin(MathF.PI * f);
            body.Rotation = -6 * f;
        }
        else if (t < release)
        {
            var f = (t - kick) / 0.12f;
            body.Facing = Direction.South;
            body.Ground.Y -= 0.05f * f;
            body.Rotation = 12 * f;
            body.Squash = 1 - 0.08f * f;
        }
        else
        {
            var f = Math.Clamp((t - release) / 0.45f, 0, 1);
            body.Facing = Direction.South;
            body.Ground.Y -= 0.05f * (1 - Smooth((t - release - 0.6f) / 0.6f));
            body.Rotation = 14 * (1 - f);
            body.Squash = 0.9f + 0.1f * f;
        }
    }

    /// <summary>What a fielder does once the ball is in play.</summary>
    private void Aftermath(Plate plate, int role, float t, ref Body body)
    {
        var tau = (t - plate.Contact) / plate.Pace;
        var ground = body.Ground;
        var moving = false;
        for (var i = 0; i < plate.MoveCount; i++)
        {
            ref var move = ref plate.Moves[i];
            if (move.Role != role || tau <= move.From)
                continue;

            var f = Smooth((tau - move.From) / Math.Max(0.01f, move.By - move.From));
            if (f < 1)
            {
                body.Facing = Facing(ground, move.To);
                moving = true;
            }

            ground = Vector2.Lerp(ground, move.To, f);
        }

        body.Ground = ground;
        if (moving)
        {
            body.Lift = Stride(role);
            body.Squash = 1;
        }
        else if (!float.IsNaN(plate.Watch) && tau < plate.Watch && role != Catcher)
        {
            // everyone turns to watch a ball in the air
            body.Facing = Direction.North;
            body.Squash = 1;
        }

        // the catcher comes out of his crouch once the ball is in play, or to throw it back after a strikeout
        if (role == Catcher)
        {
            var strikeout = plate.Outcome == 0 && plate.Kind == CMUSports.BaseballStrikeout;
            body.Squash = Math.Max(body.Squash, 0.74f + 0.26f * Smooth(strikeout ? (tau - 0.3f) / 0.2f : tau / 0.25f));
        }

        if (role == plate.Catching && !float.IsNaN(plate.Caught) && MathF.Abs(tau - plate.Caught) < 0.12f)
            body.Lift = Math.Max(body.Lift, 2.5f);

        // the outfielder leaps at the wall for a ball he will never reach
        if (plate.Outcome == 4 && role == plate.Catching && tau > 0.98f && tau < 1.3f)
        {
            body.Lift = 7 * MathF.Sin(MathF.PI * (tau - 0.98f) / 0.32f);
            body.Facing = Direction.North;
        }

        // a shard of the bat drops the pitcher or the third baseman for a moment
        if (plate.Broken && !float.IsNaN(plate.Shard) && t >= plate.Shard &&
            role == (plate.ShardVictim == CMUSports.BaseballPitcher ? Pitcher : Third))
        {
            var down = t - plate.Shard;
            body.Lie = down < 0.12f ? down / 0.12f : 1 - Smooth((down - 0.9f) / 0.35f);
            body.Rotation = -8 * body.Lie;
            body.Lift = 0;
        }

        // flattened at the plate by the runner
        if (role == Catcher && !float.IsNaN(plate.Crash) && t >= plate.Crash)
        {
            var down = t - plate.Crash;
            body.Ground += new Vector2(0.03f, -0.08f) * Smooth(down / 0.15f);
            body.Lie = down < 0.1f ? down / 0.1f : 1 - Smooth((down - 0.75f) / 0.3f);
            body.Rotation = -180 * body.Lie;
            body.Facing = Direction.North;
            body.Squash = 1;
            body.Lift = 0;
        }
    }

    private void PoseBatters(int side, float t)
    {
        var plate = _plate;
        var half = _half;
        if (plate == null)
        {
            // a new half: the leadoff hitter walks up from the dugout
            var walk = Arrival(half, t);
            var body = Standing(Vector2.Lerp(Dugouts[side], BatterSpot, walk), walk < 1 ? Facing(Dugouts[side], BatterSpot) : Direction.East);
            if (walk < 1)
                body.Lift = Stride();

            body.Squash = walk >= 1 ? 0.95f : 1;
            Stand(side, Batter, body);
            Stand(side, OnDeck, Standing(Circles[side], side == 0 ? Direction.West : Direction.East));
            return;
        }

        if (t < plate.Contact)
        {
            var body = Standing(BatterSpot, Direction.East);
            body.Squash = 0.95f;
            if (plate.Outcome == CMUSports.BaseballHitByPitch && t > plate.Contact - 0.07f)
            {
                body.Facing = Direction.South;
                body.Rotation = -12;
            }

            Stand(side, Batter, body);
            // runners take a lead as the pitcher comes set
            var lead = 0.03f + 0.06f * Smooth((t - plate.Windup) / Math.Max(0.1f, plate.Release - plate.Windup));
            for (var k = 1; k <= 3; k++)
            {
                if (plate.Has(k))
                    Stand(side, Batter + k, Standing(Path(k + lead), Direction.South));
            }

            Stand(side, OnDeck, Standing(Circles[side], side == 0 ? Direction.West : Direction.East));
            return;
        }

        var tau = (t - plate.Contact) / plate.Pace;
        var done = plate.Real(plate.Finish);
        for (var k = 0; k <= 3; k++)
        {
            if (!plate.Has(k))
                continue;

            var body = Runner(plate, side, k, t, tau);
            if (plate.LastOfHalf && !plate.WalkOff)
                Leave(ref body, side, Smooth((t - done - 0.1f) / 1.1f), k);

            if (plate.WalkOff)
                Celebrate(plate, ref body, k, t);

            Stand(side, Batter + k, body);
        }

        // the next hitter walks up to the plate as the play winds down
        var deck = Standing(Circles[side], side == 0 ? Direction.West : Direction.East);
        if (!plate.LastOfHalf)
        {
            var walk = Smooth((t - (plate.End - 0.55f)) / 0.5f);
            if (walk > 0)
            {
                deck.Ground = Vector2.Lerp(Circles[side], BatterSpot, walk);
                deck.Facing = walk < 1 ? Facing(Circles[side], BatterSpot) : Direction.East;
            }
        }
        else if (plate.WalkOff)
        {
            Celebrate(plate, ref deck, 4, t);
        }
        else
        {
            Leave(ref deck, side, Smooth((t - done) / 1.0f), 4);
        }

        Stand(side, OnDeck, deck);
    }

    /// <summary>The batting side mobs the plate after a walk-off.</summary>
    private void Celebrate(Plate plate, ref Body body, int index, float t)
    {
        var f = Smooth((t - plate.Real(plate.Mob)) / 0.7f);
        if (f <= 0)
            return;

        var slot = new Vector2(0, 0.08f) + Polar(index * 72 + 20, 0.13f);
        body.Ground = Vector2.Lerp(body.Ground, slot, f);
        body.Alpha = 1;
        body.Lie = 0;
        body.Rotation = 0;
        body.Squash = 1;
        body.Facing = index % 2 == 0 ? Direction.South : Direction.West;
        body.Lift = f < 1 ? Stride(index) : MathF.Abs(MathF.Sin(_clock * 7.5f + index * 1.3f)) * 6;
    }

    /// <summary>Where the runner who started on <paramref name="k"/> (0 the batter) is during the play.</summary>
    private Body Runner(Plate plate, int side, int k, float t, float tau)
    {
        var body = Standing(k == 0 ? BatterSpot : Path(k + 0.09f), k == 0 ? Direction.East : Direction.South);
        var dugout = Dugouts[side];
        if (plate.Outcome == 0)
        {
            if (k > 0)
            {
                // runners hold, drifting back to the bag
                body.Ground = Path(k + 0.09f * (1 - Smooth(tau / 0.5f)));
                return body;
            }

            if (plate.Kind == CMUSports.BaseballStrikeout)
            {
                var f = Smooth((tau - plate.BatterFrom) / 1.1f);
                body.Ground = Vector2.Lerp(BatterSpot, dugout, f);
                if (f > 0)
                {
                    body.Facing = Facing(BatterSpot, dugout);
                    body.Lift = f < 1 ? Stride() * 0.5f : 0;
                }

                body.Alpha = f < 0.6f ? 1 : (1 - f) / 0.4f;
                return body;
            }

            var outAt = float.IsNaN(plate.Caught) ? 0.6f : plate.Caught;
            var run = Math.Min(tau, outAt + 0.1f);
            var u = Math.Clamp((run - plate.BatterFrom) / plate.PerBase, 0, 1.05f);
            body.Ground = Vector2.Lerp(BatterSpot, Path(u), Smooth(u * 4));
            body.Facing = Direction.East;
            body.Lift = tau > plate.BatterFrom && tau < outAt + 0.1f ? Stride() : 0;
            var off = Smooth((tau - outAt - 0.15f) / 1.1f);
            if (off > 0)
            {
                var from = body.Ground;
                body.Ground = Vector2.Lerp(from, dugout, off);
                body.Facing = Facing(from, dugout);
                body.Lift = off < 1 ? Stride() * 0.6f : 0;
                body.Alpha = off < 0.6f ? 1 : (1 - off) / 0.4f;
            }

            return body;
        }

        var bases = Bases(plate, k);
        var start = k == 0 ? plate.BatterFrom : plate.RunFrom;
        var progress = bases == 0 ? 0 : Math.Clamp((tau - start) / plate.PerBase, 0, bases);
        var at = k + progress;
        var arrive = start + bases * plate.PerBase;

        if (k == 0 && plate.Outcome == CMUSports.BaseballHitByPitch)
        {
            // down in the box, up again, then the long walk to first holding his head
            var fall = Math.Clamp(tau / 0.15f, 0, 1) * (1 - Smooth((tau - 0.58f) / 0.2f));
            body.Ground = Vector2.Lerp(BatterSpot, Path(at), Smooth(at * 3));
            body.Lie = fall;
            body.Rotation = -10 * fall;
            body.Facing = fall > 0.5f ? Direction.South : Direction.East;
            if (fall < 0.5f && progress > 0 && progress < bases)
                body.Lift = Stride() * 0.4f;

            return body;
        }

        body.Ground = k == 0 ? Vector2.Lerp(BatterSpot, Path(at), Smooth(at * 4)) : Path(at < k + 0.09f && progress <= 0 ? k + 0.09f : at);
        var leg = Math.Min((int) at, 3);
        if (progress > 0 && progress < bases)
        {
            body.Facing = PathFacing[leg];
            body.Lift = Stride(k);
        }

        // feet first into the bag
        if (k == 0 && !float.IsNaN(plate.Slide) && tau > plate.Slide && tau < arrive + 0.3f)
        {
            body.Lie = 0.85f * (1 - Smooth((tau - arrive - 0.05f) / 0.25f));
            body.Lift = 0;
            // lying turns the head left, so a slide towards second or third turns right round to lead with the feet
            var into = (int) (at - 0.01f);
            body.Rotation = into is 1 or 2 ? 180 : 0;
        }

        // the lead runner dives into the catcher
        if (!float.IsNaN(plate.Crash) && k == plate.CrashBase)
        {
            var crash = plate.Crash;
            if (t > crash - 0.12f * plate.Pace)
            {
                var down = t - crash;
                body.Lie = down < 0 ? 1 + down / (0.12f * plate.Pace) : 1 - Smooth((down - 0.8f) / 0.3f);
                // head first, towards the plate on his right
                body.Rotation = -180 * body.Lie;
                body.Facing = Direction.South;
                body.Lift = 0;
                body.Ground = new Vector2(-0.04f, 0.02f);
                if (down > 1.1f)
                {
                    var f = Smooth((down - 1.1f) / 1.0f);
                    body.Ground = Vector2.Lerp(body.Ground, dugout, f);
                    body.Facing = Facing(BaseSpots[0], dugout);
                    body.Lift = f < 1 ? Stride() * 0.5f : 0;
                    body.Alpha = f < 0.6f ? 1 : (1 - f) / 0.4f;
                }
            }

            return body;
        }

        // a run in: he trots off to the dugout to the high fives
        if (k + bases >= 4 && tau > arrive && !plate.WalkOff)
        {
            var f = Smooth((tau - arrive - 0.1f) / 0.9f);
            body.Ground = Vector2.Lerp(BaseSpots[0], dugout, f);
            body.Facing = Facing(BaseSpots[0], dugout);
            body.Lift = f < 1 ? Stride(k) * 0.6f : 0;
            body.Alpha = f < 0.6f ? 1 : (1 - f) / 0.4f;
        }
        else if (progress >= bases && bases > 0)
        {
            body.Facing = Direction.South;
        }

        return body;
    }

    #endregion

    #region The ball and the bat

    private void PoseBall(float t)
    {
        if (!BallAt(_plate, t, out var ground, out var height))
            return;

        _ballOn = true;
        _ballShadow = Project(ground);
        var near = Near(_ballShadow.Y);
        _ballHeight = height;
        _ballAt = _ballShadow - new Vector2(0, height * near);
        _ballSize = 1.3f + 1.1f * near + Math.Min(height, 120) * 0.006f;
        _trailCount = 0;
        for (var k = 1; k <= _trail.Length; k++)
        {
            if (!BallAt(_plate, t - k * 0.018f, out var g, out var h))
                break;

            var shadow = Project(g);
            _trail[_trailCount] = shadow - new Vector2(0, h * Near(shadow.Y));
            _trailSize[_trailCount] = _ballSize * (1 - k * 0.12f);
            _trailCount++;
        }
    }

    private bool BallAt(Plate? plate, float t, out Vector2 ground, out float height)
    {
        ground = default;
        height = 0;
        if (plate == null || t < plate.Start)
            return false;

        if (plate.Fouled && t < plate.Windup)
        {
            if (t < plate.Foul)
                return Pitched(plate.FoulRelease, plate.Foul, Zone, 15, t, out ground, out height);

            if (t >= plate.FanTime)
                return false;

            // fouled back over the dugout into the seats
            var f = (t - plate.Foul) / Math.Max(0.05f, plate.FanTime - plate.Foul);
            var seat = Seats[Math.Clamp(plate.FanSeat, 0, Seats.Length - 1)];
            ground = Vector2.Lerp(Zone, Unproject(seat.At), f);
            height = 15 * (1 - f) + seat.Size * 2 * f + 60 * 4 * f * (1 - f);
            return true;
        }

        if (t < plate.Contact)
        {
            if (plate.Outcome == CMUSports.BaseballHitByPitch)
                return Pitched(plate.Release, plate.Contact, BatterSpot + new Vector2(0.03f, 0), Wound(plate.Kind), t, out ground, out height);

            if (plate.Outcome == 0 && plate.Kind == CMUSports.BaseballStrikeout)
                return Pitched(plate.Release, plate.Contact, Mitt, 11, t, out ground, out height);

            return Pitched(plate.Release, plate.Contact, Zone, 15, t, out ground, out height);
        }

        var tau = (t - plate.Contact) / plate.Pace;
        for (var i = 0; i < plate.Flights; i++)
        {
            ref var flight = ref plate.Ball[i];
            if (tau < flight.From || tau >= flight.To)
                continue;

            var f = (tau - flight.From) / Math.Max(0.01f, flight.To - flight.From);
            ground = Vector2.Lerp(flight.A, flight.B, f);
            height = flight.HeightA + (flight.HeightB - flight.HeightA) * f + flight.Arc * 4 * f * (1 - f);
            if (flight.Hops > 0)
                height += MathF.Abs(MathF.Sin(MathF.PI * f * flight.Hops)) * 7 * (1 - f);

            return true;
        }

        return false;
    }

    private static bool Pitched(float release, float arrive, Vector2 target, float targetHeight, float t, out Vector2 ground, out float height)
    {
        ground = default;
        height = 0;
        if (t < release)
            return false;

        var f = Math.Clamp((t - release) / Math.Max(0.05f, arrive - release), 0, 1);
        ground = Vector2.Lerp(Hand, target, f);
        height = 25 + (targetHeight - 25) * f - 3 * f * (1 - f);
        return true;
    }

    /// <summary>The bat: waggling in the stance, swinging through the zone, dropped or flipped after contact.</summary>
    private void PoseBat(float t)
    {
        var plate = _plate;
        var batter = _dolls[_half?.Side ?? 1, Batter];
        if (!_used[batter])
            return;

        var foot = _feet[batter];
        var near = Near(foot.Y);
        var hands = foot + new Vector2(5, -16) * near;
        _batSize = 22 * near;
        _batAt = hands;
        _batAngle = -118 + 5 * MathF.Sin(_clock * 3.1f);
        _batOn = true;
        if (plate == null || t < plate.Start)
            return;

        var strikeout = plate.Outcome == 0 && plate.Kind == CMUSports.BaseballStrikeout;
        if (t >= plate.Contact && !strikeout)
        {
            // after contact the bat is dropped, flipped or in pieces
            var tau = (t - plate.Contact) / plate.Pace;
            var lying = Project(BatterSpot + new Vector2(0.07f, -0.03f));
            if (plate.Broken)
            {
                _batOn = false;
            }
            else if (plate.Outcome == 4 && tau < 0.55f)
            {
                var f = Math.Clamp((tau - 0.1f) / 0.45f, 0, 1);
                _batAt = Vector2.Lerp(hands, lying, f) - new Vector2(0, 20 * near * 4 * f * (1 - f));
                _batAngle = -510 + 720 * f;
            }
            else if (tau > plate.BatterFrom * 0.6f || plate.Outcome == CMUSports.BaseballHitByPitch)
            {
                _batAt = lying;
                _batAngle = 8;
            }
            else
            {
                _batAngle = -510;
            }

            return;
        }

        if (strikeout && t >= plate.Contact && (t - plate.Contact) / plate.Pace > plate.BatterFrom)
        {
            // he takes it back to the dugout with him
            _batOn = false;
            return;
        }

        var foul = plate.Fouled && t < plate.Windup;
        var pitch = foul ? plate.Foul : plate.Contact;
        if ((foul || plate.Outcome != CMUSports.BaseballHitByPitch) && t > pitch - 0.14f)
            _batAngle = Swing(t - pitch);
        else if (t > pitch - 0.5f)
            _batAngle = -128;
    }

    /// <summary>The bat shard spinning out to whoever it hits.</summary>
    private void PoseShard(float t)
    {
        var plate = _plate;
        if (plate == null || !plate.Broken || float.IsNaN(plate.Shard) || t <= plate.Contact || t >= plate.Shard)
            return;

        var f = (t - plate.Contact) / (plate.Shard - plate.Contact);
        var victim = plate.ShardVictim == CMUSports.BaseballPitcher ? Pitcher : Third;
        var to = Air(FielderBody(plate, 1 - plate.Side, victim, plate.Shard).Ground, 23);
        var from = Air(Zone, 15);
        _shardOn = true;
        _shardAt = Vector2.Lerp(from, to, f) - new Vector2(0, 14 * 4 * f * (1 - f));
        _shardAngle = f * 1500;
        _shardSize = 16 * Near(_shardAt.Y);
    }

    /// <summary>The barrel's angle on screen through a swing, by seconds from the ball reaching the plate.</summary>
    private static float Swing(float s)
    {
        ReadOnlySpan<float> times = stackalloc[] { -0.14f, -0.05f, 0, 0.07f, 0.18f };
        ReadOnlySpan<float> angles = stackalloc[] { -128f, -225f, -360f, -450f, -510f };
        if (s <= times[0])
            return angles[0];

        for (var i = 1; i < times.Length; i++)
        {
            if (s <= times[i])
                return angles[i - 1] + (angles[i] - angles[i - 1]) * (s - times[i - 1]) / (times[i] - times[i - 1]);
        }

        return angles[^1];
    }

    #endregion

    #region Moments

    /// <summary>Plays the moments of the current play that fall in this frame: throws, catches, slides, runs, fireworks.</summary>
    private void Cues(float t)
    {
        var plate = _plate;
        if (_hasPlay && Crossed(0.4f))
            SoundAny(ClapSounds, -6);

        if (plate == null)
            return;

        if (plate.Fouled && Crossed(plate.FoulRelease) || Crossed(plate.Release))
            SoundAny(SwooshSounds, -6);

        if (plate.Fouled && Crossed(plate.Foul - 0.1f) || plate.Outcome != CMUSports.BaseballHitByPitch && Crossed(plate.Contact - 0.1f))
            SoundAny(SwooshSounds, -9, 0.15f);

        // throws back to the infield, to first, to the bag
        for (var i = 1; i < plate.Flights; i++)
        {
            ref var flight = ref plate.Ball[i];
            if (flight.HeightA > 5 && Crossed(plate.Real(flight.From)))
                SoundAny(SwooshSounds, -9);
        }

        if (plate.Fouled && Crossed(plate.Foul))
        {
            Crack(-3, 1.05f);
            Burst(Air(Zone, 15), 6, Color.White, 80, 0.2f, 1.2f, streak: true);
        }

        if (!float.IsNaN(plate.Landed) && Crossed(plate.Real(plate.Landed)))
        {
            if (plate.Outcome == 4)
                HomeRunLands(plate);
            else
                Landing(plate);
        }

        if (!float.IsNaN(plate.Caught) && plate.Caught > 0 && Crossed(plate.Real(plate.Caught)))
        {
            SoundAny(MittSounds, -3);
            if (plate.LastOfGame || plate.Outs == 3)
                SoundAny(ClapSounds, -5);
        }

        if (!float.IsNaN(plate.Thrown) && Crossed(plate.Real(plate.Thrown)))
        {
            SoundAny(MittSounds, -5);
            if (plate.Safe)
                Pop(_safe, Air(BaseSpots[plate.ThrowBase], 34) - new Vector2(_safe.Length * 3.1f, 0), Color.FromHex("#7CFF00"), false, 1.0f, 12);
        }

        if (!float.IsNaN(plate.Slide) && Crossed(plate.Real(plate.Slide + 0.1f)))
        {
            SoundAny(SlideSounds, 2);
            Sound(SwooshSounds[2], -6, 0.7f);
            var bag = Project(BaseSpots[Math.Min(plate.Outcome, 3)]);
            Burst(bag, 22, Color.FromHex("#C9A27A"), 55, 0.8f, 2.6f, -MathF.PI / 2, 2.8f, gravity: 40, drag: 2.5f);
        }

        // runs crossing the plate
        for (var k = 0; k <= 3; k++)
        {
            if (!plate.Has(k) || k + Bases(plate, k) < 4)
                continue;

            var from = k == 0 ? plate.BatterFrom : plate.RunFrom;
            if (Crossed(plate.Real(from + (4 - k) * plate.PerBase)))
            {
                SmallCheer();
                _excite = Math.Max(_excite, 0.6f);
                if (k == 0 || plate.Outcome != 4)
                    SoundAny(WhistleSounds, -8);
            }
        }

        // late in the game, with the home side threatening, the organ calls the charge
        if (plate.Charge && Crossed(Math.Min(plate.Real(plate.Finish), plate.End - 0.2f)) && _clock >= _cheerUntil)
            Sound(ChargeSound, -2);

        // a beaned batter drips all the way to first
        if (plate.Outcome == CMUSports.BaseballHitByPitch)
        {
            for (var i = 0; i < 6; i++)
            {
                if (!Crossed(plate.Real(0.85f + i * 0.13f)))
                    continue;

                var batter = _dolls[plate.Side, Batter];
                var foot = _feet[batter];
                var near = Near(foot.Y);
                Burst(foot - new Vector2(0, 26 * near), 2, Blood, 20, 1, 2, MathF.PI / 2, 1, gravity: 300, floor: foot.Y + 1, stains: true);
            }
        }

        if (!float.IsNaN(plate.Mob))
        {
            var mob = plate.Real(plate.Mob);
            if (Crossed(mob))
            {
                SoundAny(ClapSounds, -1);
                SoundAny(WhistleSounds, -3);
                _excite = 1;
                if (!plate.WalkOff)
                {
                    Sound(HornSound, -2);
                    SmallCheer();
                }
            }

            for (var i = 0; i < 6; i++)
            {
                if (Crossed(mob + 0.2f + i * 0.45f))
                    Firework(i);
            }
        }
    }

    private void Crack(float volume, float pitch)
    {
        Sound(HitSound, volume, pitch * (0.97f + FxFloat() * 0.06f));
    }

    /// <summary>A big cheer, unless one is still going: they are long, and stacked they only get louder.</summary>
    private void BigCheer()
    {
        if (!Live || _clock < _cheerUntil)
            return;

        SoundAny(CheerSounds, -2, 0.03f);
        _cheerUntil = _clock + 14;
    }

    /// <summary>A short cheer for a run, kept under a big one and not stacked on itself.</summary>
    private void SmallCheer()
    {
        if (!Live || _clock < _cheerUntil || _clock < _smallUntil)
            return;

        Sound(SmallCheerSound, -3);
        _smallUntil = _clock + 3;
    }

    private bool Crossed(float time)
    {
        return _lastT < time && time <= _t;
    }

    private void Landing(Plate plate)
    {
        Sound(BounceSound, -4);
        var at = Project(plate.Ball[0].B);
        Burst(at, 6, Color.FromHex("#6FAF5A"), 30, 0.4f, 1.6f, -MathF.PI / 2, 2, gravity: 120);
    }

    /// <summary>Gone: into the seats, the crowd up, the horn, and fireworks over the stands.</summary>
    private void HomeRunLands(Plate plate)
    {
        _excite = 1;
        Sound(HornSound, 0);
        BigCheer();
        SoundAny(WhistleSounds, -3);
        var at = Project(plate.Ball[0].B);
        Burst(at, 12, Color.White, 50, 0.5f, 1.4f, -MathF.PI / 2, 2.4f, gravity: 160);
        for (var i = 0; i < 3; i++)
            Firework(i + plate.Number);
    }

    private void Firework(int i)
    {
        var at = new Vector2(40 + FxFloat() * (ArenaWidth - 80), Bar + 8 + FxFloat() * 22);
        var colour = FireworkColours[(i % FireworkColours.Length + FireworkColours.Length) % FireworkColours.Length];
        Burst(at + new Vector2(0, 30), 3, Color.FromHex("#FFE6A0"), 120, 0.25f, 1, -MathF.PI / 2, 0.15f, streak: true);
        Burst(at, 34, colour, 95, 1.1f, 1.7f, gravity: 45, streak: true, drag: 1.4f);
        Burst(at, 12, Color.White, 45, 0.6f, 1.2f, gravity: 30, drag: 2);
        SoundAny(FireworkSounds, -10, 0.2f);
    }

    #endregion

    #region Text

    private void Scoreboard(float t)
    {
        Array.Clear(_line);
        Array.Clear(_played);
        Array.Clear(_runs);
        Array.Clear(_hits);
        foreach (var half in _halves)
        {
            if (half.Start <= t)
                _played[half.Side, half.Inning - 1] = true;
        }

        foreach (var plate in _plates)
        {
            if (plate.Contact > t)
                break;

            // a run counts when it crosses the plate, not when the ball is hit
            var runs = 0;
            for (var k = 0; k <= 3; k++)
            {
                if (plate.Has(k) && k + Bases(plate, k) >= 4)
                {
                    var from = k == 0 ? plate.BatterFrom : plate.RunFrom;
                    if (plate.Real(from + (4 - k) * plate.PerBase) <= t)
                        runs++;
                }
            }

            runs = Math.Min(runs, plate.Runs);
            _line[plate.Side, plate.Half.Inning - 1] += runs;
            _runs[plate.Side] += runs;
            if (plate.Outcome is >= 1 and <= 4)
                _hits[plate.Side]++;
        }

        var over = _hasPlay && t >= CMUSports.Length(CMUSport.Baseball) - 0.05f;

        // the outfield board: a line score, away over home
        var x = BoardBox.X + 4;
        var y = BoardBox.Y + 1;
        for (var inning = 0; inning < CMUSports.Innings; inning++)
            Say(Columns[inning], x + 34 + inning * 11, y, Color.FromHex("#9AA4B0"));

        Say(_runsLabel, x + 104, y, Color.FromHex("#FFD24A"));
        Say(_hitsLabel, x + 116, y, Color.FromHex("#9AA4B0"));
        for (var row = 0; row < 2; row++)
        {
            var side = row == 0 ? 1 : 0;
            var rowY = y + 12 + row * 12;
            var batting = _half?.Side == side && !over;
            Say(_abbreviations[side], x, rowY, side == 0 ? _homeColour : _awayColour);
            for (var inning = 0; inning < CMUSports.Innings; inning++)
            {
                var played = _played[side, inning];
                var text = played ? Digits[Math.Clamp(_line[side, inning], 0, 99)] : over && side == 0 && inning == CMUSports.Innings - 1 ? "X" : string.Empty;
                if (text.Length > 0)
                    Say(text, x + 34 + inning * 11, rowY, batting && _half?.Inning == inning + 1 ? Color.Gold : Color.White);
            }

            Say(Digits[Math.Clamp(_runs[side], 0, 99)], x + 104, rowY, Color.FromHex("#FFD24A"));
            Say(Digits[Math.Clamp(_hits[side], 0, 99)], x + 116, rowY, Color.White);
        }

        // the score bug: inning, outs, who is on
        if (_half != null && !over)
        {
            Say(_bugs[_half.Side, _half.Inning - 1], BugBox.X + 6, BugBox.Y + 3, Color.White);
            Say(_outsLabel, BugBox.X + 6, BugBox.Y + 17, Color.FromHex("#9AA4B0"));
        }
        else if (over)
        {
            Say(_final, BugBox.X + 6, BugBox.Y + 10, Color.Gold);
        }
        else if (!_hasPlay || t < 3)
        {
            Say(_matchup, BugBox.X + 6, BugBox.Y + 10, Color.White);
        }

        if (_plate != null && t >= _plate.Release && t < _plate.Contact + 2.2f)
            _radar = _plate.Speed;

        if (_plate is { Outcome: 4 } homer && t >= homer.Real(homer.Landed) && t < homer.Real(homer.Landed) + 2.5f)
            _radar = homer.Distance;

        if (_radar.Length > 0)
            Say(_radar, RadarBox.X + RadarBox.Z - 4, RadarBox.Y + 2, Color.FromHex("#7CFF00"), right: true);
    }

    private void Calls(float t)
    {
        if (!_hasPlay)
            return;

        if (t >= 0 && t < 2.9f)
        {
            Say(_playBall, ArenaWidth / 2, 108, Color.Gold, true);
            return;
        }

        var half = _half;
        var plate = _plate;
        if (half != null && t < half.Start + 1.6f && (plate == null || t < plate.Contact))
        {
            var card = _cards[half.Side, half.Inning - 1];
            Say(card, ArenaWidth / 2, 104, Color.White, true);
            var line = _batting[half.Side, half.Inning - 1];
            Say(line, ArenaWidth / 2 - line.Length * 3.1f, 118, half.Side == 0 ? _homeColour : _awayColour);
            return;
        }

        if (plate == null || t < plate.Contact)
            return;

        if (t >= plate.Real(plate.CallAt) && t < plate.Real(plate.CallAt) + 1.5f)
            Say(plate.Call, ArenaWidth / 2, 104, plate.CallColour, true);

        if (plate.Line.Length > 0 && t >= plate.Real(plate.LineAt) && t < plate.Real(plate.LineAt) + 1.6f)
        {
            var colour = plate.WalkOff ? Color.Gold : Color.FromHex("#FFD24A");
            if (plate.WalkOff)
                Say(plate.Line, ArenaWidth / 2, 124, colour, true);
            else
                Say(plate.Line, ArenaWidth / 2 - plate.Line.Length * 3.1f, 118, colour);
        }
    }

    #endregion

    #region Drawing

    protected override void DrawField(DrawingHandleScreen handle)
    {
        // the stands, rows of seats stepping up
        Rect(handle, 0, Bar, ArenaWidth, ArenaHeight - Bar, Color.FromHex("#262A31"));
        for (var y = Bar + 6f; y < ArenaHeight; y += 7)
            Rect(handle, 0, y, ArenaWidth, 1, Color.FromHex("#30353D"));

        DrawCrowd(handle);

        Shape(handle, Surface, Grass);
        foreach (var stripe in Stripes)
            Shape(handle, stripe, Mown);

        Shape(handle, Track, Color.FromHex("#9C6C40"), DrawPrimitiveTopology.TriangleStrip);
        Shape(handle, Dirt, Clay);
        Shape(handle, Infield, Grass);
        Shape(handle, PlateCircle, Clay);
        Shape(handle, Mound, Color.FromHex("#C08A58"));
        Shape(handle, Rubber, Chalky);
        for (var i = 0; i + 1 < Chalk.Length; i += 2)
            Line(handle, Chalk[i], Chalk[i + 1], Chalky.WithAlpha(0.85f));

        foreach (var bag in Bags)
            Shape(handle, bag, Chalky);

        Shape(handle, PlateShape, Chalky);
        Shape(handle, Wall, Color.FromHex("#1D4A2C"), DrawPrimitiveTopology.TriangleStrip);
        Shape(handle, WallTop, Color.FromHex("#E8C547"), DrawPrimitiveTopology.LineStrip);
        foreach (var pole in Poles)
            Line(handle, pole, pole - new Vector2(0, 30 * Near(pole.Y)), Color.FromHex("#F2D22E"));

        for (var side = 0; side < 2; side++)
        {
            Shape(handle, Rails[side], Color.FromHex("#22324E"), DrawPrimitiveTopology.TriangleStrip);
            Shape(handle, Dens[side], Color.FromHex("#101216"));
        }

        // broadcast boxes over the stands
        Panel(handle, BoardBox, Color.FromHex("#0E1116"), Color.FromHex("#3A4250"));
        Panel(handle, BugBox, Color.FromHex("#0C0F14").WithAlpha(0.92f), Color.FromHex("#7CFF00"));
        if (_radar.Length > 0)
            Panel(handle, RadarBox, Color.FromHex("#0C0F14").WithAlpha(0.92f), Color.FromHex("#7CFF00"));

        DrawBug(handle);
    }

    private void Panel(DrawingHandleScreen handle, Vector4 box, Color fill, Color edge)
    {
        Rect(handle, box.X, box.Y, box.Z, box.W, fill);
        Rect(handle, box.X, box.Y + box.W - 1, box.Z, 1, edge);
    }

    /// <summary>Outs as lights and a little diamond with the occupied bags lit.</summary>
    private void DrawBug(DrawingHandleScreen handle)
    {
        if (_half == null || _hasPlay && _t >= CMUSports.Length(CMUSport.Baseball) - 0.05f)
            return;

        for (var i = 0; i < 3; i++)
        {
            var lit = i < _outs;
            Circle(handle, BugBox.X + 40 + i * 9, BugBox.Y + 23, 3, lit ? Color.FromHex("#FF5050") : Color.FromHex("#3A4250"));
        }

        var centre = new Vector2(BugBox.X + 92, BugBox.Y + 16);
        ReadOnlySpan<Vector2> bags = stackalloc[] { new Vector2(9, 0), new Vector2(0, -9), new Vector2(-9, 0) };
        for (var i = 0; i < 3; i++)
        {
            var lit = (_bases & 1 << i) != 0;
            Diamond(handle, centre + bags[i], 4.5f, lit ? Color.Gold : Color.FromHex("#3A4250"));
        }

        Diamond(handle, centre + new Vector2(0, 9), 3, Color.FromHex("#9AA4B0"));
    }

    private void Diamond(DrawingHandleScreen handle, Vector2 at, float size, Color colour)
    {
        var origin = Box(0, 0, 0, 0).TopLeft;
        var scale = UIScale;
        _buffer[0] = (at + new Vector2(size, 0)) * scale + origin;
        _buffer[1] = (at + new Vector2(0, -size)) * scale + origin;
        _buffer[2] = (at + new Vector2(-size, 0)) * scale + origin;
        _buffer[3] = (at + new Vector2(0, size)) * scale + origin;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _buffer.AsSpan(0, 4), colour);
    }

    private void DrawCrowd(DrawingHandleScreen handle)
    {
        var up = _excite > 0.55f ? 1.5f : 0;
        for (var i = 0; i < Seats.Length; i++)
        {
            var seat = Seats[i];
            var bob = _excite * 2.4f * MathF.Max(0, MathF.Sin(seat.Phase + _clock * 11));
            var at = seat.At - new Vector2(0, bob + up);
            var size = seat.Size;
            Rect(handle, at.X - size * 0.75f, at.Y, size * 1.5f, size * 1.2f, _shirts[seat.Shirt]);
            if (_hurtFans[i])
            {
                // knocked flat in his seat
                Rect(handle, at.X - size, at.Y + size * 0.2f, size * 1.1f, size * 0.8f, Skins[seat.Skin]);
                continue;
            }

            Rect(handle, at.X - size * 0.5f, at.Y - size, size, size, Skins[seat.Skin]);
            // arms up when the place goes wild
            if (_excite > 0.5f && seat.Shirt % 3 == 0)
                Rect(handle, at.X + size * 0.5f, at.Y - size * 2, 1, size * 1.6f, Skins[seat.Skin]);
        }
    }

    protected override void DrawOverDecals(DrawingHandleScreen handle)
    {
        // shadows under everyone, and under the ball
        for (var doll = 0; doll < _used.Length; doll++)
        {
            if (!_used[doll])
                continue;

            var near = Near(_feet[doll].Y);
            Ellipse(handle, _feet[doll], 7 * near, 2.2f * near, Color.Black.WithAlpha(0.3f * _alphas[doll]));
        }

        if (_ballOn)
        {
            var near = Near(_ballShadow.Y);
            var fade = 1 - Math.Min(1, _ballHeight / 140);
            Ellipse(handle, _ballShadow, 2.6f * near, 1.1f * near, Color.Black.WithAlpha(0.4f * fade));
        }
    }

    protected override void DrawOverlay(DrawingHandleScreen handle)
    {
        if (_batOn)
            DrawBat(handle, _batAt, _batAngle, _batSize, true);

        if (_shardOn)
            DrawBat(handle, _shardAt, _shardAngle, _shardSize, false);

        if (!_ballOn)
            return;

        for (var i = _trailCount - 1; i >= 0; i--)
            Ellipse(handle, _trail[i], _trailSize[i], _trailSize[i], Color.White.WithAlpha(0.32f * (1 - i / (float) _trail.Length)));

        // a dark rim keeps it readable against the crowd and the white shirts
        Ellipse(handle, _ballAt, _ballSize + 0.9f, _ballSize + 0.9f, Color.Black.WithAlpha(0.55f));
        Ellipse(handle, _ballAt, _ballSize, _ballSize, Color.FromHex("#F4F1E8"));
        if (_ballSize > 2.2f)
            Line(handle, _ballAt - new Vector2(_ballSize * 0.5f, 0), _ballAt + new Vector2(_ballSize * 0.5f, 0), Color.FromHex("#C03030"));
    }

    /// <summary>The bat sprite with its handle in the hands and the barrel pointing at <paramref name="degrees"/> on screen.</summary>
    private void DrawBat(DrawingHandleScreen handle, Vector2 hands, float degrees, float size, bool held)
    {
        var texture = Sprite(BatRsi, "icon");
        var radians = degrees * MathF.PI / 180;
        var direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        // the icon lies with its barrel up and to the right
        var centre = held ? hands + direction * size * 0.31f : hands;
        Texture(handle, texture, centre, new Vector2(size, size), radians + MathF.PI / 4);
    }

    /// <summary>Draws a fan of arena points with the shake, from a reused buffer.</summary>
    private void Shape(DrawingHandleScreen handle, Vector2[] points, Color colour, DrawPrimitiveTopology topology = DrawPrimitiveTopology.TriangleFan)
    {
        var origin = Box(0, 0, 0, 0).TopLeft;
        var scale = UIScale;
        var count = Math.Min(points.Length, _buffer.Length);
        for (var i = 0; i < count; i++)
            _buffer[i] = points[i] * scale + origin;

        handle.DrawPrimitives(topology, _buffer.AsSpan(0, count), colour);
    }

    private void Ellipse(DrawingHandleScreen handle, Vector2 at, float width, float height, Color colour)
    {
        const int segments = 14;
        var origin = Box(0, 0, 0, 0).TopLeft;
        var scale = UIScale;
        for (var i = 0; i < segments; i++)
        {
            var a = MathF.Tau * i / segments;
            _buffer[i] = (at + new Vector2(MathF.Cos(a) * width, MathF.Sin(a) * height)) * scale + origin;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _buffer.AsSpan(0, segments), colour);
    }

    #endregion
}
