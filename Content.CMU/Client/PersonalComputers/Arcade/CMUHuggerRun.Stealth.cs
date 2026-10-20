using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Stage 4: the runner sneaks through a desert colony's outskirts at night, seen from above like the VR missions of
/// an old stealth game and drawn with the game's own Kutjevo desert sprites. Four short rooms, each harder than the
/// last: marines walk their patrol routes with vision cones that cover blocks (chain-link fences do not), and a
/// sentry tower sweeps a searchlight. Stand in a cone and the marine's "!" fills; fill it and he shouts, radios it in
/// and opens fire while the others converge on where the runner was seen, search, then give up ("?") and go back to
/// their rounds. Action pounces a short dash: landed on a marine from behind or the side (or before he has noticed)
/// it kills him, leaving him in a pool of blood that stays; into his face he shoves the runner off and shoots.
/// Reaching the exit clears the room: points for the room, for not being spotted (GHOST), for silent takedowns and
/// for time. The last room hands on to the ravager. The JS port (hugger/stealth.js) has the same constants, maps,
/// sprites, sounds and strings.
/// </summary>
public sealed class CMUHuggerStealth : CMUHuggerStage
{
    #region Tuning

    private const float Tile = 16;

    /// <summary>Each room sits this far along world x from the last, so the stains of a cleared room stay out of sight.</summary>
    private const float RoomGap = 4096;

    private const float ViewHeight = CMUHuggerRun.ScreenHeight - CMUHuggerRun.HudHeight;
    private const int MaxW = 40;
    private const int MaxH = 24;
    private const int MaxMarines = 6;
    private const int MaxRoute = 6;

    // The runner
    private const float RunSpeed = 84;
    private const float RunAccel = 720;
    private const float RunnerRadius = 6;
    private const float RunnerScale = 0.6f;
    private const float PounceSpeed = 270;
    private const float PounceTime = 0.2f;
    private const float PounceCooldown = 0.45f;
    private const float PounceBuffer = 0.15f;
    private const float Contact = 13;
    private const float MaulTime = 0.5f;
    private const float StunTime = 0.6f;
    private const int Health = 3;
    private const float Grace = 0.8f;
    private const int TrailLength = 6;

    // Marines
    private const float MarineRadius = 5;
    private const float MarineScale = 0.75f;
    private const float PatrolSpeed = 30;
    private const float HuntSpeed = 62;
    private const float TurnRate = 3.5f;
    private const float ConeHalf = 0.62f;
    private const float ConeRange = 88;
    private const float HuntRange = 110;
    private const float TowerHalf = 0.5f;
    private const float TowerRange = 150;
    private const int Rays = 20;

    /// <summary>How fast the "!" fills, per second, for a runner in the middle of a cone; slower far off and at the edges.</summary>
    private const float NoticeRate = 2.6f;

    /// <summary>Marines already looking for the runner fill it this many times faster.</summary>
    private const float AwareFactor = 4;

    private const float Decay = 0.55f;
    private const float HearRange = 20;
    private const float HearRate = 1.1f;

    /// <summary>A pounce that comes in within this angle of where the marine looks is in his face.</summary>
    private const float FaceAngle = 0.95f;

    /// <summary>Below this much "!", a marine has not really noticed the runner yet, and a pounce still takes him.</summary>
    private const float Unaware = 0.3f;

    private const float LoseTime = 2.2f;
    private const float SearchTime = 5.5f;
    private const float AimTime = 0.45f;

    /// <summary>The first aim after the shout is slower: the moment to break and run.</summary>
    private const float AlertAim = 0.7f;

    private const int BurstSize = 3;
    private const float BurstGap = 0.1f;
    private const float FireCooldown = 0.85f;
    private const float FireRange = 170;
    private const float BulletSpeed = 380;
    private const float BulletLife = 0.7f;
    private const float Spread = 0.08f;
    private const float NoiseRange = 72;
    private const float LightRadius = 22;
    private const float LightSpeed = 55;
    private const float LightChase = 110;

    /// <summary>Bullets, muzzles and lasers are drawn this far above the ground they move on.</summary>
    private const float Chest = 9;

    /// <summary>The tower guard stands on the watchtower's platform, drawn this far above his post.</summary>
    private const float Lift = 4;

    private const float Tracer = 12;
    private const int MaxBullets = 48;
    private const int MaxPath = 128;

    // Scoring
    private const int ClearPoints = 500;
    private const int GhostPoints = 500;
    private const int SilentPoints = 250;
    private const int KillPoints = 100;
    private const float TimeRate = 10;
    private const int TimeCap = 400;

    // Flow
    private const float ExitWalk = 0.5f;
    private const float TallyStep = 0.4f;
    private const float TallyTime = 2.6f;
    private const float RoomFade = 0.35f;
    private const float BannerTime = 2.6f;

    // Tiles
    private const byte Sand = 0;
    private const byte Scrub = 1;
    private const byte Rock = 2;
    private const byte Crate = 3;
    private const byte Barrel = 4;
    private const byte Fence = 5;
    private const byte Wall = 6;
    private const byte TowerTile = 7;
    private const byte Exit = 8;
    private const byte Sandbag = 9;
    private const byte Wreck = 10;
    private const int MaxBoulders = 16;

    #endregion

    #region Sounds and colours

    private const string Pounce = "/Audio/_RMC14/Xeno/alien_pounce.ogg";
    private const string Gun = "/Audio/Weapons/Guns/Gunshots/m41.ogg";
    private const string Ricochet = "/Audio/_RMC14/Bullets/bullet_impact1.ogg";
    private const string Spotted = "/Audio/Effects/alert.ogg";
    private const string Radio = "/Audio/_RMC14/Effects/radiostatic.ogg";
    private const string BodyFall = "/Audio/CMU14/PersonalComputers/Games/fall.ogg";
    private const string Block = "/Audio/_RMC14/Xeno/alien_claw_block.ogg";
    private const string Death = "/Audio/_RMC14/Xeno/alien_death.ogg";
    private const string Cleared = "/Audio/Effects/double_beep.ogg";
    private const string Ghosted = "/Audio/_RMC14/Effects/tech_notification.ogg";
    private const string TallyBeep = "/Audio/Effects/beep1.ogg";
    private const string MaleSigh = "/Audio/Voice/Human/male_sigh.ogg";
    private const string FemaleSigh = "/Audio/Voice/Human/female_sigh.ogg";

    private static readonly string[] Claws =
    {
        "/Audio/_RMC14/Xeno/alien_claw_flesh1.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh2.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh3.ogg",
        "/Audio/_RMC14/Xeno/alien_claw_flesh4.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh5.ogg",
    };

    private static readonly string[] Bites = { "/Audio/_RMC14/Xeno/alien_bite1.ogg", "/Audio/_RMC14/Xeno/alien_bite2.ogg" };

    private static readonly string[] Hisses =
    {
        "/Audio/_RMC14/Xeno/alien_hiss1.ogg", "/Audio/_RMC14/Xeno/alien_hiss2.ogg", "/Audio/_RMC14/Xeno/alien_hiss3.ogg",
    };

    private static readonly string[] Pain =
    {
        "/Audio/_RMC14/Xeno/alien_help1.ogg", "/Audio/_RMC14/Xeno/alien_help2.ogg", "/Audio/_RMC14/Xeno/alien_help3.ogg",
    };

    private static readonly string[] Flesh =
    {
        "/Audio/_RMC14/Bullets/impact_flesh_1.ogg", "/Audio/_RMC14/Bullets/impact_flesh_2.ogg",
        "/Audio/_RMC14/Bullets/impact_flesh_3.ogg", "/Audio/_RMC14/Bullets/impact_flesh_4.ogg",
    };

    private static readonly string[] Punches = { "/Audio/CMU14/PersonalComputers/Games/punch_1.ogg", "/Audio/CMU14/PersonalComputers/Games/punch_2.ogg" };
    private static readonly string[] MaleGasps = { "/Audio/Effects/Gasp/gasp_male1.ogg", "/Audio/Effects/Gasp/gasp_male2.ogg" };

    private static readonly string[] FemaleGasps =
    {
        "/Audio/Effects/Gasp/gasp_female1.ogg", "/Audio/Effects/Gasp/gasp_female2.ogg", "/Audio/Effects/Gasp/gasp_female3.ogg",
    };

    private static readonly string[] Shouts =
    {
        "/Audio/Voice/Talk/speak_1_exclaim.ogg", "/Audio/Voice/Talk/speak_2_exclaim.ogg",
        "/Audio/Voice/Talk/speak_3_exclaim.ogg", "/Audio/Voice/Talk/speak_4_exclaim.ogg",
    };

    private static readonly string[] Asks =
    {
        "/Audio/Voice/Talk/speak_1_ask.ogg", "/Audio/Voice/Talk/speak_2_ask.ogg",
        "/Audio/Voice/Talk/speak_3_ask.ogg", "/Audio/Voice/Talk/speak_4_ask.ogg",
    };

    private static readonly string[] Steps =
    {
        "/Audio/Effects/Footsteps/asteroid1.ogg", "/Audio/Effects/Footsteps/asteroid2.ogg", "/Audio/Effects/Footsteps/asteroid3.ogg",
        "/Audio/Effects/Footsteps/asteroid4.ogg", "/Audio/Effects/Footsteps/asteroid5.ogg",
    };

    private static readonly string[] Rattles = { "/Audio/Effects/fence_rattle1.ogg", "/Audio/Effects/fence_rattle2.ogg", "/Audio/Effects/fence_rattle3.ogg" };

    private static readonly string[] Winds =
    {
        "/Audio/Effects/Weather/wind_2_1.ogg", "/Audio/Effects/Weather/wind_2_2.ogg", "/Audio/Effects/Weather/wind_3_1.ogg",
    };

    // The game's own desert art, drawn at half size: Kutjevo's rock and colony walls (corner smoothed like the game
    // does it), chain-link fences, cargo containers, crates, barrels, sandbags, a wreck, the watchtower, boulders,
    // cacti and stones on the sand.
    private const string RockRsi = "/Textures/_RMC14/Structures/Walls/kutjevo_rock.rsi";
    private const string WallRsi = "/Textures/_RMC14/Structures/Walls/kutjevo_wall.rsi";
    private const string FenceRsi = "/Textures/_RMC14/Structures/fences.rsi";
    private const string BarrelRsi = "/Textures/_RMC14/Structures/barrels.rsi";
    private const string SandbagRsi = "/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi";
    private const string TowerRsi = "/Textures/_RMC14/Structures/watchtower.rsi";
    private const string WreckRsi = "/Textures/_RMC14/Structures/vehicle_pileup.rsi";
    private const string CactusRsi = "/Textures/_RMC14/Structures/Flora/cactus.rsi";
    private const string StoneRsi = "/Textures/_RMC14/Structures/Flora/rocks.rsi";
    private const string BoulderRsi = "/Textures/_RMC14/Structures/Flora/large_boulder.rsi";
    private const string ContainerRsi = "/Textures/_RMC14/Structures/containers.rsi";
    private const float WreckWidth = 123;
    private const float WreckHeight = 82;

    private static readonly string[] Floors =
    {
        "/Textures/_RMC14/Tiles/planet/desert/desert_0.png", "/Textures/_RMC14/Tiles/planet/desert/desert_1.png",
        "/Textures/_RMC14/Tiles/planet/desert/desert_2.png", "/Textures/_RMC14/Tiles/planet/desert/desert_3.png",
        "/Textures/_RMC14/Tiles/planet/desert/desert_4.png", "/Textures/_RMC14/Tiles/planet/desert/desert_5.png",
    };

    private static readonly string[] RockStates = { "rock0", "rock1", "rock2", "rock3", "rock4", "rock5", "rock6", "rock7" };
    private static readonly string[] WallStates = { "colony0", "colony1", "colony2", "colony3", "colony4", "colony5", "colony6", "colony7" };

    private static readonly string[] FenceStates =
    {
        "fence0", "fence1", "fence2", "fence3", "fence4", "fence5", "fence6", "fence7",
        "fence8", "fence9", "fence10", "fence11", "fence12", "fence13", "fence14", "fence15",
    };

    private static readonly string[] Crates =
    {
        "/Textures/_RMC14/Structures/Storage/Crates/woodcrate.rsi", "/Textures/_RMC14/Structures/Storage/Crates/supply.rsi",
        "/Textures/_RMC14/Structures/Storage/Crates/ammo.rsi", "/Textures/_RMC14/Structures/Storage/Crates/weapons.rsi",
    };

    /// <summary>Cargo containers, two tiles tall: left, middle and right pieces in three colours.</summary>
    private static readonly string[][] Containers =
    {
        new[] { "redcontainerleft", "redcontainercenter", "redcontainerright" },
        new[] { "greencontainerleft", "greencontainermiddle", "greencontainerright" },
        new[] { "bluecontainerleft", "bluecontainermiddle", "bluecontainerright" },
    };

    private static readonly string[] Barrels = { "barrel_green", "barrel_red", "barrel_yellow", "barrel_blue" };
    private static readonly string[] Cacti = { "cactus_3", "cacti_1", "cacti_9", "cactus_7" };
    private static readonly string[] Stones = { "rock_1", "rock_5", "rock_8", "rock_2" };
    private static readonly string[] Boulders = { "boulder_large1", "boulder_large2" };

    /// <summary>The desert at night: the sand is darkened under everything else, so the cones and cover read over it.</summary>
    private static readonly Color FloorTint = Color.FromHex("#7C866A");

    private static readonly Color ExitColour = Color.FromHex("#12361A");

    #endregion

    #region Rooms

    /// <summary>A marine's post: a route of tile points walked in a loop (one point is a sentry who looks <see cref="Look"/>
    /// radians, sweeping <see cref="Sweep"/> either side), waiting <see cref="Wait"/> seconds at each. A tower guard
    /// stands on the tower, at the corner between the tiles of his point.</summary>
    private readonly record struct Post(int[] Route, float Wait = 1, float Look = 0, float Sweep = 0, bool Tower = false);

    /// <summary>
    /// A room: # rock, C crate (two rows of them are a row of cargo containers), B barrel, F chain-link fence (blocks
    /// the way, not the view), W colony wall, T sentry tower, Z sandbags, V a wreck's footprint of 4 x 2 tiles (the
    /// wreck is drawn from <see cref="WreckAt"/>, its top left tile), comma a cactus or stones, S start, E exit. The
    /// searchlight sweeps around <see cref="LightCentre"/> by <see cref="LightRadii"/>, in tiles.
    /// </summary>
    private sealed class Room
    {
        public readonly string[] Map;
        public readonly float Par;
        public readonly Post[] Posts;
        public readonly bool Light;
        public readonly Vector2 LightCentre;
        public readonly Vector2 LightRadii;
        public readonly bool HasWreck;
        public readonly Vector2 WreckAt;

        public Room(float par, string[] map, Post[] posts, bool light = false, Vector2 centre = default, Vector2 radii = default,
            bool wreck = false, Vector2 wreckAt = default)
        {
            Par = par;
            Map = map;
            Posts = posts;
            Light = light;
            LightCentre = centre;
            LightRadii = radii;
            HasWreck = wreck;
            WreckAt = wreckAt;
        }
    }

    private static readonly Room[] Rooms =
    {
        new(30, new[]
        {
            "######################",
            "##.....,......#....,##",
            "#.....##.........CC..#",
            "#..,..##.........CC..#",
            "#..........,.........#",
            "#...#................#",
            "#.S.#.......B....,...#",
            "#...#................#",
            "#..........,.........#",
            "#..CC.........B......E",
            "#..CC.......##.......E",
            "##.....,....##.......E",
            "######################",
        }, new Post[]
        {
            new(new[] { 9, 2, 9, 11 }, 1.1f),
            new(new[] { 17, 8 }, Look: -MathF.PI / 2, Sweep: 0.7f),
        }),
        new(45, new[]
        {
            "##############################",
            "#......,.......B.......,.....#",
            "#..CC....CCC.......CCC...CC..#",
            "#..CC....CCC...,...CCC...CC..#",
            "#............................#",
            "#.S....BB......VVVV......BB..E",
            "#......BB......VVVV......BB..E",
            "#............................E",
            "#..CC....CCC.......CCC...CC..#",
            "#..CC....CCC...,...CCC...CC..#",
            "#..B..,....B...B......B......#",
            "#.......,..........,.........#",
            "##############################",
        }, new Post[]
        {
            new(new[] { 27, 4, 3, 4 }, 1.2f),
            new(new[] { 27, 7, 3, 7 }, 1.2f),
            new(new[] { 14, 1, 14, 11 }, 1.4f),
            new(new[] { 28, 11 }, Look: -2.4f, Sweep: 0.5f),
        }, wreck: true, wreckAt: new Vector2(15, 5)),
        new(60, new[]
        {
            "#########################EEE##",
            "#.....,.........F.........,..#",
            "#..##...........F....CC......#",
            "#..##.....B.....F....CC..##..#",
            "#...............F........##..#",
            "#....,..........F............#",
            "#....CC.........F.....B......#",
            "#....CC......................#",
            "#...........,................#",
            "#...##..........F......CC....#",
            "#FFFFFF...FFFFFFF..FFFFFFF..F#",
            "#...............F............#",
            "#..CC......,....F....ZZ......#",
            "#..CC...........F....ZZ..B...#",
            "#...............F............#",
            "#.....##.....................#",
            "#.....##........F.....CC.....#",
            "#..S............F.....CC.....#",
            "#.........,.....F............#",
            "##############################",
        }, new Post[]
        {
            new(new[] { 2, 11, 13, 11, 13, 14, 2, 14 }, 0.8f),
            new(new[] { 19, 11, 19, 18 }, 1.2f),
            new(new[] { 17, 8 }, Look: MathF.PI, Sweep: 0.9f),
            new(new[] { 19, 1, 28, 1, 28, 8, 19, 8 }, 0.9f),
            new(new[] { 2, 5, 14, 5 }, 1.0f),
        }),
        new(70, new[]
        {
            "##################################",
            "#WWWWW.....,..........WWWWWW.....#",
            "#WWWWW..........B.....WWWWWW.....#",
            "#WWWWW....CC..........WWWWWW...B.#",
            "#.........CC.....................#",
            "#....,..........,.......CC.......#",
            "#..B....................CC.......#",
            "#.........##.....................#",
            "#.........##.....ZZ..............#",
            "#.S..............TT..........,...E",
            "#................TT..............E",
            "#.....CC.........................#",
            "#.....CC..............##.........#",
            "#.....................##....B....#",
            "#..,......B......................#",
            "#.............,..........VVVV....#",
            "#WWWWWW.......WWWWW......VVVV....#",
            "#WWWWWW.......WWWWW..............#",
            "#WWWWWW.......WWWWW......,.......#",
            "##################################",
        }, new Post[]
        {
            new(new[] { 18, 10 }, Look: MathF.PI, Sweep: 1.2f, Tower: true),
            new(new[] { 13, 6, 21, 6, 21, 13, 13, 13 }, 0.6f),
            new(new[] { 30, 2, 30, 17 }, 1.2f),
            new(new[] { 2, 13, 11, 13 }, 1.0f),
            new(new[] { 27, 10 }, Look: -MathF.PI / 2, Sweep: 1.0f),
        }, true, new Vector2(18, 10), new Vector2(8, 4), true, new Vector2(25, 15)),
    };

    #endregion

    private enum State : byte
    {
        Patrol,
        Suspect,
        Hunt,
        Combat,
        Search,
        Return,
        Dead,
    }

    private sealed class Marine
    {
        public readonly CMUHuggerPerson Person = new();
        public readonly float[] Rays = new float[CMUHuggerStealth.Rays + 1];
        public readonly int[] Path = new int[MaxPath];
        public readonly Vector2[] Route = new Vector2[MaxRoute];
        public int RouteCount;
        public float Wait;
        public float Look;
        public float Sweep;
        public float Phase;
        public bool Tower;

        public Vector2 At;
        public float Facing;
        public State State;
        public float Meter;
        public int Leg;
        public float Pause;
        public float LookAt;
        public bool Sees;
        public Vector2 Goal;
        public Vector2 Want;
        public Vector2 Hunt;
        public Vector2 Suspect;
        public bool Reachable;
        public int PathLength;
        public int PathAt;
        public float Repath;
        public float Lost;
        public float Search;
        public float SearchAngle;
        public float Aim;
        public int Burst;
        public float BurstTimer;
        public float Cooldown;
        public float Flash;
        public float Popped;
        public float Gait;
        public bool Moving;
        public float Die;
        public float Fall;
        public float FallDirection;
        public int Pool;
        public bool Found;
    }

    private struct Bullet
    {
        public Vector2 At;
        public Vector2 Velocity;
        public float Life;
        public float Lift;
    }

    private static readonly float[] PoolMarks = { 0.3f, 0.7f, 1.2f, 1.8f };

    private readonly byte[] _grid = new byte[MaxW * MaxH];
    private readonly byte[] _fence = new byte[MaxW * MaxH];
    private readonly ushort[] _corners = new ushort[MaxW * MaxH];
    private readonly int[] _boulders = new int[MaxBoulders * 2];
    private int _boulderCount;
    private Texture[]? _floors;
    private readonly int[] _prev = new int[MaxW * MaxH];
    private readonly int[] _queue = new int[MaxW * MaxH];
    private readonly Marine[] _marines = new Marine[MaxMarines];
    private readonly Bullet[] _bullets = new Bullet[MaxBullets];
    private readonly Vector2[] _trail = new Vector2[TrailLength];
    private readonly int[] _order = new int[MaxMarines + 1];

    private int _marineCount;
    private int _bulletCount;
    private int _trailCount;
    private Marine? _tower;

    private Room _spec = Rooms[0];
    private int _room;
    private int _w;
    private int _h;
    private float _ox;
    private Vector2 _exit;
    private Vector2 _exitDirection;
    private Vector2 _light;
    private bool _lightSees;
    private Vector2 _cam;

    private float _time;
    private int _health;
    private Vector2 _player;
    private Vector2 _velocity;
    private float _facing;
    private float _pounce;
    private float _pounceCool;
    private float _pounceWant;
    private Vector2 _pounceVelocity;
    private Vector2 _from;
    private float _maul;
    private float _stun;
    private float _grace;
    private bool _dead;
    private float _walk;
    private float _rattle;
    private float _step;
    private float _wind;
    private Vector2 _last;
    private float _roomTime;
    private bool _seen;
    private int _kills;
    private float _clearing = -1;
    private int _tallied;
    private bool _ghost;
    private int _timePoints;
    private float _fade;
    private bool _fadeOut;
    private float _banner;

    private readonly string _alertText = Loc.GetString("cmu-game-hugger-stealth-alert");
    private readonly string _cautionText = Loc.GetString("cmu-game-hugger-stealth-caution");
    private readonly string _exitText = Loc.GetString("cmu-game-hugger-stealth-exit");
    private readonly string _clearTitle = Loc.GetString("cmu-game-hugger-stealth-clear");
    private readonly string _spottedText = Loc.GetString("cmu-game-hugger-stealth-spotted");
    private readonly string _silentText = Loc.GetString("cmu-game-hugger-stealth-silent");
    private readonly string _killText = Loc.GetString("cmu-game-hugger-stealth-kill");
    private readonly string _deadText = Loc.GetString("cmu-game-hugger-stealth-dead");
    private string _roomText = string.Empty;
    private string _bannerText = string.Empty;
    private string _clearText = string.Empty;
    private string _ghostText = string.Empty;
    private string _timeText = string.Empty;
    private string _killsText = string.Empty;

    public CMUHuggerStealth()
    {
        for (var i = 0; i < MaxMarines; i++)
        {
            _marines[i] = new Marine();
        }
    }

    public override string Title => Loc.GetString("cmu-game-hugger-stealth-title");
    public override string Hint => Loc.GetString("cmu-game-hugger-stealth-hint");

    #region Lifecycle

    public override void Enter()
    {
        _time = 0;
        _health = Health;
        LoadRoom(0);
    }

    private void LoadRoom(int index)
    {
        var spec = Rooms[index];
        _room = index;
        _spec = spec;
        _w = spec.Map[0].Length;
        _h = spec.Map.Length;
        _ox = index * RoomGap;
        Array.Fill(_grid, Rock);
        Array.Clear(_fence);
        var exits = 0;
        var exitSum = Vector2.Zero;
        for (var y = 0; y < _h; y++)
        {
            for (var x = 0; x < _w; x++)
            {
                var ch = spec.Map[y][x];
                _grid[y * MaxW + x] = KindOf(ch);
                if (ch == 'S')
                {
                    _player = new Vector2(_ox + (x + 0.5f) * Tile, (y + 0.5f) * Tile);
                }
                else if (ch == 'E')
                {
                    exits++;
                    exitSum += new Vector2(x, y);
                    _exitDirection = new Vector2(x == _w - 1 ? 1 : x == 0 ? -1 : 0, y == 0 ? -1 : y == _h - 1 ? 1 : 0);
                }
            }
        }

        _exit = new Vector2(_ox + (exitSum.X / exits + 0.5f) * Tile, (exitSum.Y / exits + 0.5f) * Tile);

        // which way each fence runs: bits for a fence (or wall) to the left, right, up and down
        for (var y = 0; y < _h; y++)
        {
            for (var x = 0; x < _w; x++)
            {
                if (TileKind(x, y) != Fence)
                    continue;

                var bits = 0;
                if (Joins(x - 1, y))
                    bits |= 1;
                if (Joins(x + 1, y))
                    bits |= 2;
                if (Joins(x, y - 1))
                    bits |= 4;
                if (Joins(x, y + 1))
                    bits |= 8;
                _fence[y * MaxW + x] = (byte) bits;
            }
        }

        // the corners of each rock and colony wall tile, smoothed into their neighbours the way the game does
        _boulderCount = 0;
        for (var y = 0; y < _h; y++)
        {
            for (var x = 0; x < _w; x++)
            {
                var t = TileKind(x, y);
                if (t is Rock or Wall)
                    _corners[y * MaxW + x] = CornerFill(x, y, t);

                if (_boulderCount < MaxBoulders && IsBoulder(x, y))
                {
                    _boulders[_boulderCount * 2] = x;
                    _boulders[_boulderCount * 2 + 1] = y;
                    _boulderCount++;
                }
            }
        }

        _marineCount = 0;
        _tower = null;
        foreach (var post in spec.Posts)
        {
            if (_marineCount >= MaxMarines)
                break;

            var m = _marines[_marineCount++];
            m.RouteCount = Math.Min(MaxRoute, post.Route.Length / 2);
            for (var i = 0; i < m.RouteCount; i++)
            {
                var offset = post.Tower ? 0 : 0.5f;
                m.Route[i] = new Vector2(_ox + (post.Route[i * 2] + offset) * Tile, (post.Route[i * 2 + 1] + offset) * Tile);
            }

            m.Wait = post.Wait;
            m.Look = post.Look;
            m.Sweep = post.Sweep;
            m.Tower = post.Tower;
            m.Phase = Run.Fx() * MathF.Tau;
            Run.Dress(m.Person, CMUHuggerOutfit.Rifleman);
            ResetMarine(m);
            if (m.Tower)
                _tower = m;
        }

        if (spec.Light)
            _light = new Vector2(_ox + spec.LightCentre.X * Tile, spec.LightCentre.Y * Tile);

        _lightSees = false;
        _bulletCount = 0;
        _trailCount = 0;
        _velocity = Vector2.Zero;
        _facing = 0;
        _pounce = 0;
        _pounceCool = 0;
        _pounceWant = 0;
        _pounceVelocity = Vector2.Zero;
        _from = Vector2.Zero;
        _maul = 0;
        _stun = 0;
        _grace = 0;
        _dead = false;
        _walk = 0;
        _rattle = 0;
        _step = 0;
        _wind = 0.5f;
        _last = _player;
        _roomTime = 0;
        _seen = false;
        _kills = 0;
        _clearing = -1;
        _tallied = 0;
        _fade = index == 0 ? 0 : RoomFade;
        _fadeOut = false;
        _banner = BannerTime;
        _roomText = Loc.GetString("cmu-game-hugger-stealth-room", ("room", index + 1), ("rooms", Rooms.Length));
        _bannerText = Loc.GetString("cmu-game-hugger-stealth-banner", ("room", index + 1),
            ("name", Loc.GetString(index switch
            {
                0 => "cmu-game-hugger-stealth-name-0",
                1 => "cmu-game-hugger-stealth-name-1",
                2 => "cmu-game-hugger-stealth-name-2",
                _ => "cmu-game-hugger-stealth-name-3",
            })));
        _health = Math.Min(Health, _health + (index == 0 ? 0 : 1));
        UpdateCamera(1);
        CastCones();
        if (index > 0)
            Run.PlayAny(Hisses, -6);
    }

    private static void ResetMarine(Marine m)
    {
        m.At = m.Route[0];
        m.Facing = m.RouteCount > 1 ? MathF.Atan2(m.Route[1].Y - m.At.Y, m.Route[1].X - m.At.X) : m.Look;
        m.State = State.Patrol;
        m.Meter = 0;
        m.Leg = m.RouteCount > 1 ? 1 : 0;
        m.Pause = 0;
        m.LookAt = m.Facing;
        m.Sees = false;
        m.Goal = m.At;
        m.Want = m.At;
        m.Hunt = m.At;
        m.Suspect = m.At;
        m.Reachable = true;
        m.PathLength = 0;
        m.PathAt = 0;
        m.Repath = 0;
        m.Lost = 0;
        m.Search = 0;
        m.SearchAngle = 0;
        m.Aim = AimTime;
        m.Burst = 0;
        m.BurstTimer = 0;
        m.Cooldown = 0;
        m.Flash = 0;
        m.Popped = 0;
        m.Gait = 0;
        m.Moving = false;
        m.Die = 0;
        m.Fall = 0;
        m.FallDirection = 1;
        m.Pool = 0;
        m.Found = false;
    }

    public override void OnKey(CMUArcadeInput input)
    {
        if (input.Down && !input.Repeat && input.Key == CMUArcadeKey.Action)
            _pounceWant = PounceBuffer;
    }

    public override void OnRelease()
    {
        _pounceWant = 0;
    }

    #endregion

    #region Tiles

    private static byte KindOf(char ch)
    {
        return ch switch
        {
            ',' => Scrub,
            '#' => Rock,
            'C' => Crate,
            'B' => Barrel,
            'F' => Fence,
            'W' => Wall,
            'T' => TowerTile,
            'E' => Exit,
            'Z' => Sandbag,
            'V' => Wreck,
            _ => Sand,
        };
    }

    private byte TileKind(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _w || y >= _h)
            return Rock;

        return _grid[y * MaxW + x];
    }

    private bool Same(int x, int y, byte kind)
    {
        if (x < 0 || y < 0 || x >= _w || y >= _h)
            return kind == Rock;

        return _grid[y * MaxW + x] == kind;
    }

    /// <summary>
    /// The four corner states of a smoothed wall tile, packed three bits each (SE, NE, NW, SW): 1 the side
    /// counter-clockwise of the corner joins, 2 the diagonal, 4 the side clockwise (the game's CornerFill). Rock runs
    /// on past the edge of the map.
    /// </summary>
    private ushort CornerFill(int x, int y, byte kind)
    {
        int ne = 0, nw = 0, sw = 0, se = 0;
        if (Same(x, y - 1, kind))
        {
            ne |= 1;
            nw |= 4;
        }

        if (Same(x + 1, y - 1, kind))
            ne |= 2;

        if (Same(x + 1, y, kind))
        {
            ne |= 4;
            se |= 1;
        }

        if (Same(x + 1, y + 1, kind))
            se |= 2;

        if (Same(x, y + 1, kind))
        {
            se |= 4;
            sw |= 1;
        }

        if (Same(x - 1, y + 1, kind))
            sw |= 2;

        if (Same(x - 1, y, kind))
        {
            sw |= 4;
            nw |= 1;
        }

        if (Same(x - 1, y - 1, kind))
            nw |= 2;

        return (ushort) (se | ne << 3 | nw << 6 | sw << 9);
    }

    /// <summary>A lone 2 x 2 outcrop of rock, away from the walls of the room, gets a boulder on it; this is its top left tile.</summary>
    private bool IsBoulder(int tx, int ty)
    {
        for (var y = ty - 1; y <= ty + 2; y++)
        {
            for (var x = tx - 1; x <= tx + 2; x++)
            {
                var inside = x >= tx && x <= tx + 1 && y >= ty && y <= ty + 1;
                if (x <= 0 || y <= 0 || x >= _w - 1 || y >= _h - 1)
                    return false;

                if (_grid[y * MaxW + x] == Rock != inside)
                    return false;
            }
        }

        return true;
    }

    private bool Joins(int x, int y)
    {
        var t = TileKind(x, y);
        return t == Fence || t == Wall || t == Rock && x >= 0 && y >= 0 && x < _w && y < _h;
    }

    /// <summary>Whether a tile stops a walker; marines also keep off the exit.</summary>
    private static bool Solid(byte t, bool marine)
    {
        return t is Rock or Crate or Barrel or Fence or Wall or TowerTile or Sandbag or Wreck || marine && t == Exit;
    }

    /// <summary>Whether a tile blocks the view; from up the tower only rock and walls do.</summary>
    private static bool Opaque(byte t, bool elevated)
    {
        return t is Rock or Wall or Wreck || !elevated && t is Crate or Barrel or Sandbag;
    }

    private static bool StopsBullets(byte t)
    {
        return t is Rock or Wall or Crate or Barrel or Sandbag or Wreck;
    }

    private byte TileAt(Vector2 at)
    {
        return TileKind((int) MathF.Floor((at.X - _ox) / Tile), (int) MathF.Floor(at.Y / Tile));
    }

    /// <summary>
    /// Pushes a circle out of the solid tiles around it. Fences are thin rails and barrels round-ish, so the walker
    /// goes right up to them. Returns the kind of the last tile it touched, or -1.
    /// </summary>
    private int PushOut(ref Vector2 at, float r, bool marine)
    {
        var touched = -1;
        var x0 = (int) MathF.Floor((at.X - _ox - r) / Tile);
        var x1 = (int) MathF.Floor((at.X - _ox + r) / Tile);
        var y0 = (int) MathF.Floor((at.Y - r) / Tile);
        var y1 = (int) MathF.Floor((at.Y + r) / Tile);
        for (var ty = y0; ty <= y1; ty++)
        {
            for (var tx = x0; tx <= x1; tx++)
            {
                var t = TileKind(tx, ty);
                if (!Solid(t, marine))
                    continue;

                var bx = _ox + tx * Tile;
                var by = ty * Tile;
                if (t == Fence)
                {
                    var bits = _fence[ty * MaxW + tx];
                    var any = false;
                    if ((bits & 3) != 0)
                        any = PushRect(ref at, r, bx, by + 6, Tile, 4) || any;
                    if ((bits & 12) != 0)
                        any = PushRect(ref at, r, bx + 6, by, 4, Tile) || any;
                    if (bits == 0)
                        any = PushRect(ref at, r, bx + 5, by + 5, 6, 6) || any;
                    if (any)
                        touched = t;
                }
                else if (t == Barrel)
                {
                    if (PushRect(ref at, r, bx + 2, by + 2, 12, 12))
                        touched = t;
                }
                else if (PushRect(ref at, r, bx, by, Tile, Tile))
                {
                    touched = t;
                }
            }
        }

        return touched;
    }

    private static bool PushRect(ref Vector2 at, float r, float x, float y, float w, float h)
    {
        var cx = Math.Clamp(at.X, x, x + w);
        var cy = Math.Clamp(at.Y, y, y + h);
        var dx = at.X - cx;
        var dy = at.Y - cy;
        var d2 = dx * dx + dy * dy;
        if (d2 >= r * r)
            return false;

        if (d2 > 1e-6f)
        {
            var d = MathF.Sqrt(d2);
            at.X += dx / d * (r - d);
            at.Y += dy / d * (r - d);
            return true;
        }

        // the centre is inside: out the nearest side
        var left = at.X - x;
        var right = x + w - at.X;
        var up = at.Y - y;
        var down = y + h - at.Y;
        var least = MathF.Min(MathF.Min(left, right), MathF.Min(up, down));
        if (least == left)
            at.X = x - r;
        else if (least == right)
            at.X = x + w + r;
        else if (least == up)
            at.Y = y - r;
        else
            at.Y = y + h + r;
        return true;
    }

    /// <summary>Whether a circle at a point would overlap a solid tile.</summary>
    private bool Blocked(Vector2 at, float r, bool marine)
    {
        return PushOut(ref at, r, marine) >= 0;
    }

    /// <summary>Moves a walker, an axis at a time so it slides along walls. Returns the tile it bumped, or -1.</summary>
    private int Move(ref Vector2 at, Vector2 delta, float r, bool marine)
    {
        at.X += delta.X;
        var a = PushOut(ref at, r, marine);
        at.Y += delta.Y;
        var b = PushOut(ref at, r, marine);
        return b >= 0 ? b : a;
    }

    /// <summary>How far a sight line from a world point at an angle runs before something opaque stops it, up to <paramref name="max"/>.</summary>
    private float Cast(Vector2 origin, float angle, float max, bool elevated)
    {
        var dx = MathF.Cos(angle);
        var dy = MathF.Sin(angle);
        var px = (origin.X - _ox) / Tile;
        var py = origin.Y / Tile;
        var mx = (int) MathF.Floor(px);
        var my = (int) MathF.Floor(py);
        var deltaX = dx == 0 ? 1e30f : MathF.Abs(1 / dx);
        var deltaY = dy == 0 ? 1e30f : MathF.Abs(1 / dy);
        var stepX = dx < 0 ? -1 : 1;
        var stepY = dy < 0 ? -1 : 1;
        var sideX = dx < 0 ? (px - mx) * deltaX : (mx + 1 - px) * deltaX;
        var sideY = dy < 0 ? (py - my) * deltaY : (my + 1 - py) * deltaY;
        var limit = max / Tile;
        for (var k = 0; k < 64; k++)
        {
            float dist;
            if (sideX < sideY)
            {
                dist = sideX;
                sideX += deltaX;
                mx += stepX;
            }
            else
            {
                dist = sideY;
                sideY += deltaY;
                my += stepY;
            }

            if (dist >= limit)
                return max;

            if (Opaque(TileKind(mx, my), elevated))
                return dist * Tile;
        }

        return max;
    }

    #endregion

    #region Paths

    private Vector2 Centre(int tile)
    {
        return new Vector2(_ox + (tile % MaxW + 0.5f) * Tile, (tile / MaxW + 0.5f) * Tile);
    }

    /// <summary>Finds a way over the tiles from a marine to a world point (breadth first, eight ways) and stores it on him.</summary>
    private void Plan(Marine m, Vector2 goal)
    {
        m.Want = goal;
        m.Goal = goal;
        m.PathLength = 0;
        m.PathAt = 0;
        m.Repath = 1;
        m.Reachable = true;
        var sx = (int) MathF.Floor((m.At.X - _ox) / Tile);
        var sy = (int) MathF.Floor(m.At.Y / Tile);
        var ex = (int) MathF.Floor((goal.X - _ox) / Tile);
        var ey = (int) MathF.Floor(goal.Y / Tile);
        if (ex < 0 || ey < 0 || ex >= _w || ey >= _h || sx < 0 || sy < 0 || sx >= _w || sy >= _h)
            return;

        var start = sy * MaxW + sx;
        var end = ey * MaxW + ex;
        if (start == end)
            return;

        Array.Fill(_prev, -1);
        _prev[start] = start;
        var head = 0;
        var tail = 0;
        _queue[tail++] = start;
        var found = false;
        while (head < tail)
        {
            var at = _queue[head++];
            if (at == end)
            {
                found = true;
                break;
            }

            var x = at % MaxW;
            var y = at / MaxW;
            for (var k = 0; k < 8; k++)
            {
                var nx = x + (k is 0 or 4 or 5 ? 1 : k is 1 or 6 or 7 ? -1 : 0);
                var ny = y + (k is 2 or 4 or 6 ? 1 : k is 3 or 5 or 7 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= _w || ny >= _h)
                    continue;

                var next = ny * MaxW + nx;
                if (_prev[next] >= 0 || Solid(_grid[next], true) && next != end)
                    continue;

                // no cutting corners round a solid tile
                if (nx != x && ny != y && (Solid(TileKind(nx, y), true) || Solid(TileKind(x, ny), true)))
                    continue;

                _prev[next] = at;
                _queue[tail++] = next;
            }
        }

        if (!found)
        {
            // out of reach (over a fence): stay put and look from here
            m.Reachable = false;
            m.Goal = m.At;
            return;
        }

        var count = 0;
        for (var at = end; at != start && count < MaxPath; at = _prev[at])
        {
            count++;
        }

        if (count >= MaxPath)
            return;

        var step = end;
        for (var i = count - 1; i >= 0; i--)
        {
            m.Path[i] = step;
            step = _prev[step];
        }

        m.PathLength = count;
    }

    /// <summary>Whether a marine can walk straight from where he is to a point.</summary>
    private bool ClearWalk(Marine m, Vector2 to)
    {
        var delta = to - m.At;
        var steps = (int) MathF.Ceiling(delta.Length() / 5);
        for (var i = 1; i <= steps; i++)
        {
            if (Blocked(m.At + delta * ((float) i / steps), MarineRadius + 1, true))
                return false;
        }

        return true;
    }

    /// <summary>Walks a marine along his path towards his goal. Returns true once he is there.</summary>
    private bool Follow(Marine m, float speed, float dt)
    {
        // look ahead for corners he can cut
        for (var k = 0; k < 3 && m.PathAt < m.PathLength; k++)
        {
            var next = m.PathAt + 1;
            if (!ClearWalk(m, next < m.PathLength ? Centre(m.Path[next]) : m.Goal))
                break;

            m.PathAt++;
        }

        var target = m.PathAt < m.PathLength ? Centre(m.Path[m.PathAt]) : m.Goal;
        var delta = target - m.At;
        var d = delta.Length();
        if (d < 1.5f)
        {
            if (m.PathAt >= m.PathLength)
                return true;

            m.PathAt++;
            return false;
        }

        var length = MathF.Min(d, speed * dt);
        Move(ref m.At, delta / d * length, MarineRadius, true);
        m.Moving = true;
        Turn(m, MathF.Atan2(delta.Y, delta.X), TurnRate * dt);
        return false;
    }

    /// <summary>Walks towards a world point, planning a path when the goal moves to another tile or now and then.</summary>
    private bool GoTo(Marine m, Vector2 to, float speed, float dt)
    {
        if (m.Tower)
            return true;

        m.Repath -= dt;
        if (m.Repath <= 0 || MathF.Floor((to.X - _ox) / Tile) != MathF.Floor((m.Want.X - _ox) / Tile)
            || MathF.Floor(to.Y / Tile) != MathF.Floor(m.Want.Y / Tile))
        {
            Plan(m, to);
        }
        else if (m.Reachable)
        {
            m.Goal = to;
        }

        return Follow(m, speed, dt);
    }

    private static void Turn(Marine m, float target, float step)
    {
        var diff = Wrap(target - m.Facing);
        m.Facing = Wrap(m.Facing + Math.Clamp(diff, -step, step));
    }

    private static float Wrap(float angle)
    {
        return ((angle + MathF.PI) % MathF.Tau + MathF.Tau) % MathF.Tau - MathF.PI;
    }

    #endregion

    #region Update

    public override void Update(float dt)
    {
        _time += dt;
        if (_banner > 0)
            _banner -= dt;
        if (_fade > 0 && !_fadeOut)
            _fade = MathF.Max(0, _fade - dt);

        if (_clearing >= 0)
        {
            UpdateClear(dt);
            UpdateCamera(dt);
            return;
        }

        _roomTime += dt;
        if (!Run.Over)
            UpdatePlayer(dt);

        for (var i = 0; i < _marineCount; i++)
        {
            UpdateMarine(_marines[i], dt);
        }

        Separate();
        UpdateLight(dt);
        UpdateBullets(dt);
        UpdateAmbience(dt);
        UpdateCamera(dt);
        CastCones();
    }

    /// <summary>Traces each marine's view out to the cover in its way, for drawing the cones.</summary>
    private void CastCones()
    {
        for (var i = 0; i < _marineCount; i++)
        {
            var m = _marines[i];
            if (m.State == State.Dead)
                continue;

            var half = m.Tower ? TowerHalf : ConeHalf;
            var range = RangeOf(m);
            for (var r = 0; r <= Rays; r++)
            {
                m.Rays[r] = Cast(m.At, m.Facing - half + 2 * half * r / Rays, range, m.Tower);
            }
        }
    }

    private static float RangeOf(Marine m)
    {
        if (m.Tower)
            return TowerRange;

        return m.State is State.Hunt or State.Search or State.Combat ? HuntRange : ConeRange;
    }

    private void UpdateCamera(float dt)
    {
        var tx = Math.Clamp(_player.X - _ox - CMUHuggerRun.ScreenWidth / 2, 0, MathF.Max(0, _w * Tile - CMUHuggerRun.ScreenWidth));
        var ty = Math.Clamp(_player.Y - ViewHeight / 2, 0, MathF.Max(0, _h * Tile - ViewHeight));
        if (dt >= 1)
        {
            _cam = new Vector2(tx, ty);
        }
        else
        {
            var k = MathF.Min(1, dt * 6);
            _cam += (new Vector2(tx, ty) - _cam) * k;
        }

        Run.Camera = new Vector2(_ox + MathF.Round(_cam.X), MathF.Round(_cam.Y) - CMUHuggerRun.HudHeight);
    }

    private void UpdatePlayer(float dt)
    {
        if (_dead)
            return;

        if (_grace > 0)
            _grace -= dt;
        if (_pounceCool > 0)
            _pounceCool -= dt;
        if (_pounceWant > 0)
            _pounceWant -= dt;

        var input = new Vector2((Run.Held(CMUArcadeKey.Right) ? 1 : 0) - (Run.Held(CMUArcadeKey.Left) ? 1 : 0),
            (Run.Held(CMUArcadeKey.Down) ? 1 : 0) - (Run.Held(CMUArcadeKey.Up) ? 1 : 0));
        if (input.X != 0 && input.Y != 0)
            input *= MathF.Sqrt(0.5f);

        if (_maul > 0)
        {
            _maul -= dt;
            _velocity = Vector2.Zero;
            return;
        }

        if (_stun > 0)
        {
            _stun -= dt;
            _velocity *= MathF.Max(0, 1 - 6 * dt);
            Move(ref _player, _velocity * dt, RunnerRadius, false);
            return;
        }

        if (_pounce > 0)
        {
            _pounce -= dt;
            var bump = Move(ref _player, _pounceVelocity * dt, RunnerRadius, false);
            if (_trailCount < TrailLength)
            {
                _trail[_trailCount++] = _player;
            }
            else
            {
                Array.Copy(_trail, 1, _trail, 0, TrailLength - 1);
                _trail[TrailLength - 1] = _player;
            }

            for (var i = 0; i < _marineCount; i++)
            {
                var m = _marines[i];
                if (m.State == State.Dead || m.Tower)
                    continue;

                if (Vector2.Distance(m.At, _player) < Contact)
                {
                    Land(m);
                    return;
                }
            }

            if (bump >= 0)
            {
                _pounce = 0;
                Run.Emit(_player + _pounceVelocity * 0.03f, 6, CMUHuggerRun.Dust.WithAlpha(0.5f), 40, 0.6f, 3,
                    MathF.Atan2(-_pounceVelocity.Y, -_pounceVelocity.X), 1.6f, kind: CMUHuggerRun.Smoke);
                if (bump == Fence)
                    RattleFence();
                Run.Shake(1.5f, 0.15f);
            }

            if (_pounce <= 0)
            {
                _pounceCool = PounceCooldown;
                _velocity = _pounceVelocity * 0.3f;
                Run.Emit(_player + new Vector2(0, 2), 5, CMUHuggerRun.Dust.WithAlpha(0.45f), 30, 0.6f, 3, -MathF.PI / 2, MathF.Tau,
                    kind: CMUHuggerRun.Smoke);
            }

            return;
        }

        if (input != Vector2.Zero)
            _facing = MathF.Atan2(input.Y, input.X);

        if (_pounceWant > 0 && _pounceCool <= 0)
        {
            _pounceWant = 0;
            _pounce = PounceTime;
            _pounceVelocity = new Vector2(MathF.Cos(_facing), MathF.Sin(_facing)) * PounceSpeed;
            _from = _player;
            _trailCount = 0;
            Run.Play(Pounce, -3);
            Run.Emit(_player + new Vector2(0, 2), 6, CMUHuggerRun.Dust.WithAlpha(0.5f), 40, 0.6f, 3, _facing + MathF.PI, 1.4f,
                kind: CMUHuggerRun.Smoke);
            return;
        }

        var step = RunAccel * dt;
        _velocity.X = MoveTowards(_velocity.X, input.X * RunSpeed, step);
        _velocity.Y = MoveTowards(_velocity.Y, input.Y * RunSpeed, step);
        var bumped = Move(ref _player, _velocity * dt, RunnerRadius, false);
        var speed = _velocity.Length();
        _walk += speed * dt * 0.22f;
        if (_rattle > 0)
            _rattle -= dt;
        if (bumped == Fence && speed > 30)
            RattleFence();

        // marines are in the way too
        for (var i = 0; i < _marineCount; i++)
        {
            var m = _marines[i];
            if (m.State == State.Dead || m.Tower)
                continue;

            var away = _player - m.At;
            var d = away.Length();
            const float least = RunnerRadius + MarineRadius;
            if (d < least && d > 0.01f)
                _player = m.At + away / d * least;
        }

        if (TileAt(_player) == Exit)
            StartClear();
    }

    private static float MoveTowards(float value, float target, float step)
    {
        return value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);
    }

    private void RattleFence()
    {
        if (_rattle > 0)
            return;

        _rattle = 0.9f;
        Run.PlayAny(Rattles, -6);
        Noise(_player, NoiseRange);
    }

    /// <summary>Marines near a sound turn and come to look.</summary>
    private void Noise(Vector2 at, float range)
    {
        for (var i = 0; i < _marineCount; i++)
        {
            var m = _marines[i];
            if (m.State is not (State.Patrol or State.Return or State.Suspect))
                continue;

            if (Vector2.Distance(m.At, at) > range)
                continue;

            m.State = State.Suspect;
            m.Meter = MathF.Max(m.Meter, 0.5f);
            m.Suspect = at;
        }
    }

    /// <summary>The pounce reaches a marine: from behind or the side, or before he has noticed, he dies; else it is botched.</summary>
    private void Land(Marine m)
    {
        var approach = MathF.Atan2(_from.Y - m.At.Y, _from.X - m.At.X);
        var off = MathF.Abs(Wrap(approach - m.Facing));
        var unaware = m.State is State.Patrol or State.Return or State.Suspect && m.Meter < Unaware;
        _pounce = 0;
        if (off > FaceAngle || unaware)
            Takedown(m);
        else
            Botch(m);
    }

    private void Takedown(Marine m)
    {
        var silent = m.State is not (State.Combat or State.Hunt or State.Search);
        var angle = MathF.Atan2(_pounceVelocity.Y, _pounceVelocity.X);
        m.State = State.Dead;
        m.Die = 0;
        m.FallDirection = _pounceVelocity.X >= 0 ? 1 : -1;
        m.Pool = 0;
        m.Meter = 0;
        _maul = MaulTime;
        _player = m.At - new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7;
        PushOut(ref _player, RunnerRadius, false);
        _velocity = Vector2.Zero;
        _kills++;

        var at = m.At - new Vector2(0, 8);
        Run.Emit(at, 40, CMUHuggerRun.Blood, 170, 0.7f, 2.2f, angle, 2.4f, stains: true, drag: 5, flat: 1);
        Run.Emit(at, 16, CMUHuggerRun.DarkBlood, 110, 0.6f, 3, angle, 3, stains: true, drag: 5, flat: 1);
        Run.Emit(at, 10, CMUHuggerRun.Blood.WithAlpha(0.9f), 220, 0.25f, 1, angle, 1.2f, kind: CMUHuggerRun.Spark);
        Run.Splat(m.At, 26, CMUHuggerRun.Blood.WithAlpha(0.95f), 1);
        Run.PlayAny(Claws);
        Run.Pend(0.14f, Bites[Run.Rng.Next(Bites.Length)], -2);
        Run.PendScream(0.05f, m.Person.Female, -2);
        Run.Pend(0.42f, BodyFall, -2);
        Run.Shake(3, 0.3f);
        Run.SlowMotion(0.18f);
        Run.Flash(CMUHuggerRun.Blood.WithAlpha(0.22f), 0.2f);
        Run.Bonus(silent ? SilentPoints : KillPoints, m.At - new Vector2(0, 30), CMUHuggerRun.Acid);
        Run.Pop(silent ? _silentText : _killText, m.At - new Vector2(0, 42), CMUHuggerRun.Bright, 1.2f);
        Noise(m.At, NoiseRange);
    }

    private void Botch(Marine m)
    {
        var angle = MathF.Atan2(_player.Y - m.At.Y, _player.X - m.At.X);
        _stun = StunTime;
        _velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150;
        Run.PlayAny(Punches);
        Run.Play(Block, -4);
        Run.Shake(2.5f, 0.25f);
        Run.Emit(_player - new Vector2(0, 6), 8, CMUHuggerRun.Dust.WithAlpha(0.5f), 40, 0.5f, 3, angle, 2, kind: CMUHuggerRun.Smoke);
        Alert(m);
        m.Aim = 0.25f;
        m.Facing = angle;
    }

    private void UpdateMarine(Marine m, float dt)
    {
        m.Moving = false;
        if (m.Flash > 0)
            m.Flash -= dt;
        if (m.Popped > 0)
            m.Popped -= dt;

        if (m.State == State.Dead)
        {
            UpdateCorpse(m, dt);
            return;
        }

        if (Run.Over)
            return;

        var rate = Notice(m);
        var aware = m.State is State.Hunt or State.Search;
        if (m.State != State.Combat)
        {
            if (rate > 0)
            {
                m.Meter += rate * (aware ? AwareFactor : 1) * dt;
                m.Suspect = _player;
                if (m.State is State.Patrol or State.Return)
                    m.State = State.Suspect;
            }
            else if (m.State == State.Suspect)
            {
                // he goes over to look while it fades slowly, and gives it up faster once there
                var there = Vector2.Distance(m.Suspect, m.At) < 10 || m.Tower;
                m.Meter -= (there ? Decay : Decay * 0.3f) * dt;
            }

            if (m.Meter >= 1)
            {
                Alert(m);
                return;
            }
        }

        switch (m.State)
        {
            case State.Patrol:
                Patrol(m, dt);
                CheckBodies(m);
                break;
            case State.Return:
                if (m.Tower || GoTo(m, m.Route[m.Leg], PatrolSpeed, dt))
                {
                    m.State = State.Patrol;
                    m.Pause = 0;
                }

                CheckBodies(m);
                break;
            case State.Suspect:
                // turn to where he saw something; walk over once he is fairly sure
                Turn(m, MathF.Atan2(m.Suspect.Y - m.At.Y, m.Suspect.X - m.At.X), TurnRate * dt);
                if (rate == 0 && m.Meter > 0.2f && Vector2.Distance(m.Suspect, m.At) > 10)
                    GoTo(m, m.Suspect, PatrolSpeed, dt);

                if (m.Meter <= 0)
                {
                    m.Meter = 0;
                    m.State = State.Return;
                    m.Repath = 0;
                    Run.Pop("?", m.At - new Vector2(0, 34), CMUHuggerRun.Amber, 1);
                }

                CheckBodies(m);
                break;
            case State.Hunt:
                if (GoTo(m, m.Hunt, HuntSpeed, dt))
                {
                    m.State = State.Search;
                    m.Search = 0;
                    m.SearchAngle = m.Facing;
                }

                break;
            case State.Search:
                m.Search += dt;
                Turn(m, m.SearchAngle + MathF.Sin(m.Search * 2.2f) * 1.4f, TurnRate * dt);
                if (m.Search > SearchTime && !AnyCombat())
                    GiveUp(m);
                break;
            case State.Combat:
                Combat(m, rate > 0 || m.Tower && _lightSees, dt);
                break;
        }

        if (m.Moving)
            m.Gait += dt * (m.State is State.Hunt or State.Combat ? 11 : 7);
    }

    /// <summary>Keeps marines from walking through each other.</summary>
    private void Separate()
    {
        const float least = MarineRadius * 2 + 2;
        for (var i = 0; i < _marineCount; i++)
        {
            var a = _marines[i];
            if (a.State == State.Dead || a.Tower)
                continue;

            for (var j = i + 1; j < _marineCount; j++)
            {
                var b = _marines[j];
                if (b.State == State.Dead || b.Tower)
                    continue;

                var delta = b.At - a.At;
                var d = delta.Length();
                if (d >= least)
                    continue;

                var normal = d > 0.01f ? delta / d : Vector2.UnitX;
                var push = (least - d) / 2;
                a.At -= normal * push;
                b.At += normal * push;
                PushOut(ref a.At, MarineRadius, true);
                PushOut(ref b.At, MarineRadius, true);
            }
        }
    }

    /// <summary>How fast this marine is noticing the runner right now (meter per second), 0 if he is not. Sets <see cref="Marine.Sees"/>.</summary>
    private float Notice(Marine m)
    {
        m.Sees = false;
        if (_dead || _clearing >= 0)
            return 0;

        var delta = _player - m.At;
        var dist = delta.Length();
        var moving = _velocity.Length() > 20 || _pounce > 0;
        var heard = !m.Tower && dist < HearRange && moving ? HearRate : 0;
        var range = RangeOf(m);
        var half = m.Tower ? TowerHalf : ConeHalf;
        if (dist > range)
            return heard;

        var angle = MathF.Atan2(delta.Y, delta.X);
        var off = MathF.Abs(Wrap(angle - m.Facing));
        if (off > half)
            return heard;

        if (Cast(m.At, angle, dist, m.Tower) < dist - 0.5f)
            return heard;

        m.Sees = true;
        return MathF.Max(heard, NoticeRate * (1.25f - dist / range) * (1.2f - 0.6f * off / half));
    }

    private void Patrol(Marine m, float dt)
    {
        if (m.RouteCount == 1)
        {
            var post = m.Route[0];
            if (!m.Tower && Vector2.Distance(post, m.At) > 2)
            {
                GoTo(m, post, PatrolSpeed, dt);
                return;
            }

            Turn(m, m.Look + MathF.Sin(_time * 0.8f + m.Phase) * m.Sweep, TurnRate * 0.5f * dt);
            return;
        }

        if (m.Pause > 0)
        {
            m.Pause -= dt;
            Turn(m, m.LookAt + MathF.Sin(m.Pause * 3) * 0.6f, TurnRate * 0.6f * dt);
            return;
        }

        var point = m.Route[m.Leg];
        if (!GoTo(m, point, PatrolSpeed, dt))
            return;

        var next = m.Route[(m.Leg + 1) % m.RouteCount];
        m.LookAt = MathF.Atan2(next.Y - point.Y, next.X - point.X);
        m.Leg = (m.Leg + 1) % m.RouteCount;
        m.Pause = m.Wait;
        m.Repath = 0;
    }

    /// <summary>A marine who sees a body goes to look, and the others near it come too.</summary>
    private void CheckBodies(Marine m)
    {
        for (var i = 0; i < _marineCount; i++)
        {
            var body = _marines[i];
            if (body.State != State.Dead || body.Found || body == m)
                continue;

            var delta = body.At - m.At;
            var dist = delta.Length();
            if (dist > ConeRange)
                continue;

            var angle = MathF.Atan2(delta.Y, delta.X);
            if (MathF.Abs(Wrap(angle - m.Facing)) > ConeHalf)
                continue;

            if (Cast(m.At, angle, dist, m.Tower) < dist - 0.5f)
                continue;

            body.Found = true;
            Run.PlayAny(m.Person.Female ? FemaleGasps : MaleGasps, -2);
            Run.Pop("!?", m.At - new Vector2(0, 34), CMUHuggerRun.Amber, 1);
            for (var j = 0; j < _marineCount; j++)
            {
                var o = _marines[j];
                if (o.State is State.Dead or State.Combat)
                    continue;

                if (o != m && Vector2.Distance(o.At, body.At) > 160)
                    continue;

                StartHunt(o, body.At);
            }

            return;
        }
    }

    private void StartHunt(Marine m, Vector2 at)
    {
        m.Meter = 0;
        if (m.Tower)
        {
            m.State = State.Search;
            m.Search = 0;
            m.SearchAngle = MathF.Atan2(at.Y - m.At.Y, at.X - m.At.X);
            return;
        }

        m.State = State.Hunt;
        m.Hunt = at;
        Plan(m, at);
    }

    private void GiveUp(Marine m)
    {
        m.State = State.Return;
        m.Meter = 0;
        m.Repath = 0;
        Run.Pop("?", m.At - new Vector2(0, 34), CMUHuggerRun.Amber, 1.2f);
        if (Vector2.Distance(m.At, _player) >= 200)
            return;

        Run.Play(Asks[Run.Rng.Next(Asks.Length)], -8);
        Run.Pend(0.4f, m.Person.Female ? FemaleSigh : MaleSigh, -6);
    }

    private bool AnyCombat()
    {
        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State == State.Combat)
                return true;
        }

        return false;
    }

    /// <summary>The marine is sure: he shouts, radios it in, and the others come running.</summary>
    private void Alert(Marine m)
    {
        var first = !AnyCombat();
        m.State = State.Combat;
        m.Meter = 1;
        m.Popped = 0.9f;
        m.Aim = AlertAim;
        m.Burst = 0;
        m.Cooldown = 0;
        m.Lost = 0;
        _last = _player;
        _seen = true;
        if (first)
            Run.Play(Spotted, -4);

        Run.PlayAny(m.Person.Female ? FemaleGasps : MaleGasps, -2);
        Run.Play(Shouts[Run.Rng.Next(Shouts.Length)], -4, 0.85f);
        Run.Pend(0.35f, Radio, -10);
        for (var i = 0; i < _marineCount; i++)
        {
            var o = _marines[i];
            if (o == m || o.State is State.Dead or State.Combat)
                continue;

            StartHunt(o, _player);
        }
    }

    private void Combat(Marine m, bool sees, float dt)
    {
        if (sees)
        {
            _last = _player;
            m.Lost = 0;
            Turn(m, MathF.Atan2(_player.Y - m.At.Y, _player.X - m.At.X), TurnRate * 1.6f * dt);

            // everyone else hunting hears where the runner is
            for (var i = 0; i < _marineCount; i++)
            {
                if (_marines[i].State == State.Hunt)
                    _marines[i].Hunt = _player;
            }
        }
        else
        {
            m.Lost += dt;
            if (m.Lost > LoseTime)
            {
                StartHunt(m, _last);
                return;
            }
        }

        var dist = Vector2.Distance(_player, m.At);
        if (sees && dist < FireRange)
        {
            if (m.Burst > 0)
            {
                m.BurstTimer -= dt;
                if (m.BurstTimer <= 0)
                {
                    Fire(m);
                    m.Burst--;
                    m.BurstTimer = BurstGap;
                    if (m.Burst == 0)
                        m.Cooldown = FireCooldown;
                }
            }
            else if (m.Cooldown > 0)
            {
                m.Cooldown -= dt;
            }
            else
            {
                m.Aim -= dt;
                if (m.Aim <= 0)
                {
                    m.Burst = BurstSize;
                    m.BurstTimer = 0;
                    m.Aim = AimTime * 0.7f;
                }
            }

            return;
        }

        m.Burst = 0;
        m.Aim = MathF.Min(AimTime, m.Aim + dt);
        if (!m.Tower)
            GoTo(m, _last, HuntSpeed, dt);
    }

    private void Fire(Marine m)
    {
        var angle = MathF.Atan2(_player.Y - m.At.Y, _player.X - m.At.X) + (Run.Fx() - 0.5f) * 2 * Spread;
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var lift = m.Tower ? Chest + Lift : Chest;
        if (_bulletCount < MaxBullets)
        {
            _bullets[_bulletCount++] = new Bullet { At = m.At + dir * 9, Velocity = dir * BulletSpeed, Life = BulletLife, Lift = lift };
        }

        m.Flash = 0.06f;
        m.Facing = angle;
        Run.Play(Gun, -5, 0.92f + Run.Fx() * 0.16f);
        var muzzle = m.At + dir * 11 - new Vector2(0, lift);
        Run.Emit(muzzle, 4, CMUHuggerRun.TracerGlow.WithAlpha(0.9f), 120, 0.12f, 1, angle, 0.8f, kind: CMUHuggerRun.Spark);
        Run.Emit(muzzle, 2, CMUHuggerRun.Dust.WithAlpha(0.35f), 15, 0.5f, 2, angle, 1, kind: CMUHuggerRun.Smoke);
    }

    private void UpdateBullets(float dt)
    {
        for (var i = _bulletCount - 1; i >= 0; i--)
        {
            ref var b = ref _bullets[i];
            b.At += b.Velocity * dt;
            b.Life -= dt;
            var gone = b.Life <= 0;
            if (!gone && StopsBullets(TileAt(b.At)))
            {
                gone = true;
                var back = MathF.Atan2(-b.Velocity.Y, -b.Velocity.X);
                var at = b.At - new Vector2(0, b.Lift);
                Run.Emit(at, 5, CMUHuggerRun.TracerCore, 140, 0.18f, 1, back, 1.6f, kind: CMUHuggerRun.Spark);
                Run.Emit(at, 2, CMUHuggerRun.Dust.WithAlpha(0.4f), 15, 0.5f, 2, back, 1, kind: CMUHuggerRun.Smoke);
                Run.Play(Ricochet, -10, 0.9f + Run.Fx() * 0.2f);
            }

            if (!gone && !_dead && _clearing < 0 && Vector2.Distance(b.At, _player) < RunnerRadius + 2)
            {
                gone = true;
                if (_grace <= 0)
                    Hurt(b.Velocity / BulletSpeed);
            }

            if (gone)
                _bullets[i] = _bullets[--_bulletCount];
        }
    }

    private void Hurt(Vector2 dir)
    {
        var angle = MathF.Atan2(dir.Y, dir.X);
        _health--;
        _grace = Grace;
        _velocity += dir * 120;
        var at = _player - new Vector2(0, 6);
        Run.Emit(at, 16, CMUHuggerRun.Acid, 150, 0.6f, 2, angle, 1.8f, stains: true, acid: true, drag: 5, flat: 1);
        Run.Emit(at, 6, CMUHuggerRun.Acid, 240, 0.2f, 1, angle, 1.2f, kind: CMUHuggerRun.Spark);
        Run.Splat(_player, 14, CMUHuggerRun.Acid.WithAlpha(0.85f), 1, true);
        Run.PlayAny(Flesh);
        if (_health <= 0)
        {
            Die(angle);
            return;
        }

        Run.PlayAny(Pain, -4);
        Run.Flash(CMUHuggerRun.Acid.WithAlpha(0.25f), 0.2f);
        Run.Shake(3, 0.25f);
    }

    private void Die(float angle)
    {
        _dead = true;
        _pounce = 0;
        _maul = 0;
        var at = _player - new Vector2(0, 6);
        Run.Emit(at, 50, CMUHuggerRun.Acid, 200, 0.9f, 2.4f, angle, 2.6f, stains: true, acid: true, drag: 4, flat: 1);
        Run.Emit(at, 16, CMUHuggerRun.DarkAcid, 120, 0.8f, 3.2f, angle, 3, stains: true, acid: true, drag: 4, flat: 1);
        Run.Emit(at, 8, CMUHuggerRun.Acid.WithAlpha(0.4f), 30, 1, 4, -MathF.PI / 2, 1.5f, -20, kind: CMUHuggerRun.Smoke);
        Run.Splat(_player + new Vector2(0, 2), 40, CMUHuggerRun.Acid.WithAlpha(0.9f), 1, true);
        Run.Splat(_player + new Vector2(-12, 6), 18, CMUHuggerRun.Acid.WithAlpha(0.85f), 1, true);
        Run.Play(Death);
        Run.Flash(CMUHuggerRun.Acid.WithAlpha(0.35f), 0.3f);
        Run.Shake(5, 0.45f);
        Run.SlowMotion(0.5f);
        Run.Lose(_deadText, CMUHuggerRun.Acid);
    }

    private void UpdateCorpse(Marine m, float dt)
    {
        m.Die += dt;
        m.Fall = MathF.Min(1, m.Die / 0.32f);

        // the pool spreads for a moment, then stays
        if (m.Pool >= PoolMarks.Length || m.Die < PoolMarks[m.Pool])
            return;

        var size = 22 + m.Pool * 6;
        var at = m.At + new Vector2((Run.Fx() - 0.5f) * 6 + m.FallDirection * 4, (Run.Fx() - 0.5f) * 4);
        Run.Splat(at, size, (m.Pool < 2 ? CMUHuggerRun.Blood : CMUHuggerRun.DarkBlood).WithAlpha(0.8f), 1);
        m.Pool++;
    }

    private void UpdateLight(float dt)
    {
        _lightSees = false;
        var tower = _tower;
        if (!_spec.Light || tower == null || Run.Over)
            return;

        Vector2 target;
        var speed = LightSpeed;
        if (tower.State == State.Combat)
        {
            target = _last;
            speed = LightChase;
        }
        else if (tower.State == State.Search)
        {
            target = _last + new Vector2(MathF.Cos(_time * 1.6f) * 40, MathF.Sin(_time * 2.1f) * 30);
        }
        else
        {
            target = new Vector2(_ox + (_spec.LightCentre.X + MathF.Cos(_time * 0.4f) * _spec.LightRadii.X) * Tile,
                (_spec.LightCentre.Y + MathF.Sin(_time * 0.6f) * _spec.LightRadii.Y) * Tile);
        }

        var delta = target - _light;
        var d = delta.Length();
        if (d > 0.5f)
            _light += delta / d * MathF.Min(d, speed * dt);

        if (_dead || _clearing >= 0)
            return;

        if (Vector2.Distance(_player, _light) >= LightRadius)
            return;

        _lightSees = true;
        if (tower.State != State.Combat)
            Alert(tower);
    }

    private void UpdateAmbience(float dt)
    {
        _wind -= dt;
        if (_wind <= 0)
        {
            _wind = 8 + Run.Fx() * 5;
            Run.PlayAny(Winds, -17);
        }

        if (Run.Fx() < dt * 2.5f)
        {
            var y = _cam.Y + Run.Fx() * ViewHeight;
            Run.Emit(new Vector2(_ox + _cam.X - 4, y), 1, CMUHuggerRun.Dust.WithAlpha(0.12f), 90, 3.5f, 3, 0.1f, 0.3f, kind: CMUHuggerRun.Smoke);
        }

        // the nearest walking marine's boots on the sand
        _step -= dt;
        if (_step > 0)
            return;

        Marine? nearest = null;
        var best = 110f;
        for (var i = 0; i < _marineCount; i++)
        {
            var m = _marines[i];
            if (!m.Moving)
                continue;

            var d = Vector2.Distance(m.At, _player);
            if (d < best)
            {
                best = d;
                nearest = m;
            }
        }

        if (nearest == null)
        {
            _step = 0.1f;
            return;
        }

        _step = nearest.State is State.Hunt or State.Combat ? 0.3f : 0.45f;
        Run.PlayAny(Steps, -14 - best / 10);
    }

    #endregion

    #region Clearing a room

    private void StartClear()
    {
        _clearing = 0;
        _tallied = 0;
        _bulletCount = 0;
        _ghost = !_seen;
        _timePoints = Math.Min(TimeCap, Math.Max(0, (int) MathF.Round((_spec.Par - _roomTime) * TimeRate / 10) * 10));
        _clearText = Loc.GetString("cmu-game-hugger-stealth-cleared", ("points", ClearPoints));
        _ghostText = _ghost ? Loc.GetString("cmu-game-hugger-stealth-ghost", ("points", GhostPoints)) : _spottedText;
        _timeText = Loc.GetString("cmu-game-hugger-stealth-time", ("points", _timePoints));
        _killsText = Loc.GetString("cmu-game-hugger-stealth-kills", ("count", _kills));
        Run.PlayAny(Hisses, -4);
    }

    private void UpdateClear(float dt)
    {
        _clearing += dt;
        if (_clearing < ExitWalk)
        {
            _facing = MathF.Atan2(_exitDirection.Y, _exitDirection.X);
            _player += _exitDirection * RunSpeed * 0.8f * dt;
            _walk += RunSpeed * 0.8f * dt * 0.22f;
        }

        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State == State.Dead)
                UpdateCorpse(_marines[i], dt);
        }

        var steps = (int) MathF.Floor((_clearing - ExitWalk) / TallyStep) + 1;
        while (_tallied < Math.Min(steps, 3))
        {
            _tallied++;
            if (_tallied == 1)
            {
                Run.AddScore(ClearPoints);
                Run.Play(Cleared, -4);
            }
            else if (_tallied == 2)
            {
                if (_ghost)
                {
                    Run.AddScore(GhostPoints);
                    Run.Play(Ghosted, -4);
                }
            }
            else if (_timePoints > 0)
            {
                Run.AddScore(_timePoints);
                Run.Play(TallyBeep, -6, 1.3f);
            }
        }

        if (_clearing < ExitWalk + TallyTime)
            return;

        if (_room >= Rooms.Length - 1)
        {
            Run.NextStage();
            return;
        }

        if (!_fadeOut)
        {
            _fadeOut = true;
            _fade = 0;
        }

        _fade += dt;
        if (_fade >= RoomFade)
            LoadRoom(_room + 1);
    }

    #endregion

    #region Drawing

    /// <summary>World to screen.</summary>
    private Vector2 Screen(Vector2 world)
    {
        return world - Run.Camera;
    }

    private static RsiDirection PersonDirection(float angle)
    {
        var c = MathF.Cos(angle);
        if (c > 0.707f)
            return RsiDirection.East;
        if (c < -0.707f)
            return RsiDirection.West;

        return MathF.Sin(angle) > 0 ? RsiDirection.South : RsiDirection.North;
    }

    /// <summary>The runner shows its long side view for diagonals too.</summary>
    private static RsiDirection RunnerDirection(float angle)
    {
        var c = MathF.Cos(angle);
        if (c > 0.6f)
            return RsiDirection.East;
        if (c < -0.6f)
            return RsiDirection.West;

        return MathF.Sin(angle) > 0 ? RsiDirection.South : RsiDirection.North;
    }

    public override void DrawBack(DrawingHandleScreen handle)
    {
        var cam = Run.Camera;
        Run.Fill(handle, 0, CMUHuggerRun.HudHeight, CMUHuggerRun.ScreenWidth, ViewHeight, Color.Black);
        var x0 = Math.Max(0, (int) MathF.Floor((cam.X - _ox) / Tile));
        var x1 = Math.Min(_w - 1, (int) MathF.Floor((cam.X - _ox + CMUHuggerRun.ScreenWidth) / Tile));
        var y0 = Math.Max(0, (int) MathF.Floor((cam.Y + CMUHuggerRun.HudHeight) / Tile));
        var y1 = Math.Min(_h - 1, (int) MathF.Floor((cam.Y + CMUHuggerRun.ScreenHeight) / Tile));
        _floors ??= LoadFloors();

        // the sand, with the faint grid of a training floor
        var grid = CMUHuggerRun.PhosphorGreen.WithAlpha(0.04f);
        var tile = new Vector2(Tile, Tile);
        for (var ty = y0; ty <= y1; ty++)
        {
            for (var tx = x0; tx <= x1; tx++)
            {
                var t = TileKind(tx, ty);
                var x = _ox + tx * Tile - cam.X;
                var y = ty * Tile - cam.Y;
                var n = _room * 4099 + ty * MaxW + tx;
                if (t == Exit)
                {
                    DrawExit(handle, x, y);
                    continue;
                }

                Run.DrawSprite(handle, _floors[(int) (CMUHuggerRun.Hash01(n, 1) * 5.999f)], new Vector2(x + Tile / 2, y + Tile / 2), tile, 0, FloorTint);
                Run.Fill(handle, x, y, Tile, 1, grid);
                Run.Fill(handle, x, y, 1, Tile, grid);
            }
        }

        // shadows, then the cones on the sand, then the cover over them
        for (var ty = y0; ty <= y1; ty++)
        {
            for (var tx = x0; tx <= x1; tx++)
            {
                var t = TileKind(tx, ty);
                var x = _ox + tx * Tile - cam.X;
                var y = ty * Tile - cam.Y;
                if (t == Crate)
                    Run.Fill(handle, x + 3, y + 3, Tile, Tile, Color.Black.WithAlpha(0.3f));
                else if (t == Sandbag)
                    Run.Ellipse(handle, new Vector2(x + 9, y + 14), 8, 3, Color.Black.WithAlpha(0.35f));
                else if (t == Barrel)
                    Run.Ellipse(handle, new Vector2(x + 9, y + 14), 6, 2.5f, Color.Black.WithAlpha(0.35f));
            }
        }

        if (_tower != null)
            Run.Ellipse(handle, Screen(_tower.At) + new Vector2(2, 14), 20, 5, Color.Black.WithAlpha(0.35f));

        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State != State.Dead)
                DrawCone(handle, _marines[i]);
        }

        if (_spec.Light)
            DrawLightSpot(handle);

        for (var ty = y0; ty <= y1; ty++)
        {
            for (var tx = x0; tx <= x1; tx++)
            {
                var t = TileKind(tx, ty);
                var x = _ox + tx * Tile - cam.X;
                var y = ty * Tile - cam.Y;
                var n = _room * 4099 + ty * MaxW + tx;
                switch (t)
                {
                    case Scrub:
                        DrawScrub(handle, x, y, n);
                        break;
                    case Rock:
                        DrawSmooth(handle, RockRsi, RockStates, _corners[ty * MaxW + tx], x, y);
                        break;
                    case Wall:
                        DrawSmooth(handle, WallRsi, WallStates, _corners[ty * MaxW + tx], x, y);
                        break;
                    case Crate:
                        DrawCrate(handle, tx, ty, x, y, n);
                        break;
                    case Barrel:
                        DrawObject(handle, BarrelRsi, Barrels[(int) (CMUHuggerRun.Hash01(n, 12) * 3.999f)], RsiDirection.South, x, y);
                        break;
                    case Fence:
                        DrawFence(handle, x, y, _fence[ty * MaxW + tx]);
                        break;
                    case Sandbag:
                        DrawObject(handle, SandbagRsi, "sandbag", SandbagDirection(tx, ty), x, y);
                        break;
                }
            }
        }

        // boulders on the lone outcrops
        for (var i = 0; i < _boulderCount; i++)
        {
            var tx = _boulders[i * 2];
            var ty = _boulders[i * 2 + 1];
            var at = Screen(new Vector2(_ox + (tx + 1) * Tile, (ty + 1) * Tile - 2));
            if (at.X < -Tile * 2 || at.X > CMUHuggerRun.ScreenWidth + Tile * 2 || at.Y < 0 || at.Y > CMUHuggerRun.ScreenHeight + Tile * 2)
                continue;

            Run.DrawSprite(handle, Run.Frame(BoulderRsi, Boulders[(tx + ty) & 1]), at, tile * 2, 0, CMUHuggerRun.Phosphor);
        }

        // the wreck, over its footprint of 4 x 2 tiles
        if (_spec.HasWreck)
        {
            var at = Screen(new Vector2(_ox + (_spec.WreckAt.X + 2) * Tile, (_spec.WreckAt.Y + 2) * Tile)) + new Vector2(0, 2 - WreckHeight / 4);
            Run.DrawSprite(handle, Run.Frame(WreckRsi, "car_pileup"), at, new Vector2(WreckWidth, WreckHeight) / 2, 0, CMUHuggerRun.Phosphor);
        }
    }

    private static Texture[] LoadFloors()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        var floors = new Texture[Floors.Length];
        for (var i = 0; i < Floors.Length; i++)
        {
            floors[i] = cache.GetTexture(Floors[i]);
        }

        return floors;
    }

    /// <summary>A 32 px game sprite filling one tile.</summary>
    private void DrawObject(DrawingHandleScreen handle, string rsi, string state, RsiDirection direction, float x, float y)
    {
        Run.DrawSprite(handle, Run.Frame(rsi, state, direction), new Vector2(x + Tile / 2, y + Tile / 2), new Vector2(Tile, Tile), 0,
            CMUHuggerRun.Phosphor);
    }

    /// <summary>A corner-smoothed wall tile: four quarter sprites, each its corner's state, facing the corner's way.</summary>
    private void DrawSmooth(DrawingHandleScreen handle, string rsi, string[] states, ushort corners, float x, float y)
    {
        DrawObject(handle, rsi, states[corners & 7], RsiDirection.South, x, y);
        DrawObject(handle, rsi, states[corners >> 3 & 7], RsiDirection.East, x, y);
        DrawObject(handle, rsi, states[corners >> 6 & 7], RsiDirection.North, x, y);
        DrawObject(handle, rsi, states[corners >> 9 & 7], RsiDirection.West, x, y);
    }

    /// <summary>
    /// Cargo: a block two crate tiles tall is a row of containers, drawn from its top tile (left, middle and right
    /// pieces, one colour per row); a crate on its own is a crate.
    /// </summary>
    private void DrawCrate(DrawingHandleScreen handle, int tx, int ty, float x, float y, int n)
    {
        if (TileKind(tx, ty - 1) == Crate)
            return;

        if (TileKind(tx, ty + 1) != Crate)
        {
            DrawObject(handle, Crates[(int) (CMUHuggerRun.Hash01(n, 12) * 3.999f)], "icon", RsiDirection.South, x, y);
            return;
        }

        var start = tx;
        while (TileKind(start - 1, ty) == Crate && TileKind(start - 1, ty + 1) == Crate)
        {
            start--;
        }

        var colour = Containers[(int) (CMUHuggerRun.Hash01(_room * 4099 + ty * MaxW + start, 13) * 2.999f)];
        var left = start == tx;
        var right = !(TileKind(tx + 1, ty) == Crate && TileKind(tx + 1, ty + 1) == Crate);
        var piece = left ? colour[0] : right ? colour[2] : colour[1];
        Run.DrawSprite(handle, Run.Frame(ContainerRsi, piece), new Vector2(x + Tile / 2, y + Tile), new Vector2(Tile, Tile * 2), 0,
            CMUHuggerRun.Phosphor);
    }

    /// <summary>Sandbags run along a line of them: side on (east) when the line runs up and down.</summary>
    private RsiDirection SandbagDirection(int tx, int ty)
    {
        if (TileKind(tx - 1, ty) == Sandbag || TileKind(tx + 1, ty) == Sandbag)
            return RsiDirection.South;

        return TileKind(tx, ty - 1) == Sandbag || TileKind(tx, ty + 1) == Sandbag ? RsiDirection.East : RsiDirection.South;
    }

    private void DrawExit(DrawingHandleScreen handle, float x, float y)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(Run.Clock * 5);
        Run.Fill(handle, x, y, Tile, Tile, ExitColour);
        Run.Fill(handle, x, y, Tile, Tile, CMUHuggerRun.PhosphorGreen.WithAlpha(0.08f + 0.12f * pulse));

        // chevrons pointing out
        var d = _exitDirection;
        var side = new Vector2(-d.Y, d.X);
        var shift = Run.Clock * 12 % 8;
        var colour = CMUHuggerRun.Acid.WithAlpha(0.7f);
        for (var k = 0; k < 2; k++)
        {
            var c = new Vector2(x + 8, y + 8) + d * (k * 8 + shift - 4);
            Run.Line(handle, c - d * 3 + side * 3, c, colour);
            Run.Line(handle, c - d * 3 - side * 3, c, colour);
        }
    }

    /// <summary>A cactus or a few stones on the sand.</summary>
    private void DrawScrub(DrawingHandleScreen handle, float x, float y, int n)
    {
        var k = (int) (CMUHuggerRun.Hash01(n, 8) * 3.999f);
        if (CMUHuggerRun.Hash01(n, 9) < 0.5f)
            DrawObject(handle, CactusRsi, Cacti[k], RsiDirection.South, x, y);
        else
            DrawObject(handle, StoneRsi, Stones[k], RsiDirection.South, x, y);
    }

    /// <summary>The fence sprite joined to its neighbours: the game's bits are north 1, south 2, east 4, west 8.</summary>
    private void DrawFence(DrawingHandleScreen handle, float x, float y, byte bits)
    {
        var joins = ((bits & 4) != 0 ? 1 : 0) | ((bits & 8) != 0 ? 2 : 0) | ((bits & 2) != 0 ? 4 : 0) | ((bits & 1) != 0 ? 8 : 0);
        DrawObject(handle, FenceRsi, FenceStates[joins], RsiDirection.South, x, y);
    }

    private static Color ConeColour(Marine m)
    {
        return m.State switch
        {
            State.Combat => CMUHuggerRun.Alarm,
            State.Hunt or State.Search => CMUHuggerRun.Amber,
            State.Suspect => m.Meter > 0.5f ? CMUHuggerRun.Amber : CMUHuggerRun.PhosphorGreen,
            _ => CMUHuggerRun.PhosphorGreen,
        };
    }

    private void DrawCone(DrawingHandleScreen handle, Marine m)
    {
        var o = Screen(m.At);
        var half = m.Tower ? TowerHalf : ConeHalf;
        var colour = ConeColour(m);
        var fill = colour.WithAlpha(m.State == State.Combat ? 0.16f : 0.11f);
        var core = RangeOf(m) * 0.5f;
        var edge = colour.WithAlpha(0.3f);
        var previous = Vector2.Zero;
        var previousCore = Vector2.Zero;
        for (var i = 0; i <= Rays; i++)
        {
            var a = m.Facing - half + 2 * half * i / Rays;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var point = o + dir * m.Rays[i];
            var inner = o + dir * MathF.Min(m.Rays[i], core);
            if (i > 0)
            {
                Run.Quad(handle, o, previous, point, point, fill);

                // a brighter core near the marine
                Run.Quad(handle, o, previousCore, inner, inner, fill);
                Run.Line(handle, previous, point, edge);
            }

            previous = point;
            previousCore = inner;
        }

        var side = colour.WithAlpha(0.22f);
        Run.Line(handle, o, o + new Vector2(MathF.Cos(m.Facing - half), MathF.Sin(m.Facing - half)) * m.Rays[0], side);
        Run.Line(handle, o, o + new Vector2(MathF.Cos(m.Facing + half), MathF.Sin(m.Facing + half)) * m.Rays[Rays], side);
    }

    private void DrawLightSpot(DrawingHandleScreen handle)
    {
        var at = Screen(_light);
        var hot = _tower is { State: State.Combat };
        var flicker = 0.92f + 0.08f * MathF.Sin(Run.Clock * 23);
        var colour = hot ? CMUHuggerRun.Alarm : CMUHuggerRun.Bright;
        Run.Circle(handle, at, LightRadius + 4, colour.WithAlpha(0.05f * flicker));
        Run.Circle(handle, at, LightRadius, colour.WithAlpha(0.13f * flicker));
        Run.Circle(handle, at, LightRadius * 0.6f, CMUHuggerRun.Bright.WithAlpha(0.1f * flicker));
        Run.Circle(handle, at, LightRadius, colour.WithAlpha(0.35f), false);
    }

    public override void DrawFront(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State == State.Dead)
                DrawCorpse(handle, _marines[i]);
        }

        if (_dead)
            DrawDeadRunner(handle);

        // walkers back to front: marine indices, and -1 for the runner; the tower sorts by its base
        var count = 0;
        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State != State.Dead)
                _order[count++] = i;
        }

        if (!_dead)
            _order[count++] = -1;

        for (var i = 1; i < count; i++)
        {
            var item = _order[i];
            var y = Depth(item);
            var j = i - 1;
            while (j >= 0 && Depth(_order[j]) > y)
            {
                _order[j + 1] = _order[j];
                j--;
            }

            _order[j + 1] = item;
        }

        for (var i = 0; i < count; i++)
        {
            if (_order[i] < 0)
                DrawRunner(handle);
            else if (_marines[_order[i]].Tower)
                DrawTower(handle);
            else
                DrawMarine(handle, _marines[_order[i]], Screen(_marines[_order[i]].At));
        }

        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State != State.Dead)
                DrawLaser(handle, _marines[i]);
        }

        var glow = CMUHuggerRun.TracerGlow.WithAlpha(0.3f);
        for (var i = 0; i < _bulletCount; i++)
        {
            var b = _bullets[i];
            var head = Screen(b.At) - new Vector2(0, b.Lift);
            var tail = head - Vector2.Normalize(b.Velocity) * Tracer;
            Run.Beam(handle, tail, head, 4, glow);
            Run.Beam(handle, tail, head, 1.6f, CMUHuggerRun.TracerCore);
        }

        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State != State.Dead && _marines[i].Flash > 0)
                DrawMuzzle(handle, _marines[i]);
        }

        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State != State.Dead)
                DrawGlyph(handle, _marines[i]);
        }
    }

    private float Depth(int item)
    {
        if (item < 0)
            return _player.Y;

        var m = _marines[item];
        return m.Tower ? m.At.Y + Tile : m.At.Y;
    }

    private void DrawMarine(DrawingHandleScreen handle, Marine m, Vector2 at)
    {
        var bob = m.Moving ? MathF.Abs(MathF.Sin(m.Gait)) * 1.2f : 0;
        var sway = m.Moving ? MathF.Sin(m.Gait) * 0.05f : 0;
        Run.Ellipse(handle, at + new Vector2(0, 3), 6, 2.5f, Color.Black.WithAlpha(0.45f));
        Run.DrawPerson(handle, m.Person, at + new Vector2(0, 4 - bob), PersonDirection(m.Facing), MarineScale, sway);
    }

    private void DrawCorpse(DrawingHandleScreen handle, Marine m)
    {
        var at = Screen(m.At);
        var fall = m.Fall * m.Fall;
        Run.DrawPerson(handle, m.Person, at + new Vector2(m.FallDirection * 3 * fall, -(10 + (2 - 10) * fall)), PersonDirection(m.Facing),
            MarineScale, m.FallDirection * fall * MathF.PI / 2, true);
    }

    private void DrawRunner(DrawingHandleScreen handle)
    {
        var at = Screen(_player);
        var dir = RunnerDirection(_facing);
        var speed = _velocity.Length();
        var moving = speed > 10 && _pounce <= 0;
        var leap = _pounce > 0 ? MathF.Sin((1 - _pounce / PounceTime) * MathF.PI) : 0;
        var scale = RunnerScale * (1 + 0.12f * leap);
        var box = 64 * scale;
        var bob = moving ? MathF.Abs(MathF.Sin(_walk)) * 1.5f : 0;
        var squash = moving ? 0.04f * MathF.Sin(_walk * 2) : 0;
        var side = dir is RsiDirection.East or RsiDirection.West;
        var tilt = moving && side ? MathF.Sin(_walk) * 0.04f : 0;
        var shift = dir == RsiDirection.South ? 4 : dir == RsiDirection.North ? -3.5f : 0;
        var lift = leap * 8 + bob;
        Run.Ellipse(handle, at + new Vector2(0, 3), side ? 17 : 10, 3.2f, Color.Black.WithAlpha(0.45f - 0.2f * leap));

        var texture = Run.Frame(CMUHuggerRun.Runner, "alive", dir);

        // ghosts of the leap
        var ghost = CMUHuggerRun.Phosphor.WithAlpha(0.18f);
        for (var i = 0; i < _trailCount; i++)
        {
            var t = Screen(_trail[i]);
            Run.DrawSprite(handle, texture, new Vector2(t.X - shift * scale, t.Y + 3 - 14 * scale - lift * 0.5f), new Vector2(box, box), 0, ghost);
        }

        if (_pounce <= 0)
            _trailCount = 0;

        var blink = _grace > 0 && (int) MathF.Floor(_grace * 14) % 2 == 0;
        var stun = _stun > 0 ? MathF.Sin(_stun * 40) * 0.08f : 0;
        Run.DrawSprite(handle, texture, new Vector2(at.X - shift * scale, at.Y + 3 - 14 * scale - lift),
            new Vector2(box * (1 + squash), box * (1 - squash)), tilt + stun, CMUHuggerRun.Phosphor.WithAlpha(blink ? 0.35f : 1));
    }

    private void DrawDeadRunner(DrawingHandleScreen handle)
    {
        const float s = RunnerScale;
        var at = Screen(_player);
        Run.DrawSprite(handle, Run.Frame(CMUHuggerRun.Runner, "dead"), new Vector2(at.X + 3.5f * s, at.Y + 6 * s - 2), new Vector2(64 * s, 64 * s), 0,
            CMUHuggerRun.Phosphor);
    }

    private void DrawLaser(DrawingHandleScreen handle, Marine m)
    {
        if (m.State != State.Combat || m.Burst > 0 || m.Cooldown > 0 || m.Aim >= AimTime || !(m.Sees || m.Tower && _lightSees))
            return;

        var k = 1 - m.Aim / AimTime;
        if (MathF.Sin(Run.Clock * 60) < -0.3f && k < 0.7f)
            return;

        var lift = m.Tower ? Chest + Lift : Chest;
        var from = Screen(m.At + new Vector2(MathF.Cos(m.Facing), MathF.Sin(m.Facing)) * 9) - new Vector2(0, lift);
        var to = Screen(_player) - new Vector2(0, 6);
        Run.Line(handle, from, to, CMUHuggerRun.Laser.WithAlpha(0.25f + 0.5f * k));
        Run.Circle(handle, to, 1.5f, CMUHuggerRun.Laser.WithAlpha(0.6f + 0.4f * k));
    }

    private void DrawMuzzle(DrawingHandleScreen handle, Marine m)
    {
        var k = m.Flash / 0.06f;
        var lift = m.Tower ? Chest + Lift : Chest;
        var at = Screen(m.At + new Vector2(MathF.Cos(m.Facing), MathF.Sin(m.Facing)) * 11) - new Vector2(0, lift);
        Run.Circle(handle, at, 6 * k + 2, CMUHuggerRun.TracerGlow.WithAlpha(0.5f * k));
        Run.Circle(handle, at, 2.5f * k + 1, CMUHuggerRun.TracerCore.WithAlpha(k));
    }

    /// <summary>The watchtower over its 2 x 2 footprint, its guard on the platform, and the searchlight's lamp.</summary>
    private void DrawTower(DrawingHandleScreen handle)
    {
        var m = _tower!;
        var at = Screen(m.At);
        Run.DrawSprite(handle, Run.Frame(TowerRsi, "watchtower"), at + new Vector2(0, Tile - 24), new Vector2(32, 48), 0, CMUHuggerRun.Phosphor);
        DrawMarine(handle, m, at - new Vector2(0, Lift));
        if (!_spec.Light)
            return;

        // the lamp, and its beam to the spot
        var lamp = at + new Vector2(10, -Lift - 12);
        var spot = Screen(_light);
        var angle = MathF.Atan2(spot.Y - lamp.Y, spot.X - lamp.X);
        var across = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * LightRadius * 0.9f;
        Run.Quad(handle, lamp, lamp, spot + across, spot - across, CMUHuggerRun.Bright.WithAlpha(0.05f));
        Run.Circle(handle, lamp, 2.5f, CMUHuggerRun.PhosphorDim);
        Run.Circle(handle, lamp + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 1.5f, 1.5f, CMUHuggerRun.Bright);
    }

    private void DrawGlyph(DrawingHandleScreen handle, Marine m)
    {
        var at = Screen(m.At);
        var x = at.X;
        var y = at.Y - (m.Tower ? Lift + 2 : 0) - 32;
        if (m.State == State.Combat)
        {
            if (m.Popped <= 0)
                return;

            var t = 1 - m.Popped / 0.9f;
            var jump = MathF.Sin(MathF.Min(1, t * 4) * MathF.PI) * 5;
            Run.CentreText(handle, Run.BigFont, "!", x, y - 10 - jump, CMUHuggerRun.Alarm.WithAlpha(MathF.Min(1, m.Popped * 4)));
            return;
        }

        if (m.State is State.Hunt or State.Search)
        {
            Run.CentreText(handle, Run.TextFont, "?", x, y, CMUHuggerRun.Amber.WithAlpha(0.8f));
            return;
        }

        if (m.Meter <= 0)
            return;

        var colour = m.Meter > 0.6f ? CMUHuggerRun.Alarm : CMUHuggerRun.Amber;
        Run.CentreText(handle, Run.TextFont, "!", x, y - 2, colour.WithAlpha(0.4f + 0.6f * m.Meter));
        Run.Fill(handle, x - 6, y + 9, 12, 2, Color.Black.WithAlpha(0.7f));
        Run.Fill(handle, x - 6, y + 9, 12 * Math.Clamp(m.Meter, 0, 1), 2, colour);
    }

    public override void DrawOverlay(DrawingHandleScreen handle)
    {
        const float top = CMUHuggerRun.HudHeight + 2;
        Run.Fill(handle, 0, CMUHuggerRun.HudHeight, CMUHuggerRun.ScreenWidth, 12, Color.Black.WithAlpha(0.4f));
        Run.Text(handle, Run.SmallFont, _roomText, 6, top, CMUHuggerRun.PhosphorGreen.WithAlpha(0.75f));

        // health
        for (var i = 0; i < Health; i++)
        {
            var c = new Vector2(86 + i * 10, top + 5);
            if (i < _health)
            {
                Run.Quad(handle, c + new Vector2(0, -4), c + new Vector2(4, 0), c + new Vector2(0, 4), c + new Vector2(-4, 0), CMUHuggerRun.Acid);
            }
            else
            {
                Run.Quad(handle, c + new Vector2(0, -3), c + new Vector2(3, 0), c + new Vector2(0, 3), c + new Vector2(-3, 0),
                    CMUHuggerRun.DarkAcid.WithAlpha(0.5f));
            }
        }

        var combat = false;
        var caution = false;
        for (var i = 0; i < _marineCount; i++)
        {
            if (_marines[i].State == State.Combat)
                combat = true;
            else if (_marines[i].State is State.Hunt or State.Search)
                caution = true;
        }

        if (_clearing < 0 && !_dead)
        {
            if (combat && Run.Clock % 0.5f < 0.32f)
                Run.CentreText(handle, Run.TextFont, _alertText, 282, top - 1, CMUHuggerRun.Alarm);
            else if (!combat && caution)
                Run.CentreText(handle, Run.TextFont, _cautionText, 282, top - 1, CMUHuggerRun.Amber);
        }

        // the exit's sign
        var sign = Screen(_exit) - _exitDirection * 22 - new Vector2(0, 4);
        if (sign.X > 0 && sign.X < CMUHuggerRun.ScreenWidth && sign.Y > CMUHuggerRun.HudHeight && sign.Y < CMUHuggerRun.ScreenHeight)
        {
            Run.CentreText(handle, Run.SignFont, _exitText, Math.Clamp(sign.X, 16, CMUHuggerRun.ScreenWidth - 16),
                Math.Clamp(sign.Y, CMUHuggerRun.HudHeight + 14, CMUHuggerRun.ScreenHeight - 12),
                CMUHuggerRun.Acid.WithAlpha(0.6f + 0.3f * MathF.Sin(Run.Clock * 5)));
        }

        if (_banner > 0 && _clearing < 0)
        {
            var alpha = Math.Clamp(_banner / 0.5f, 0, 1) * Math.Clamp((BannerTime - _banner) / 0.3f, 0, 1);
            Run.CentreText(handle, Run.TextFont, _bannerText, CMUHuggerRun.ScreenWidth / 2, 196, CMUHuggerRun.PhosphorGreen.WithAlpha(alpha));
        }

        if (_clearing >= ExitWalk)
            DrawTally(handle);

        if (_fade > 0)
        {
            Run.Fill(handle, 0, CMUHuggerRun.HudHeight, CMUHuggerRun.ScreenWidth, ViewHeight,
                Color.Black.WithAlpha(Math.Clamp(_fade / RoomFade, 0, 1)));
        }
    }

    private void DrawTally(DrawingHandleScreen handle)
    {
        var t = _clearing - ExitWalk;
        const float centre = CMUHuggerRun.ScreenWidth / 2;
        Run.Fill(handle, 64, 66, 192, 84, Color.Black.WithAlpha(0.78f));
        Run.Outline(handle, 64, 66, 192, 84, CMUHuggerRun.PhosphorDim);
        Run.CentreText(handle, Run.TextFont, _clearTitle, centre, 72, CMUHuggerRun.Acid);
        if (_tallied >= 1)
            Run.CentreText(handle, Run.SmallFont, _clearText, centre, 92, CMUHuggerRun.Bright);
        if (_tallied >= 2)
            Run.CentreText(handle, Run.SmallFont, _ghostText, centre, 105, _ghost ? CMUHuggerRun.Acid : CMUHuggerRun.PhosphorDim);
        if (_tallied >= 3)
            Run.CentreText(handle, Run.SmallFont, _timeText, centre, 118, CMUHuggerRun.Bright);
        if (t >= TallyStep * 3)
            Run.CentreText(handle, Run.SmallFont, _killsText, centre, 132, CMUHuggerRun.PhosphorDim);
    }

    #endregion
}
