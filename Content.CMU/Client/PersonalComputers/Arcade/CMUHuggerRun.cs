using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Hugger Run: a side-scrolling runner. A facehugger scurries right along a colony corridor that speeds up the
/// further it gets, while marines fire pulse rifle tracers its way to be jumped or ducked. Colonists and marines
/// wander into its path: dropping onto a face infects them and ends the run as a win with a big bonus, while
/// running into their legs gets it kicked flat. Green phosphor corridor, the game's own sprites in colour.
/// </summary>
public sealed class CMUHuggerRun : CMUArcadeScreen
{
    #region Tuning

    // Layout, in screen units: the cabinet screen is 320 by 224 at UI scale 1.
    private const float ScreenWidth = 320;
    private const float ScreenHeight = 224;
    private const float HudHeight = 16;
    private const float CeilingY = 40;
    private const float FloorY = 186;
    private const float HuggerX = 64;

    /// <summary>The parasite's 32 px sprite drawn at 1.25, with the 3 px of air under its feet in that sprite.</summary>
    private const float HuggerSize = 40;
    private const float HuggerFootPad = 3.75f;
    private const float HuggerHalfWidth = 11;
    private const float HuggerHeight = 21;
    private const float DuckHeight = 11;

    /// <summary>Humans are 32 px sprites drawn at 2x with their feet on the floor.</summary>
    private const float HumanSize = 64;

    // How far each layer moves for every unit the floor moves.
    private const float WallDepth = 0.85f;
    private const float OutsideDepth = 0.12f;
    private const float HillDepth = 0.22f;
    private const float PanelWidth = 64;

    private const float StartSpeed = 150;
    private const float MaxSpeed = 400;
    private const float Acceleration = 2.6f;
    private const float UnitsPerPoint = 12;
    private const int Milestone = 100;

    // A quick tap leaps about 45 units and a full hold about 94: high enough to clear a human outright, so a face
    // is something to aim for, not something in the way.
    private const float JumpVelocity = 330;
    private const float Gravity = 2000;
    private const float HoldGravity = 500;
    private const float MaxHold = 0.38f;
    private const float FastFallGravity = 4200;
    private const float JumpBuffer = 0.1f;

    // Fire from off screen comes along three lanes: low is jumped, mid is ducked or jumped high, and high passes
    // over a hugger that keeps its feet on the floor.
    private const float LaneLow = FloorY - 6;
    private const float LaneMid = FloorY - 19;
    private const float LaneHigh = FloorY - 40;
    private const float Warning = 0.38f;
    private const float BulletSpeed = 330;
    private const float BulletSpeedPerRun = 0.5f;
    private const float MarineBulletSpeed = 360;
    private const float BurstGap = 0.085f;
    private const float Tracer = 14;
    private const float FirstShot = 1.4f;

    // Human hit boxes, in units above the floor and either side of the feet.
    private const float HeadTop = 60;
    private const float HeadBottom = 42;
    private const float HeadHalfWidth = 9;
    private const float BodyTop = 44;
    private const float BodyHalfWidth = 7;

    /// <summary>A hugger whose feet are this high when it meets a head lands on the face rather than the chest.</summary>
    private const float LatchHeight = 36;

    private const float AlertRange = 110;
    private const float FirstHuman = 7;
    private const float HumanEveryMin = 9;
    private const float HumanEveryMax = 16;
    private const float MarinesFrom = 35;
    private const int ColonistBonus = 1000;
    private const int MarineBonus = 1500;
    private const float MarineAim = 0.45f;
    private const float MarineReach = 60;

    /// <summary>Marines back off as they fire, so a fast hugger still has to get through a volley or two.</summary>
    private const float MarineBackpedal = 40;
    private const float KickTime = 0.32f;
    private const float Struggle = 0.35f;
    private const float FallTime = 0.5f;

    /// <summary>How long the corridor takes to skid to a stop when the run ends, so the ending stays on screen.</summary>
    private const float StopTime = 0.15f;

    private const float RestartDelay = 0.9f;
    private const float SlowMotion = 0.35f;

    private const int MaxBullets = 32;
    private const int MaxShots = 12;
    private const int MaxHumans = 3;
    private const int MaxParticles = 420;
    private const int MaxDecals = 140;
    private const int MaxPopups = 8;
    private const int MaxFlashes = 8;
    private const int MaxPending = 8;

    private const byte Drop = 0;
    private const byte Spark = 1;
    private const byte Smoke = 2;

    #endregion

    #region Art and sound

    private const string Parasite = "/Textures/_RMC14/Mobs/Xenonids/Parasite/parasite.rsi";
    private const string Splatter = "/Textures/Fluids/splatter.rsi";
    private const string Parts = "/Textures/Mobs/Species/Human/parts.rsi";
    private const string Eyes = "/Textures/Mobs/Customization/eyes.rsi";
    private const string Hair = "/Textures/Mobs/Customization/human_hair.rsi";
    private const string Clothing = "/Textures/_RMC14/Objects/Clothing/";
    private const string Rifle = "/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi";
    private const string FontPath = "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf";

    private static readonly string[] Jumpsuits =
    {
        "Uniforms/Civilian/blue_jumpsuit.rsi", "Uniforms/Civilian/brown_utility.rsi", "Uniforms/Civilian/khaki_workwear.rsi",
        "Uniforms/Civilian/grey_jumpsuit.rsi", "Uniforms/Civilian/green_utility.rsi", "Uniforms/Civilian/blue_workwear.rsi",
        "Uniforms/Civilian/lightbrown_jumpsuit.rsi", "Uniforms/Civilian/doctor.rsi", "Uniforms/Civilian/emt.rsi",
    };

    private static readonly string[] Shoes = { "Shoes/Boots/black.rsi", "Shoes/Boots/brown.rsi", "Shoes/laceup.rsi", "Shoes/leather.rsi" };
    private static readonly string[] MaleHair = { "crewcut", "business", "bedhead", "buzzcut", "afro" };
    private static readonly string[] FemaleHair = { "ponytail", "bob", "bun", "longstraightponytail" };
    private const string MarineUniform = "Uniforms/Marine/standard.rsi";
    private const string MarineBoots = "Shoes/Boots/rmc.rsi";
    private const string MarineArmor = "OuterClothing/Armor/m3/standard.rsi";
    private const string MarineHelmet = "Head/Helmets/m10/standard.rsi";

    private const string Pounce = "/Audio/_RMC14/Xeno/alien_pounce.ogg";
    private const string ParasiteDies = "/Audio/_RMC14/Xeno/alien_parasite_dies.ogg";
    private const string RifleShot = "/Audio/Weapons/Guns/Gunshots/m41.ogg";
    private const string Ricochet = "/Audio/_RMC14/Bullets/bullet_impact1.ogg";
    private const string Squish = "/Audio/Effects/Footsteps/meatslap.ogg";
    private const string Beep = "/Audio/Effects/beep1.ogg";
    private const string NewGame = "/Audio/Effects/Arcade/newgame.ogg";
    private const string Lost = "/Audio/Effects/Arcade/gameover.ogg";
    private const string Won = "/Audio/Effects/Arcade/win.ogg";
    private const string DistantRoar = "/Audio/_RMC14/Xeno/alien_distantroar_3.ogg";

    private static readonly string[] Flesh =
    {
        "/Audio/_RMC14/Bullets/impact_flesh_1.ogg", "/Audio/_RMC14/Bullets/impact_flesh_2.ogg",
        "/Audio/_RMC14/Bullets/impact_flesh_3.ogg", "/Audio/_RMC14/Bullets/impact_flesh_4.ogg",
    };

    private static readonly string[] Sizzles =
    {
        "/Audio/_RMC14/Xeno/acid_sizzle1.ogg", "/Audio/_RMC14/Xeno/acid_sizzle2.ogg",
        "/Audio/_RMC14/Xeno/acid_sizzle3.ogg", "/Audio/_RMC14/Xeno/acid_sizzle4.ogg",
    };

    private static readonly string[] Hisses =
    {
        "/Audio/_RMC14/Xeno/alien_hiss1.ogg", "/Audio/_RMC14/Xeno/alien_hiss2.ogg", "/Audio/_RMC14/Xeno/alien_hiss3.ogg",
    };

    private static readonly string[] Swooshes =
    {
        "/Audio/CMU14/PersonalComputers/Games/swoosh_1.ogg", "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg",
        "/Audio/CMU14/PersonalComputers/Games/swoosh_3.ogg",
    };

    private static readonly string[] Punches =
    {
        "/Audio/CMU14/PersonalComputers/Games/punch_1.ogg", "/Audio/CMU14/PersonalComputers/Games/punch_2.ogg",
    };

    private const string BodyFall = "/Audio/CMU14/PersonalComputers/Games/fall.ogg";

    private static readonly string[] Steps =
    {
        "/Audio/Effects/Footsteps/plating1.ogg", "/Audio/Effects/Footsteps/plating2.ogg", "/Audio/Effects/Footsteps/plating3.ogg",
        "/Audio/Effects/Footsteps/plating4.ogg", "/Audio/Effects/Footsteps/plating5.ogg",
    };

    private static readonly string[] MaleGasps = { "/Audio/Effects/Gasp/gasp_male1.ogg", "/Audio/Effects/Gasp/gasp_male2.ogg" };

    private static readonly string[] FemaleGasps =
    {
        "/Audio/Effects/Gasp/gasp_female1.ogg", "/Audio/Effects/Gasp/gasp_female2.ogg", "/Audio/Effects/Gasp/gasp_female3.ogg",
    };

    private static readonly string[] Whistles =
    {
        "/Audio/Voice/Human/whistle_1.ogg", "/Audio/Voice/Human/whistle_2.ogg", "/Audio/Voice/Human/whistle_3.ogg",
        "/Audio/Voice/Human/whistle_4.ogg",
    };

    private static readonly Color Phosphor = Color.FromHex("#E2FFE2");
    private static readonly Color Bright = Color.FromHex("#A8FFB8");
    private static readonly Color WallColour = Color.FromHex("#04140A");
    private static readonly Color PanelColour = Color.FromHex("#082010");
    private static readonly Color LeafColour = Color.FromHex("#0C2C15");
    private static readonly Color CeilingColour = Color.FromHex("#020D05");
    private static readonly Color HudColour = Color.FromHex("#010401");
    private static readonly Color FloorColour = Color.FromHex("#041309");
    private static readonly Color Sky = Color.FromHex("#010502");
    private static readonly Color Hills = Color.FromHex("#0A2A12");
    private static readonly Color ScreenGlow = Color.FromHex("#0E4A1E");
    private static readonly Color Amber = Color.FromHex("#D8A428");
    private static readonly Color Alarm = Color.FromHex("#FF3B30");
    private static readonly Color Acid = Color.FromHex("#C6F02A");
    private static readonly Color DarkAcid = Color.FromHex("#6E9410");
    private static readonly Color Blood = Color.FromHex("#A80E0E");
    private static readonly Color DarkBlood = Color.FromHex("#5E0606");
    private static readonly Color TracerCore = Color.FromHex("#FFF6CC");
    private static readonly Color TracerGlow = Color.FromHex("#FFB43C");
    private static readonly Color Dust = Color.FromHex("#7A9C7C");
    private static readonly Color Laser = Color.FromHex("#FF2A2A");
    private static readonly Color EyeColour = Color.FromHex("#2B1A10");

    private static readonly Color[] Skins =
    {
        Color.FromHex("#FFE1CC"), Color.FromHex("#EDC2A0"), Color.FromHex("#C08A62"), Color.FromHex("#8A5634"),
    };

    private static readonly Color[] HairColours =
    {
        Color.FromHex("#2A1A10"), Color.FromHex("#5A3A20"), Color.FromHex("#C89A50"), Color.FromHex("#1A1A1A"), Color.FromHex("#8A3A1A"),
    };

    #endregion

    private enum Phase : byte
    {
        Attract,
        Run,
        Shot,
        Kicked,
        Infected,
    }

    private enum HumanState : byte
    {
        Walk,
        Stand,
        Alert,
        Flee,
        Aim,
        Kick,
        Latched,
        Shot,
        Down,
    }

    /// <summary>A tracer in flight, in screen units, its head at <see cref="At"/>.</summary>
    private struct Bullet
    {
        public Vector2 At;
        public Vector2 Velocity;
    }

    /// <summary>A shot from a marine off screen: its laser sight shows on the lane before it fires.</summary>
    private struct QueuedShot
    {
        public float Delay;
        public float Lane;
    }

    /// <summary>Particles and decals sit in the world: their X is along the floor, so they scroll away with it.</summary>
    private struct Particle
    {
        public Vector2 At;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public float Gravity;

        /// <summary>How far down the floor this drop lands, or NaN to fly until it fades.</summary>
        public float Floor;

        public Color Colour;
        public byte Kind;
        public bool Stains;
        public bool Acid;
    }

    private struct Decal
    {
        public float X;
        public float Y;
        public float Size;
        public float Rotation;
        public float Age;
        public int State;
        public Color Colour;
        public bool Acid;
    }

    private struct Popup
    {
        public string? Text;
        public Vector2 At;
        public Color Colour;
        public float Age;
        public float Life;
    }

    private struct MuzzleFlash
    {
        public Vector2 At;
        public float Age;
    }

    /// <summary>A sound that follows a moment later, like the scream after the pounce. Scream 1 is male, 2 female.</summary>
    private struct PendingSound
    {
        public float Delay;
        public string? Path;
        public float Volume;
        public float Pitch;
        public int Scream;
    }

    private readonly record struct HumanLayer(Texture West, Texture East, Color Tint);

    /// <summary>A colonist or marine on the floor, dressed from body part and clothing sprites.</summary>
    private sealed class Human
    {
        public bool Active;
        public bool Marine;
        public bool Female;

        /// <summary>Facing east: running away from the hugger.</summary>
        public bool East;

        public bool Flees;
        public bool Infected;
        public bool Whistled;
        public bool Shouted;
        public HumanState State;

        /// <summary>Feet position along the floor, in world units.</summary>
        public float X;

        public float Walk;
        public float Step;
        public float Timer;
        public float Cooldown;
        public int Shots;

        /// <summary>Which way the body goes down: 1 back to the right, -1 to the left.</summary>
        public float Fall;

        public readonly List<HumanLayer> Layers = new();
    }

    private readonly System.Random _random = new();
    private readonly Font _font;
    private readonly Font _smallFont;
    private readonly Font _signFont;
    private readonly Font _bigFont;
    private readonly Dictionary<(string Rsi, string State), IRsiStateLike> _states = new();
    private SpriteSystem? _sprites;

    private readonly Bullet[] _bullets = new Bullet[MaxBullets];
    private int _bulletCount;
    private readonly QueuedShot[] _shots = new QueuedShot[MaxShots];
    private int _shotCount;
    private readonly Human[] _humans = new Human[MaxHumans];
    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _particleCount;
    private readonly Decal[] _decals = new Decal[MaxDecals];
    private int _decalCount;
    private int _decalNext;
    private readonly Popup[] _popups = new Popup[MaxPopups];
    private int _popupCount;
    private readonly MuzzleFlash[] _flashes = new MuzzleFlash[MaxFlashes];
    private int _flashCount;
    private readonly PendingSound[] _pending = new PendingSound[MaxPending];
    private int _pendingCount;

    private Phase _phase = Phase.Attract;

    /// <summary>Real seconds, for blinking text and the screen's roll bar.</summary>
    private float _clock;

    private float _time;
    private float _scroll;
    private float _run;
    private float _speed;
    private int _bonus;
    private int _best;
    private int _milestones;
    private float _over;
    private float _stopRate;
    private float _slow;
    private bool _paused;

    // The hugger: height of its feet over the floor and its climb, in units per second.
    private float _y;
    private float _vy;
    private bool _grounded = true;
    private bool _held;
    private bool _duck;
    private float _hold;
    private float _buffer;
    private float _squash;
    private float _anim;
    private float _dust;

    // Its body once it is dead: X along the floor, Y the height above it.
    private bool _corpse;
    private bool _corpseDown;
    private Vector2 _corpseAt;
    private Vector2 _corpseVelocity;
    private float _corpseSpin;
    private float _corpseRotation;
    private float _sizzle;

    private float _nextShot;
    private float _nextHuman;
    private float _ambient;

    private float _shake;
    private float _shakeTime;
    private Vector2 _offset;
    private Color _flash;
    private float _flashLife;
    private float _flashAge;
    private float _hudFlash;

    private readonly string _title;
    private readonly string _start;
    private readonly string _keys;
    private readonly string _again;
    private readonly string _pausedText;
    private readonly string _shotText;
    private readonly string _kickedText;
    private readonly string _infectedText;
    private readonly string _contact;
    private readonly string[] _signs = new string[6];
    private string _bestText = string.Empty;
    private int _bestShown = -1;
    private string _endText = string.Empty;
    private Color _endColour;

    // Set for each draw: the UI scale and the shake applied to the world (zero for the HUD).
    private float _s = 1;
    private Vector2 _o;

    public CMUHuggerRun(IGameTiming timing) : base(timing)
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        // a wide face: these sizes keep the longest lines inside the 320 unit screen
        _font = cache.GetFont(FontPath, 8);
        _smallFont = cache.GetFont(FontPath, 7);
        _signFont = cache.GetFont(FontPath, 6);
        _bigFont = cache.GetFont(FontPath, 16);
        for (var i = 0; i < MaxHumans; i++)
        {
            _humans[i] = new Human();
        }

        _title = Loc.GetString("cmu-game-hugger-title");
        _start = Loc.GetString("cmu-game-hugger-start");
        _keys = Loc.GetString("cmu-game-hugger-keys");
        _again = Loc.GetString("cmu-game-hugger-again");
        _pausedText = Loc.GetString("cmu-game-hugger-paused");
        _shotText = Loc.GetString("cmu-game-hugger-shot");
        _kickedText = Loc.GetString("cmu-game-hugger-kicked");
        _infectedText = Loc.GetString("cmu-game-hugger-infected");
        _contact = Loc.GetString("cmu-game-hugger-contact");
        for (var i = 0; i < _signs.Length; i++)
        {
            _signs[i] = Loc.GetString($"cmu-game-hugger-sign-{i}");
        }
    }

    public override string Help => Loc.GetString("cmu-game-hugger-help");

    public override void Start()
    {
        // a jump mashed as the hugger dies must not throw it straight into a new run
        if (_phase != Phase.Attract && _over < RestartDelay)
            return;

        base.Start();
        _phase = Phase.Run;
        _time = 0;
        _run = 0;
        _speed = StartSpeed;
        _bonus = 0;
        _milestones = 0;
        _over = 0;
        _slow = 0;
        _y = 0;
        _vy = 0;
        _grounded = true;
        _held = false;
        _hold = 0;
        _buffer = 0;
        _squash = 0;
        _corpse = false;
        _sizzle = 0;
        _bulletCount = 0;
        _shotCount = 0;
        _particleCount = 0;
        _decalCount = 0;
        _decalNext = 0;
        _popupCount = 0;
        _flashCount = 0;
        _pendingCount = 0;
        _shakeTime = 0;
        _flashLife = 0;
        foreach (var human in _humans)
        {
            human.Active = false;
        }

        _nextShot = FirstShot;
        _nextHuman = FirstHuman;
        _ambient = 5;
        Play(NewGame, -6);
    }

    protected override void OnKey(CMUArcadeInput input)
    {
        if (input.Repeat)
            return;

        switch (input.Key)
        {
            case CMUArcadeKey.Up:
            case CMUArcadeKey.Action:
                // W, Up and 8 start a run as well as Space
                if (input.Down && !Running)
                {
                    Start();
                    return;
                }

                _held = input.Down;
                if (input.Down)
                    _buffer = JumpBuffer;
                break;
            case CMUArcadeKey.Down:
                _duck = input.Down;
                break;
        }
    }

    protected override void OnFocusLost()
    {
        _held = false;
        _duck = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var real = MathF.Min(args.DeltaSeconds, 0.05f);
        _clock += real;

        // a run without the keys waits for them instead of running into the next bullet
        _paused = _phase == Phase.Run && !HasKeyboardFocus();
        if (_paused)
            return;

        if (_slow > 0)
            _slow -= real;

        if (_phase is not (Phase.Attract or Phase.Run))
            _over += real;

        var delta = _slow > 0 ? real * SlowMotion : real;
        for (var left = delta; left > 0; left -= 1 / 120f)
        {
            Simulate(MathF.Min(left, 1 / 120f));
        }

        UpdateEffects(delta);
        UpdateSounds(real);
    }

    #region Simulation

    private void Simulate(float dt)
    {
        switch (_phase)
        {
            case Phase.Attract:
                _speed = 28;
                break;
            case Phase.Run:
                _time += dt;
                _speed = MathF.Min(MaxSpeed, _speed + Acceleration * dt);
                break;
            default:
                _speed = MathF.Max(0, _speed - _stopRate * dt);
                break;
        }

        var moved = _speed * dt;
        _scroll += moved;
        var scurry = _phase == Phase.Run ? MathF.Max(1, _speed / StartSpeed) * (_duck && _grounded ? 1.6f : 1) : 1;
        _anim += dt * scurry;
        if (_phase == Phase.Run)
        {
            _run += moved;
            MoveHugger(dt);
            Spawn(dt);
            FireQueued(dt);
        }

        MoveBullets(dt);
        MoveHumans(dt);
        MoveCorpse(dt);
        if (_phase != Phase.Run)
            return;

        Collide();
        if (_phase == Phase.Run)
            Tally();
    }

    private void MoveHugger(float dt)
    {
        if (_buffer > 0)
            _buffer -= dt;

        if (_grounded)
        {
            if (_buffer > 0 && !_duck)
                Jump();
        }
        else
        {
            // holding the jump floats it higher for a moment; ducking in the air drops it like a stone
            var gravity = Gravity;
            if (_duck)
            {
                gravity = FastFallGravity;
            }
            else if (_held && _vy > 0 && _hold < MaxHold)
            {
                gravity = HoldGravity;
                _hold += dt;
            }

            _vy -= gravity * dt;
            _y += _vy * dt;
            if (_y <= 0)
                Land();
        }

        _squash = MoveTowards(_squash, 0, dt * 7);
        if (!_grounded)
            return;

        _dust -= dt;
        if (_dust > 0)
            return;

        _dust = 0.06f + Fx() * 0.06f;
        Emit(World(HuggerX - 8, FloorY - 1), 1, Dust.WithAlpha(0.45f), 30, 0.35f, 1.4f, MathF.PI + 0.4f, 0.8f, -20, kind: Smoke);
    }

    private void Jump()
    {
        _grounded = false;
        _vy = JumpVelocity;
        _hold = 0;
        _buffer = 0;
        _squash = 1;
        PlayAny(Swooshes, -6, 1.1f);
        Emit(World(HuggerX - 4, FloorY - 1), 5, Dust.WithAlpha(0.6f), 60, 0.4f, 2, MathF.PI, 1.2f, -10, kind: Smoke);
    }

    private void Land()
    {
        _y = 0;
        _vy = 0;
        _grounded = true;
        _squash = -1;
        PlayAny(Steps, -12, 1.6f);
        Emit(World(HuggerX, FloorY - 1), 6, Dust.WithAlpha(0.6f), 50, 0.45f, 2, -MathF.PI / 2, MathF.PI, -10, kind: Smoke);
        if (_buffer > 0 && !_duck)
            Jump();
    }

    /// <summary>Distance makes the score, plus any infection bonus. A beep every hundred, like the old dino.</summary>
    private void Tally()
    {
        var points = (int) (_run / UnitsPerPoint);
        Score = Math.Min(CMUArcadeLimits.MaxScore, points + _bonus);
        if (points / Milestone <= _milestones)
            return;

        _milestones = points / Milestone;
        _hudFlash = 0.6f;
        Play(Beep, -8, 1.2f);
    }

    /// <summary>
    /// Bullets come in patterns that get closer and nastier with time; humans walk in every so often, and while one
    /// is ahead of the hugger the marines off screen hold their fire rather than hit a colonist.
    /// </summary>
    private void Spawn(float dt)
    {
        _nextShot -= dt;
        _nextHuman -= dt;
        _ambient -= dt;
        if (_ambient <= 0)
        {
            Ambience();
            _ambient = 7 + Fx() * 8;
        }

        var blocked = HumanAhead();
        if (_nextHuman <= 0 && !blocked && _shotCount == 0 && !BulletsAhead())
        {
            SpawnHuman(_time >= MarinesFrom && _random.NextDouble() < 0.4);
            _nextHuman = HumanEveryMin + Fx() * (HumanEveryMax - HumanEveryMin);
            _nextShot = MathF.Max(_nextShot, 1.2f);
            return;
        }

        if (blocked)
        {
            _nextShot = MathF.Max(_nextShot, 0.8f);
            return;
        }

        // let the lane clear for the human that is due
        if (_nextShot > 0 || _nextHuman <= 0)
            return;

        var hard = Math.Clamp(_time / 90f, 0, 1);
        var roll = _random.NextDouble();
        if (_time > 15 && roll < 0.25 + hard * 0.2)
        {
            Queue(PickLane(), 3);
        }
        else if (_time > 40 && roll < 0.55)
        {
            Queue(LaneLow, 1);
            Queue(LaneMid, 1, 0.3f);
        }
        else
        {
            Queue(PickLane(), 1);
        }

        _nextShot = (1.55f - 0.7f * hard) * (0.8f + Fx() * 0.6f);
    }

    private float PickLane()
    {
        if (_time < 10)
            return LaneLow;

        var roll = _random.NextDouble();
        if (roll < 0.5)
            return LaneLow;

        return roll < 0.8 || _time < 20 ? LaneMid : LaneHigh;
    }

    private void Queue(float lane, int count, float delay = 0)
    {
        for (var i = 0; i < count && _shotCount < MaxShots; i++)
        {
            _shots[_shotCount++] = new QueuedShot { Delay = Warning + delay + i * BurstGap, Lane = lane };
        }
    }

    private bool HumanAhead()
    {
        foreach (var human in _humans)
        {
            if (human.Active && human.X - _scroll > HuggerX - 24 && human.State is not (HumanState.Down or HumanState.Shot))
                return true;
        }

        return false;
    }

    private bool BulletsAhead()
    {
        for (var i = 0; i < _bulletCount; i++)
        {
            if (_bullets[i].At.X > HuggerX)
                return true;
        }

        return false;
    }

    /// <summary>Distant fire and roars from elsewhere in the colony, so the corridor never feels empty.</summary>
    private void Ambience()
    {
        if (Fx() < 0.3f)
        {
            Play(DistantRoar, -14, 0.9f + Fx() * 0.2f);
            return;
        }

        var shots = 2 + _random.Next(4);
        for (var i = 0; i < shots; i++)
        {
            Pend(i * 0.09f, RifleShot, -18, 0.78f);
        }
    }

    private void FireQueued(float dt)
    {
        for (var i = _shotCount - 1; i >= 0; i--)
        {
            _shots[i].Delay -= dt;
            if (_shots[i].Delay > 0)
                continue;

            var lane = _shots[i].Lane;
            _shots[i] = _shots[--_shotCount];
            AddBullet(new Vector2(ScreenWidth + 4, lane), new Vector2(-(BulletSpeed + _speed * BulletSpeedPerRun), 0));
            AddFlash(new Vector2(ScreenWidth - 1, lane));
            Play(RifleShot, -4, 0.95f + Fx() * 0.1f);
        }
    }

    private void AddBullet(Vector2 at, Vector2 velocity)
    {
        if (_bulletCount < MaxBullets)
            _bullets[_bulletCount++] = new Bullet { At = at, Velocity = velocity };
    }

    private void MoveBullets(float dt)
    {
        for (var i = _bulletCount - 1; i >= 0; i--)
        {
            ref var bullet = ref _bullets[i];
            bullet.At += bullet.Velocity * dt;
            if (bullet.At.Y >= FloorY + 2)
            {
                // an aimed shot that missed sparks off the deck plating
                var world = World(bullet.At.X, FloorY + 2);
                Emit(world, 7, Amber, 160, 0.3f, 1, -MathF.PI / 2 + MathF.Sign(bullet.Velocity.X) * 0.6f, 1.2f, 500, kind: Spark);
                Emit(world, 2, Dust.WithAlpha(0.5f), 20, 0.5f, 2, -MathF.PI / 2, 1, -15, kind: Smoke);
                Play(Ricochet, -10, 1.1f + Fx() * 0.2f);
            }
            else if (bullet.At.X > -Tracer - 10 && bullet.At.Y > HudHeight)
            {
                continue;
            }

            _bullets[i] = _bullets[--_bulletCount];
        }
    }

    private void SpawnHuman(bool marine)
    {
        Human? human = null;
        foreach (var free in _humans)
        {
            if (free.Active)
                continue;

            human = free;
            break;
        }

        if (human == null)
            return;

        human.Active = true;
        human.Marine = marine;
        human.Female = Fx() < (marine ? 0.2f : 0.45f);
        human.East = false;
        human.Flees = false;
        human.Infected = false;
        human.Whistled = marine;
        human.Shouted = false;
        human.Timer = 0;
        human.Step = Fx() * 3;
        human.Fall = 1;
        human.Cooldown = 0;
        human.X = _scroll + ScreenWidth + 40;
        if (marine || Fx() < 0.3f)
        {
            human.State = HumanState.Stand;
            human.Walk = 0;
        }
        else
        {
            human.State = HumanState.Walk;
            human.Walk = 10 + Fx() * 24;
        }

        Dress(human);

        // the hugger smells prey before it sees it
        PlayAny(Hisses, -12, 1.45f);
    }

    /// <summary>Puts a human together the way the game draws one: body parts, eyes, clothes and hair in layer order.</summary>
    private void Dress(Human human)
    {
        human.Layers.Clear();
        var skin = Skins[_random.Next(Skins.Length)];
        var sex = human.Female ? "_f" : "_m";
        Layer(human, Parts, "torso" + sex, skin);
        Layer(human, Parts, "head" + sex, skin);
        Layer(human, Eyes, "eyes", EyeColour);
        Layer(human, Parts, "r_arm", skin);
        Layer(human, Parts, "l_arm", skin);
        Layer(human, Parts, "r_leg", skin);
        Layer(human, Parts, "l_leg", skin);
        Layer(human, Clothing + (human.Marine ? MarineUniform : Jumpsuits[_random.Next(Jumpsuits.Length)]), "equipped-INNERCLOTHING", Color.White);
        Layer(human, Parts, "l_foot", skin);
        Layer(human, Parts, "r_foot", skin);
        Layer(human, Parts, "l_hand", skin);
        Layer(human, Parts, "r_hand", skin);
        Layer(human, Clothing + (human.Marine ? MarineBoots : Shoes[_random.Next(Shoes.Length)]), "equipped-FEET", Color.White);
        if (human.Marine)
        {
            Layer(human, Clothing + MarineArmor, "equipped-OUTERCLOTHING", Color.White);
            Layer(human, Clothing + MarineHelmet, "equipped-HELMET", Color.White);
            Layer(human, Rifle, "wielded-inhand-left", Color.White);
            return;
        }

        var styles = human.Female ? FemaleHair : MaleHair;
        Layer(human, Hair, styles[_random.Next(styles.Length)], HairColours[_random.Next(HairColours.Length)]);
    }

    private void Layer(Human human, string rsi, string state, Color tint)
    {
        human.Layers.Add(new HumanLayer(Frame(rsi, state, RsiDirection.West), Frame(rsi, state, RsiDirection.East), tint));
    }

    private void MoveHumans(float dt)
    {
        foreach (var human in _humans)
        {
            if (!human.Active)
                continue;

            human.Timer += dt;
            var x = human.X - _scroll;
            switch (human.State)
            {
                case HumanState.Walk:
                    human.X -= human.Walk * dt;
                    human.Step += dt * (4 + human.Walk * 0.18f);
                    if (!human.Whistled && x < ScreenWidth - 10)
                    {
                        human.Whistled = true;
                        if (Fx() < 0.35f)
                            PlayAny(Whistles, -12);
                    }

                    Notice(human, x);
                    break;
                case HumanState.Stand:
                    if (human.Marine)
                        Watch(human, x, dt);
                    else
                        Notice(human, x);
                    break;
                case HumanState.Alert:
                    if (human.Flees && human.Timer > 0.3f)
                    {
                        human.State = HumanState.Flee;
                        human.East = true;
                        human.Timer = 0;
                    }

                    break;
                case HumanState.Flee:
                    human.X += 48 * dt;
                    human.Step += dt * 14;
                    break;
                case HumanState.Aim:
                    Backpedal(human, dt);
                    Aim(human, x);
                    break;
                case HumanState.Kick:
                    if (human.Timer >= KickTime * 1.6f)
                    {
                        human.State = HumanState.Stand;
                        human.Timer = 0;
                    }

                    break;
                case HumanState.Latched:
                    if (human.Timer >= Struggle + FallTime)
                        Fallen(human);
                    break;
                case HumanState.Shot:
                    if (human.Timer >= FallTime * 0.8f)
                        Fallen(human);
                    break;
            }

            if (x < -60 || human.State == HumanState.Flee && x > ScreenWidth + 60)
                human.Active = false;
        }
    }

    /// <summary>A colonist who sees the hugger coming freezes with a gasp, and some turn and run.</summary>
    private void Notice(Human human, float x)
    {
        if (_phase != Phase.Run || x < HuggerX || x - HuggerX > AlertRange)
            return;

        human.State = HumanState.Alert;
        human.Timer = 0;
        human.Flees = !human.Marine && Fx() < 0.45f;
        Pop("!", new Vector2(x, FloorY - 72), Amber, 0.7f);
        PlayAny(human.Female ? FemaleGasps : MaleGasps, -6);
    }

    /// <summary>A marine on screen takes aim at the hugger every second or so, until it gets too close to shoot.</summary>
    private void Watch(Human human, float x, float dt)
    {
        if (_phase != Phase.Run)
            return;

        if (x < HuggerX + MarineReach)
        {
            Notice(human, x);
            return;
        }

        Backpedal(human, dt);
        human.Cooldown -= dt;
        if (human.Cooldown > 0 || x > ScreenWidth + 8)
            return;

        human.State = HumanState.Aim;
        human.Timer = 0;
        human.Shots = 3;
        if (human.Shouted)
            return;

        human.Shouted = true;
        Pop(_contact, new Vector2(x, FloorY - 74), Amber, 0.9f);
    }

    private void Aim(Human human, float x)
    {
        if (_phase != Phase.Run || x < HuggerX + MarineReach)
        {
            human.State = HumanState.Stand;
            human.Timer = 0;
            return;
        }

        // the laser sight settles for a moment, then a three round burst at wherever the hugger is
        if (human.Timer < MarineAim + (3 - human.Shots) * 0.09f)
            return;

        var muzzle = Barrel(x);
        var dir = Vector2.Normalize(Target() - muzzle);
        AddBullet(muzzle, dir * MarineBulletSpeed);
        AddFlash(muzzle);
        Play(RifleShot, 0, 0.95f + Fx() * 0.1f);
        Emit(World(muzzle.X + 22, muzzle.Y + 2), 1, Amber, 90, 1.2f, 1.6f, -MathF.PI / 2 + 0.5f, 0.6f, 600);
        human.Shots--;
        if (human.Shots > 0)
            return;

        human.State = HumanState.Stand;
        human.Timer = 0;
        human.Cooldown = 0.9f + Fx() * 0.7f;
    }

    private static void Backpedal(Human human, float dt)
    {
        human.X += MarineBackpedal * dt;
        human.Step += dt * 6;
    }

    /// <summary>The M41A's muzzle on a marine facing west, from the sprite.</summary>
    private static Vector2 Barrel(float x)
    {
        return new Vector2(x - 26, FloorY - 31);
    }

    private Vector2 Target()
    {
        return new Vector2(HuggerX, FloorY - _y - (_duck && _grounded ? 5 : 11));
    }

    private void Fallen(Human human)
    {
        human.State = HumanState.Down;
        human.Timer = 0;
        var x = human.X - _scroll;
        Play(BodyFall, -1, 0.95f + Fx() * 0.1f);
        Emit(World(x + human.Fall * 30, FloorY), 10, Dust.WithAlpha(0.5f), 60, 0.6f, 3, -MathF.PI / 2, MathF.PI, -10, kind: Smoke);
        Shake(2, 0.25f);
        if (!human.Infected)
            AddDecal(human.X + human.Fall * 22, FloorY + 5, 34, DarkBlood.WithAlpha(0.9f), false);
    }

    private void MoveCorpse(float dt)
    {
        if (!_corpse)
            return;

        if (!_corpseDown)
        {
            _corpseVelocity.Y -= Gravity * dt;
            _corpseAt += _corpseVelocity * dt;
            _corpseRotation += _corpseSpin * dt;
            if (_corpseAt.Y > 0)
                return;

            _corpseAt.Y = 0;
            _corpseDown = true;
            _corpseRotation = 0;
            Splash();
            return;
        }

        if (_sizzle <= 0)
            return;

        // the acid eats into the deck for a while
        _sizzle -= dt;
        if (Fx() < dt * 20)
            Emit(new Vector2(_corpseAt.X + (Fx() - 0.5f) * 24, FloorY + 3), 1, Acid.WithAlpha(0.35f), 14, 1.1f, 2.5f, -MathF.PI / 2, 0.6f, -25, kind: Smoke);
    }

    /// <summary>The dead hugger hits the floor in a pool of its own acid.</summary>
    private void Splash()
    {
        var at = new Vector2(_corpseAt.X, FloorY);
        AddDecal(at.X, FloorY + 5, 34, Acid.WithAlpha(0.9f), true);
        AddDecal(at.X - 14, FloorY + 9, 16, Acid.WithAlpha(0.85f), true);
        AddDecal(at.X + 12, FloorY + 2, 12, Acid.WithAlpha(0.85f), true);
        Emit(at, 18, Acid, 140, 0.9f, 1.8f, -MathF.PI / 2, 2.4f, 600, stains: true, acid: true);
        Emit(at, 6, Acid.WithAlpha(0.4f), 20, 1.2f, 3, -MathF.PI / 2, 1, -20, kind: Smoke);
        Play(Squish, -2);
        PlayAny(Sizzles, -3);
        Pend(0.6f, Lost, -6);
        Shake(2.5f, 0.25f);
        _sizzle = 2.4f;
    }

    #endregion

    #region Collisions and endings

    private void Collide()
    {
        var bottom = FloorY - _y;
        var top = bottom - (_duck && _grounded ? DuckHeight : HuggerHeight);
        const float left = HuggerX - HuggerHalfWidth;
        const float right = HuggerX + HuggerHalfWidth;
        for (var i = _bulletCount - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            var tail = bullet.At - Vector2.Normalize(bullet.Velocity) * Tracer;
            if (Inside(bullet.At, left, right, top, bottom) || Inside((bullet.At + tail) / 2, left, right, top, bottom)
                || Inside(tail, left, right, top, bottom))
            {
                _bullets[i] = _bullets[--_bulletCount];
                ShotDown(bullet);
                return;
            }

            // a round that got past the hugger can still find somebody behind it
            if (bullet.At.X >= HuggerX - 14)
                continue;

            foreach (var human in _humans)
            {
                if (!Hittable(human))
                    continue;

                var x = human.X - _scroll;
                if (!Inside(bullet.At, x - BodyHalfWidth, x + BodyHalfWidth, FloorY - HeadTop, FloorY))
                    continue;

                _bullets[i] = _bullets[--_bulletCount];
                Wounded(human, bullet);
                break;
            }
        }

        foreach (var human in _humans)
        {
            if (!Hittable(human))
                continue;

            var x = human.X - _scroll;
            if (_y >= LatchHeight && Overlap(left, right, top, bottom, x - HeadHalfWidth, x + HeadHalfWidth, FloorY - HeadTop, FloorY - HeadBottom))
            {
                Infect(human);
                return;
            }

            if (Overlap(left, right, top, bottom, x - BodyHalfWidth, x + BodyHalfWidth, FloorY - BodyTop, FloorY))
            {
                Kicked(human);
                return;
            }
        }
    }

    private static bool Hittable(Human human)
    {
        return human.Active && human.State is not (HumanState.Latched or HumanState.Shot or HumanState.Down);
    }

    private static bool Inside(Vector2 point, float left, float right, float top, float bottom)
    {
        // tracers are thin: a hair of slack so a grazing round still counts
        return point.X >= left - 1.5f && point.X <= right + 1.5f && point.Y >= top - 1.5f && point.Y <= bottom + 1.5f;
    }

    private static bool Overlap(float left, float right, float top, float bottom, float otherLeft, float otherRight, float otherTop, float otherBottom)
    {
        return left < otherRight && right > otherLeft && top < otherBottom && bottom > otherTop;
    }

    private void EndRun(Phase phase, string text, Color colour)
    {
        _phase = phase;
        _over = 0;
        _stopRate = MathF.Max(1, _speed / StopTime);
        _shotCount = 0;
        _held = false;
        _duck = false;
        _endText = text;
        _endColour = colour;
        GameOver();
        _best = Math.Max(_best, Score);
    }

    /// <summary>A tracer finds the hugger: it bursts in acid that splashes along the floor.</summary>
    private void ShotDown(Bullet bullet)
    {
        var dir = Vector2.Normalize(bullet.Velocity);
        var angle = MathF.Atan2(dir.Y, dir.X);
        var hit = World(bullet.At.X, bullet.At.Y);
        _corpse = true;
        _corpseDown = false;
        _corpseAt = new Vector2(HuggerX + _scroll, _y + 8);
        _corpseVelocity = new Vector2(dir.X * 70, 170 + _y * 0.5f);
        _corpseSpin = -9;
        Emit(hit, 40, Acid, 230, 1.1f, 2.2f, angle, 1.9f, 620, stains: true, acid: true, inherit: _speed * 0.2f);
        Emit(hit, 14, DarkAcid, 140, 0.9f, 3, angle, 1.2f, 620, stains: true, acid: true, inherit: _speed * 0.2f);
        Emit(hit, 12, Acid, 280, 0.25f, 1, angle + MathF.PI, 1.4f, 0, kind: Spark);
        Emit(hit, 6, Acid.WithAlpha(0.4f), 30, 0.9f, 3, -MathF.PI / 2, 1.5f, -20, kind: Smoke);
        PlayAny(Flesh);
        Play(ParasiteDies);
        Flash(Acid.WithAlpha(0.35f), 0.3f);
        Shake(4.5f, 0.4f);
        _slow = 0.45f;
        EndRun(Phase.Shot, _shotText, Acid);
    }

    /// <summary>Ran into a pair of legs: the human punts the hugger away and it splats down the corridor.</summary>
    private void Kicked(Human human)
    {
        human.State = HumanState.Kick;
        human.Timer = 0;
        var hit = World(HuggerX + 8, FloorY - _y - 10);
        _corpse = true;
        _corpseDown = false;
        _corpseAt = new Vector2(HuggerX + _scroll + 4, _y + 10);
        _corpseVelocity = new Vector2(-40, 420);
        _corpseSpin = -14;
        Emit(hit, 16, Acid, 160, 0.9f, 2, MathF.PI + 0.5f, 1.6f, 620, stains: true, acid: true);
        PlayAny(Punches);
        Play(ParasiteDies, 0, 1.05f);
        Flash(Color.White.WithAlpha(0.2f), 0.12f);
        Shake(3.5f, 0.3f);
        _slow = 0.3f;
        EndRun(Phase.Kicked, _kickedText, Green);
    }

    /// <summary>Came down on a face: the hugger latches on, the host screams and goes over backwards. A win.</summary>
    private void Infect(Human human)
    {
        var bonus = human.Marine ? MarineBonus : ColonistBonus;
        human.State = HumanState.Latched;
        human.Timer = 0;
        human.Infected = true;
        human.Fall = human.East ? -1 : 1;
        _bonus += bonus;
        Tally();
        EndRun(Phase.Infected, _infectedText, Acid);
        SetStatus(Loc.GetString("cmu-game-hugger-win", ("score", Score)));

        var face = new Vector2(human.X - _scroll, FloorY - 50);
        Emit(World(face.X, face.Y), 14, Blood, 120, 0.9f, 1.8f, -MathF.PI / 2, 2.2f, 520, stains: true);
        Emit(World(face.X, face.Y), 5, DarkBlood, 70, 0.9f, 2.6f, -MathF.PI / 2, 1.6f, 520, stains: true);
        Pop(Loc.GetString("cmu-game-hugger-bonus", ("bonus", bonus)), face - new Vector2(0, 22), Acid, 1.4f);
        Play(Pounce);
        PlayAny(Swooshes, -3, 0.9f);
        PendScream(0.12f, human.Female);
        Pend(1.1f, Won, -4);
        Flash(Color.White.WithAlpha(0.3f), 0.15f);
        Shake(3, 0.3f);
        _slow = 0.3f;
    }

    /// <summary>A round meant for the hugger hits whoever it passed: they scream and drop.</summary>
    private void Wounded(Human human, Bullet bullet)
    {
        human.State = HumanState.Shot;
        human.Timer = 0;
        human.Fall = -1;
        var dir = Vector2.Normalize(bullet.Velocity);
        var angle = MathF.Atan2(dir.Y, dir.X);
        var hit = World(bullet.At.X, bullet.At.Y);
        Emit(hit, 22, Blood, 170, 1.2f, 2, angle, 1.6f, 520, stains: true);
        Emit(hit, 6, DarkBlood, 90, 1, 3, angle, 1.2f, 520, stains: true);
        PlayAny(Flesh);
        CMUGameAudio.Scream(this, human.Female, -2);
    }

    #endregion

    #region Effects

    private Vector2 World(float x, float y)
    {
        return new Vector2(x + _scroll, y);
    }

    private float Fx()
    {
        return (float) _random.NextDouble();
    }

    private static float MoveTowards(float value, float target, float step)
    {
        return value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);
    }

    /// <summary>
    /// Throws particles from a world spot. <paramref name="direction"/> and <paramref name="spread"/> are radians,
    /// 0 pointing right and positive turning down the screen. Falling drops land somewhere down the floor's depth.
    /// </summary>
    private void Emit(Vector2 at, int count, Color colour, float speed, float life, float size, float direction, float spread,
        float gravity, bool stains = false, byte kind = Drop, bool acid = false, float inherit = 0)
    {
        for (var i = 0; i < count && _particleCount < MaxParticles; i++)
        {
            var angle = direction + (Fx() - 0.5f) * spread;
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * (0.35f + Fx() * 0.65f);
            velocity.X += inherit;
            _particles[_particleCount++] = new Particle
            {
                At = at,
                Velocity = velocity,
                Life = life * (0.6f + Fx() * 0.6f),
                Size = size * (0.6f + Fx() * 0.8f),
                Gravity = gravity,
                Floor = gravity > 0 ? FloorY + 1 + Fx() * 13 : float.NaN,
                Colour = colour,
                Kind = kind,
                Stains = stains,
                Acid = acid,
            };
        }
    }

    private void AddDecal(float x, float y, float size, Color colour, bool acid)
    {
        _decals[_decalNext] = new Decal
        {
            X = x,
            Y = y,
            Size = size,
            Rotation = Fx() * MathF.Tau,
            State = _random.Next(6),
            Colour = colour,
            Acid = acid,
        };
        _decalNext = (_decalNext + 1) % MaxDecals;
        _decalCount = Math.Min(_decalCount + 1, MaxDecals);
    }

    private void AddFlash(Vector2 at)
    {
        if (_flashCount < MaxFlashes)
            _flashes[_flashCount++] = new MuzzleFlash { At = at };
    }

    /// <summary>Text that floats up from a screen spot and fades, riding along with the floor.</summary>
    private void Pop(string text, Vector2 at, Color colour, float life)
    {
        if (_popupCount < MaxPopups)
            _popups[_popupCount++] = new Popup { Text = text, At = World(at.X, at.Y), Colour = colour, Life = life };
    }

    private void Shake(float strength, float time)
    {
        // a light knock does not cut short a big one still shaking
        if (_shakeTime > 0 && strength < _shake)
            return;

        _shake = strength;
        _shakeTime = time;
    }

    private void Flash(Color colour, float time)
    {
        _flash = colour;
        _flashLife = time;
        _flashAge = 0;
    }

    private void UpdateEffects(float dt)
    {
        for (var i = _particleCount - 1; i >= 0; i--)
        {
            ref var p = ref _particles[i];
            p.Age += dt;
            p.Velocity.Y += p.Gravity * dt;
            if (p.Kind == Smoke)
            {
                p.Velocity *= MathF.Max(0, 1 - 2.5f * dt);
                p.Size += dt * 6;
            }

            p.At += p.Velocity * dt;
            var landed = !float.IsNaN(p.Floor) && p.At.Y >= p.Floor && p.Velocity.Y > 0;
            if (landed && p.Stains)
                AddDecal(p.At.X, p.Floor, p.Size * 2.4f, p.Colour, p.Acid);

            if (landed || p.Age >= p.Life)
                _particles[i] = _particles[--_particleCount];
        }

        for (var i = 0; i < _decalCount; i++)
        {
            _decals[i].Age += dt;
        }

        for (var i = _popupCount - 1; i >= 0; i--)
        {
            _popups[i].Age += dt;
            if (_popups[i].Age >= _popups[i].Life)
                _popups[i] = _popups[--_popupCount];
        }

        for (var i = _flashCount - 1; i >= 0; i--)
        {
            _flashes[i].Age += dt;
            if (_flashes[i].Age >= 0.07f)
                _flashes[i] = _flashes[--_flashCount];
        }

        if (_shakeTime > 0)
        {
            _shakeTime -= dt;
            var strength = _shakeTime > 0 ? _shake * MathF.Min(1, _shakeTime * 4) : 0;
            _offset = new Vector2((Fx() - 0.5f) * 2 * strength, (Fx() - 0.5f) * 2 * strength);
        }
        else
        {
            _offset = Vector2.Zero;
        }

        if (_flashLife > 0)
            _flashAge += dt;

        if (_hudFlash > 0)
            _hudFlash -= dt;
    }

    private void Play(string path, float volume = 0, float pitch = 1)
    {
        Sound(path, volume, pitch);
    }

    private void PlayAny(string[] paths, float volume = 0, float pitch = 1)
    {
        Sound(paths[_random.Next(paths.Length)], volume, pitch * (0.94f + Fx() * 0.12f));
    }

    private void Pend(float delay, string path, float volume = 0, float pitch = 1)
    {
        if (_pendingCount < MaxPending)
            _pending[_pendingCount++] = new PendingSound { Delay = delay, Path = path, Volume = volume, Pitch = pitch };
    }

    private void PendScream(float delay, bool female)
    {
        if (_pendingCount < MaxPending)
            _pending[_pendingCount++] = new PendingSound { Delay = delay, Scream = female ? 2 : 1 };
    }

    private void UpdateSounds(float dt)
    {
        for (var i = _pendingCount - 1; i >= 0; i--)
        {
            _pending[i].Delay -= dt;
            if (_pending[i].Delay > 0)
                continue;

            var sound = _pending[i];
            _pending[i] = _pending[--_pendingCount];
            if (sound.Scream > 0)
                CMUGameAudio.Scream(this, sound.Scream == 2, sound.Volume);
            else if (sound.Path != null)
                Play(sound.Path, sound.Volume, sound.Pitch);
        }
    }

    #endregion

    #region Art

    private IRsiStateLike State(string rsi, string state)
    {
        if (_states.TryGetValue((rsi, state), out var found))
            return found;

        _sprites ??= IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        found = _sprites.RsiStateLike(new SpriteSpecifier.Rsi(new ResPath(rsi), state));
        _states[(rsi, state)] = found;
        return found;
    }

    /// <summary>A frame of a sprite state facing a direction; frames wrap, and one-direction states ignore it.</summary>
    private Texture Frame(string rsi, string state, RsiDirection direction, int frame = 0)
    {
        var art = State(rsi, state);
        if (art.RsiDirections == RsiDirectionType.Dir1)
            direction = RsiDirection.South;

        return art.GetFrame(direction, art.IsAnimated ? frame % art.AnimationFrameCount : 0);
    }

    private static uint Hash(int n)
    {
        var h = (uint) n * 0x9E3779B1u;
        h ^= h >> 15;
        h *= 0x85EBCA77u;
        h ^= h >> 13;
        h *= 0xC2B2AE3Du;
        h ^= h >> 16;
        return h;
    }

    /// <summary>A steady pseudo-random 0..1 for a spot in the world, so scenery does not flicker as it scrolls.</summary>
    private static float Hash01(int n, int salt)
    {
        return (Hash(n * 31 + salt) & 0xFFFF) / 65535f;
    }

    #endregion

    #region Drawing

    protected override void DrawGame(DrawingHandleScreen handle)
    {
        _s = UIScale;
        _o = _offset;
        DrawCeiling(handle);
        DrawWall(handle);
        DrawFloor(handle);
        DrawLights(handle);
        DrawDecals(handle);
        DrawShadows(handle);
        foreach (var human in _humans)
        {
            if (human.Active)
                DrawHuman(handle, human);
        }

        DrawCorpse(handle);
        DrawHugger(handle);
        DrawFire(handle);
        DrawParticles(handle);
        DrawStreaks(handle);
        DrawPopups(handle);
        if (_flashLife > 0 && _flashAge < _flashLife)
            Fill(handle, 0, HudHeight, ScreenWidth, ScreenHeight - HudHeight, _flash.WithAlpha(_flash.A * (1 - _flashAge / _flashLife)));

        _o = Vector2.Zero;
        DrawHud(handle);
        DrawCrt(handle);
    }

    private void DrawCeiling(DrawingHandleScreen handle)
    {
        Fill(handle, 0, HudHeight, ScreenWidth, CeilingY - HudHeight, CeilingColour);
        Fill(handle, 0, 20, ScreenWidth, 2.5f, Dim.WithAlpha(0.55f));
        Fill(handle, 0, 20, ScreenWidth, 0.8f, Green.WithAlpha(0.3f));
        Fill(handle, 0, 26, ScreenWidth, 3, Dim.WithAlpha(0.35f));
        var u = _scroll * WallDepth;
        for (var c = (int) MathF.Floor(u / 48); c * 48 - u < ScreenWidth; c++)
        {
            Fill(handle, c * 48 - u, 18, 3, 13, Dim.WithAlpha(0.6f));
        }

        Fill(handle, 0, CeilingY - 4, ScreenWidth, 4, PanelColour);
        Fill(handle, 0, CeilingY - 0.5f, ScreenWidth, 1, Dim.WithAlpha(0.8f));
        for (var c = (int) MathF.Floor((u - 30) / 96); c * 96 + 30 - u < ScreenWidth; c++)
        {
            var x = c * 96 + 30 - u;
            var on = LampOn(c);
            Fill(handle, x, CeilingY - 7, 26, 4, on ? Bright : Dim.WithAlpha(0.5f));
            if (on)
                Fill(handle, x - 3, CeilingY - 3, 32, 2, Green.WithAlpha(0.3f));
        }
    }

    /// <summary>Most lamps burn steady; a few in every corridor stutter.</summary>
    private bool LampOn(int lamp)
    {
        if (Hash(lamp) % 6 != 0)
            return true;

        return MathF.Sin(_clock * 13 + lamp) + MathF.Sin(_clock * 7.3f + lamp * 2) > -0.6f;
    }

    private void DrawWall(DrawingHandleScreen handle)
    {
        Fill(handle, 0, CeilingY, ScreenWidth, FloorY - CeilingY, WallColour);
        var u = _scroll * WallDepth;
        for (var p = (int) MathF.Floor(u / PanelWidth); p * PanelWidth - u < ScreenWidth; p++)
        {
            DrawPanel(handle, p, p * PanelWidth - u);
        }
    }

    private void DrawPanel(DrawingHandleScreen handle, int panel, float x)
    {
        var hash = Hash(panel);
        var kind = (int) (hash % 16);
        Fill(handle, x + 2, CeilingY + 4, PanelWidth - 4, FloorY - CeilingY - 20, PanelColour);
        Fill(handle, x, CeilingY, 1, FloorY - CeilingY, Dim.WithAlpha(0.6f));
        Fill(handle, x + 1, CeilingY, 1, FloorY - CeilingY, Color.Black.WithAlpha(0.4f));
        Fill(handle, x, 116, PanelWidth, 3, Dim.WithAlpha(0.45f));
        Fill(handle, x, 116, PanelWidth, 1, Green.WithAlpha(0.25f));
        Fill(handle, x, FloorY - 14, PanelWidth, 14, CeilingColour);
        Fill(handle, x, FloorY - 14, PanelWidth, 1, Dim.WithAlpha(0.6f));
        var rivet = Dim.WithAlpha(0.8f);
        Fill(handle, x + 5, CeilingY + 7, 1, 1, rivet);
        Fill(handle, x + PanelWidth - 6, CeilingY + 7, 1, 1, rivet);
        Fill(handle, x + 5, FloorY - 20, 1, 1, rivet);
        Fill(handle, x + PanelWidth - 6, FloorY - 20, 1, 1, rivet);
        switch (kind)
        {
            case 5:
            case 6:
                // vent grille
                Fill(handle, x + 16, 126, 32, 20, Color.Black.WithAlpha(0.7f));
                for (var k = 0; k < 6; k++)
                {
                    Fill(handle, x + 18, 128 + k * 3, 28, 1.2f, Dim.WithAlpha(0.7f));
                }

                break;
            case 7:
            case 8:
                // window onto the colony outside
                Fill(handle, x + 8, 52, 48, 56, Color.Black);
                DrawOutside(handle, x + 10, 54, 44, 52);
                Fill(handle, x + 31.5f, 54, 1, 52, Dim.WithAlpha(0.6f));
                Fill(handle, x + 8, 52, 48, 1, Green.WithAlpha(0.5f));
                Fill(handle, x + 8, 107, 48, 1, Dim.WithAlpha(0.7f));
                Quad(handle, new Vector2(x + 14, 54), new Vector2(x + 20, 54), new Vector2(x + 12, 70), new Vector2(x + 10, 70), Green.WithAlpha(0.06f));
                break;
            case 9:
                // bulkhead
                Fill(handle, x + 10, 52, 44, FloorY - 52, Color.Black);
                Fill(handle, x + 12, 58, 40, FloorY - 58, LeafColour);
                Fill(handle, x + 31.5f, 58, 1, FloorY - 58, Color.Black);
                Fill(handle, x + 16, 70, 10, 14, Sky);
                Fill(handle, x + 38, 70, 10, 14, Sky);
                for (var k = 0; k < 6; k++)
                {
                    var sx = x + 10 + k * 8;
                    Quad(handle, new Vector2(sx, 52), new Vector2(sx + 4, 52), new Vector2(sx + 1, 57), new Vector2(sx - 3, 57), Amber.WithAlpha(0.55f));
                }

                var locked = (hash >> 8) % 3 == 0;
                var blink = _clock % 1.2f < 0.8f;
                Circle(handle, new Vector2(x + 58, 66), 1.6f, locked ? Alarm.WithAlpha(blink ? 1 : 0.3f) : Green);
                break;
            case 10:
                // department sign
                Fill(handle, x + 12, 66, 40, 14, Color.Black);
                Outline(handle, x + 12, 66, 40, 14, Green.WithAlpha(0.8f));
                CentreText(handle, _signFont, _signs[(hash >> 8) % (uint) _signs.Length], x + 32, 69, Green);
                Quad(handle, new Vector2(x + 28, 84), new Vector2(x + 36, 84), new Vector2(x + 32, 89), new Vector2(x + 32, 89), Dim);
                break;
            case 11:
                // wall terminal
                Fill(handle, x + 18, 80, 28, 30, Color.Black.WithAlpha(0.8f));
                Outline(handle, x + 18, 80, 28, 30, Dim);
                Fill(handle, x + 21, 83, 22, 15, ScreenGlow);
                for (var k = 0; k < 3; k++)
                {
                    Fill(handle, x + 23, 85 + k * 4, 6 + Hash01(panel, k) * 12, 1.5f, Green.WithAlpha(0.8f));
                }

                if (_clock % 1 < 0.5f)
                    Fill(handle, x + 23, 97, 3, 1.5f, Bright);

                Fill(handle, x + 16, 110, 32, 3, Dim.WithAlpha(0.7f));
                break;
            case 12:
                // pipes and a valve
                Fill(handle, x + 20, CeilingY, 4, FloorY - CeilingY - 14, Dim.WithAlpha(0.45f));
                Fill(handle, x + 20, CeilingY, 1, FloorY - CeilingY - 14, Green.WithAlpha(0.3f));
                Fill(handle, x + 28, CeilingY, 3, FloorY - CeilingY - 14, Dim.WithAlpha(0.35f));
                Circle(handle, new Vector2(x + 26, 96), 6, Amber.WithAlpha(0.7f), false);
                Line(handle, new Vector2(x + 20, 96), new Vector2(x + 32, 96), Amber.WithAlpha(0.6f));
                Line(handle, new Vector2(x + 26, 90), new Vector2(x + 26, 102), Amber.WithAlpha(0.6f));
                break;
            case 13:
                // hazard striped kick plate
                for (var k = 0; k < 8; k++)
                {
                    var sx = x + k * 8 + 4;
                    Quad(handle, new Vector2(sx, FloorY - 13), new Vector2(sx + 4, FloorY - 13), new Vector2(sx, FloorY), new Vector2(sx - 4, FloorY), Amber.WithAlpha(0.4f));
                }

                break;
            case 14:
                // somebody already died here
                if ((hash >> 8) % 2 == 0)
                    goto default;

                DrawSprite(handle, Frame(Splatter, SplatterState((int) ((hash >> 9) % 6)), RsiDirection.South), new Vector2(x + 32, 138),
                    new Vector2(30, 30), (hash >> 12) % 628 / 100f, DarkBlood.WithAlpha(0.65f));
                Fill(handle, x + 30, 148, 1.5f, 14, DarkBlood.WithAlpha(0.5f));
                Fill(handle, x + 36, 146, 1, 9, DarkBlood.WithAlpha(0.45f));
                break;
            default:
                Fill(handle, x + 10, 60, PanelWidth - 20, 1, Dim.WithAlpha(0.25f));
                Fill(handle, x + 10, 140, PanelWidth - 20, 1, Dim.WithAlpha(0.25f));
                break;
        }
    }

    /// <summary>The colony outside a window: stars, rain and hills, far enough away to crawl past.</summary>
    private void DrawOutside(DrawingHandleScreen handle, float x, float y, float width, float height)
    {
        Fill(handle, x, y, width, height, Sky);
        const float cell = 11;
        var u = _scroll * OutsideDepth;
        var last = (int) MathF.Floor((x + width + u) / cell);
        for (var c = (int) MathF.Floor((x + u) / cell); c <= last; c++)
        {
            for (var k = 0; k < 2; k++)
            {
                var sx = c * cell + Hash01(c, k * 2 + 1) * cell - u;
                if (sx < x + 1 || sx > x + width - 2)
                    continue;

                var sy = y + 2 + Hash01(c, k * 2 + 2) * height * 0.6f;
                var twinkle = 0.4f + 0.6f * MathF.Abs(MathF.Sin(_clock * (1 + Hash01(c, 9) * 3) + c));
                Fill(handle, sx, sy, 1, 1, Green.WithAlpha((0.2f + 0.6f * Hash01(c, k + 5)) * twinkle));
            }
        }

        var hills = _scroll * HillDepth;
        for (var cx = x; cx < x + width; cx += 2)
        {
            var ridge = Ridge(cx + hills);
            Fill(handle, cx, y + height - ridge, MathF.Min(2, x + width - cx), ridge, Hills);
        }

        for (var k = 0; k < 5; k++)
        {
            var rx = x + (k * 9.7f + _clock * 14) % width;
            var ry = y + (k * 23 + _clock * 120) % (height - 6);
            Line(handle, new Vector2(rx, ry), new Vector2(MathF.Max(x, rx - 2), ry + 5), Green.WithAlpha(0.18f));
        }
    }

    private static float Ridge(float u)
    {
        var i = (int) MathF.Floor(u / 18);
        var f = u / 18 - i;
        f = f * f * (3 - 2 * f);
        var a = Hash01(i, 3);
        return 6 + 16 * (a + (Hash01(i + 1, 3) - a) * f);
    }

    private void DrawFloor(DrawingHandleScreen handle)
    {
        Fill(handle, 0, FloorY, ScreenWidth, ScreenHeight - FloorY, FloorColour);
        Fill(handle, 0, FloorY + 11, ScreenWidth, 1, Dim.WithAlpha(0.3f));
        Fill(handle, 0, FloorY + 24, ScreenWidth, 1, Dim.WithAlpha(0.2f));
        for (var c = (int) MathF.Floor((_scroll - 120) / 40); c * 40 - _scroll < ScreenWidth + 120; c++)
        {
            var sx = c * 40 - _scroll;
            Line(handle, new Vector2(sx, FloorY + 2), new Vector2(sx + (sx - ScreenWidth / 2) * 0.5f, ScreenHeight), Dim.WithAlpha(0.35f));
            if (Hash(c) % 3 != 0)
                continue;

            // a floor grate
            Fill(handle, sx + 10, FloorY + 4, 18, 1, Color.Black.WithAlpha(0.6f));
            Fill(handle, sx + 10, FloorY + 7, 19, 1, Color.Black.WithAlpha(0.6f));
        }

        Fill(handle, 0, FloorY, ScreenWidth, 1.5f, Green.WithAlpha(0.65f));
        Fill(handle, 0, FloorY + 1.5f, ScreenWidth, 2.5f, Dim.WithAlpha(0.25f));
    }

    private void DrawLights(DrawingHandleScreen handle)
    {
        var u = _scroll * WallDepth;
        for (var c = (int) MathF.Floor((u - 30) / 96) - 1; c * 96 + 30 - u < ScreenWidth + 40; c++)
        {
            if (!LampOn(c))
                continue;

            var x = c * 96 + 30 - u;
            Quad(handle, new Vector2(x + 2, CeilingY - 3), new Vector2(x + 24, CeilingY - 3), new Vector2(x + 62, FloorY + 14), new Vector2(x - 36, FloorY + 14), Green.WithAlpha(0.035f));
            Ellipse(handle, new Vector2(x + 13, FloorY + 9), 40, 6, Green.WithAlpha(0.05f));
        }
    }

    private void DrawDecals(DrawingHandleScreen handle)
    {
        var saved = handle.GetTransform();
        var start = _decalCount < MaxDecals ? 0 : _decalNext;
        for (var i = 0; i < _decalCount; i++)
        {
            var decal = _decals[(start + i) % MaxDecals];
            var x = decal.X - _scroll;
            if (x < -decal.Size || x > ScreenWidth + decal.Size)
                continue;

            var colour = decal.Colour;
            if (decal.Acid)
                colour = Color.InterpolateBetween(colour, DarkAcid.WithAlpha(colour.A), MathF.Min(1, decal.Age / 4));

            // the floor is seen at a slant, so its stains are squashed flat
            var half = decal.Size * _s / 2;
            var at = (new Vector2(x, decal.Y) + _o) * _s;
            handle.SetTransform(Matrix3x2.CreateRotation(decal.Rotation) * Matrix3x2.CreateScale(1, 0.45f) * Matrix3x2.CreateTranslation(at) * saved);
            var texture = Frame(Splatter, SplatterState(decal.State), RsiDirection.South);
            handle.DrawTextureRect(texture, new UIBox2(-half, -half, half, half), colour);
            if (decal.Acid && decal.Age < 2.5f)
                handle.DrawTextureRect(texture, new UIBox2(-half, -half, half, half), Acid.WithAlpha(0.5f * (1 - decal.Age / 2.5f)));
        }

        handle.SetTransform(saved);
    }

    private static string SplatterState(int state)
    {
        return state switch
        {
            0 => "splatter-0",
            1 => "splatter-1",
            2 => "splatter-2",
            3 => "splatter-3",
            4 => "splatter-4",
            _ => "splatter-5",
        };
    }

    private void DrawShadows(DrawingHandleScreen handle)
    {
        var shade = Color.Black.WithAlpha(0.45f);
        foreach (var human in _humans)
        {
            if (!human.Active)
                continue;

            var x = human.X - _scroll;
            if (human.State == HumanState.Down)
                Ellipse(handle, new Vector2(x + human.Fall * 28, FloorY + 2), 30, 4, shade);
            else
                Ellipse(handle, new Vector2(x, FloorY + 1), 12, 3, shade);
        }

        if (_phase is Phase.Attract or Phase.Run)
        {
            var lift = MathF.Min(_y, 100) / 100;
            Ellipse(handle, new Vector2(HuggerX, FloorY + 1), 12 * (1 - lift * 0.45f), 3, Color.Black.WithAlpha(0.45f * (1 - lift * 0.6f)));
        }

        if (_corpse)
            Ellipse(handle, new Vector2(_corpseAt.X - _scroll, FloorY + 1), 11, 2.5f, shade);
    }

    private void DrawHuman(DrawingHandleScreen handle, Human human)
    {
        var x = human.X - _scroll;
        if (x < -60 || x > ScreenWidth + 60)
            return;

        Pose(human, out var rotation, out var lean, out var bob);
        var saved = handle.GetTransform();
        var pivot = (new Vector2(x + lean, FloorY - bob) + _o) * _s;
        handle.SetTransform(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(pivot) * saved);
        var box = new UIBox2(-HumanSize / 2 * _s, -HumanSize * _s, HumanSize / 2 * _s, 0);
        foreach (var layer in human.Layers)
        {
            handle.DrawTextureRect(human.East ? layer.East : layer.West, box, layer.Tint * Phosphor);
        }

        if (human.Infected)
        {
            // the hugger wrapped round the face, squeezing faster while the host still fights it
            var squeeze = human.State == HumanState.Down ? human.Timer / 0.25f : human.Timer / 0.08f;
            var mask = Frame(Parasite, "equipped-MASK", human.East ? RsiDirection.East : RsiDirection.West, (int) squeeze);
            handle.DrawTextureRect(mask, box, Phosphor);
        }

        handle.SetTransform(saved);
    }

    /// <summary>How a human stands this frame: rotation about the feet in radians, a sideways lean and a bob, in units.</summary>
    private void Pose(Human human, out float rotation, out float lean, out float bob)
    {
        rotation = 0;
        lean = 0;
        bob = 0;
        switch (human.State)
        {
            case HumanState.Walk:
                bob = MathF.Abs(MathF.Sin(human.Step)) * 2;
                break;
            case HumanState.Flee:
                bob = MathF.Abs(MathF.Sin(human.Step)) * 2.5f;
                rotation = 0.12f;
                break;
            case HumanState.Stand:
            case HumanState.Aim:
                bob = human.Marine && _phase == Phase.Run
                    ? MathF.Abs(MathF.Sin(human.Step)) * 1.5f
                    : 0.5f + MathF.Sin(_clock * 2 + human.X) * 0.5f;
                break;
            case HumanState.Alert:
                lean = (Fx() - 0.5f) * 1.2f;
                rotation = 0.06f;
                break;
            case HumanState.Kick:
                var kick = MathF.Sin(MathF.Min(1, human.Timer / KickTime) * MathF.PI);
                rotation = (human.East ? 0.18f : -0.18f) * kick;
                lean = (human.East ? 3 : -5) * kick;
                break;
            case HumanState.Latched:
                if (human.Timer < Struggle)
                {
                    rotation = MathF.Sin(human.Timer * 40) * 0.08f;
                    break;
                }

                // goes over backwards, slow at first and then all at once, settling onto the floor
                var fall = Math.Clamp((human.Timer - Struggle) / FallTime, 0, 1);
                rotation = human.Fall * MathF.PI / 2 * fall * fall;
                bob = -6 * fall * fall;
                break;
            case HumanState.Shot:
                var drop = Math.Clamp(human.Timer / (FallTime * 0.8f), 0, 1);
                rotation = human.Fall * MathF.PI / 2 * drop * drop;
                bob = -6 * drop * drop;
                break;
            case HumanState.Down:
                rotation = human.Fall * MathF.PI / 2;
                bob = -6;
                if (human.Timer < 1.5f)
                    rotation += MathF.Sin(human.Timer * 50) * 0.025f * (1 - human.Timer / 1.5f);
                break;
        }
    }

    private void DrawHugger(DrawingHandleScreen handle)
    {
        if (_phase is not (Phase.Attract or Phase.Run))
            return;

        Texture texture;
        var rotation = 0f;
        if (_grounded)
        {
            texture = Frame(Parasite, "running", RsiDirection.East, (int) (_anim / 0.1f));
        }
        else if (_vy > 90)
        {
            // nose up on the way up, then legs spread for the grab
            texture = Frame(Parasite, "running", RsiDirection.East, 1);
            rotation = -0.35f;
        }
        else
        {
            texture = Frame(Parasite, "thrown", RsiDirection.South);
            rotation = _vy < -100 ? 0.15f : 0;
        }

        var size = new Vector2(HuggerSize, HuggerSize);
        if (_squash > 0)
            size *= new Vector2(1 - 0.14f * _squash, 1 + 0.22f * _squash);
        else if (_squash < 0)
            size *= new Vector2(1 - 0.2f * _squash, 1 + 0.26f * _squash);

        if (_duck && _grounded)
            size *= new Vector2(1.14f, 0.55f);

        var feet = FloorY - _y;
        DrawSprite(handle, texture, new Vector2(HuggerX, feet + HuggerFootPad * size.Y / HuggerSize - size.Y / 2), size, rotation, Phosphor);
    }

    private void DrawCorpse(DrawingHandleScreen handle)
    {
        if (!_corpse)
            return;

        var x = _corpseAt.X - _scroll;
        var feet = FloorY - _corpseAt.Y;
        if (!_corpseDown)
        {
            DrawSprite(handle, Frame(Parasite, "thrown", RsiDirection.South), new Vector2(x, feet - 16), new Vector2(HuggerSize, HuggerSize), _corpseRotation, Phosphor);
            return;
        }

        DrawSprite(handle, Frame(Parasite, "dead", RsiDirection.South), new Vector2(x, feet - HuggerSize / 2 + 1.25f), new Vector2(HuggerSize, HuggerSize), 0, Phosphor);
    }

    /// <summary>Laser sights on the lanes about to be fired down, marines' aim, tracers and muzzle flashes.</summary>
    private void DrawFire(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _shotCount; i++)
        {
            var shot = _shots[i];
            if (shot.Delay >= Warning)
                continue;

            var k = 1 - shot.Delay / Warning;
            var flicker = 0.75f + 0.25f * MathF.Sin(_clock * 60 + i);
            Fill(handle, ScreenWidth - 90 * k, shot.Lane - 0.5f, 90 * k, 1, Laser.WithAlpha(0.3f * k * flicker));
            Circle(handle, new Vector2(ScreenWidth - 1, shot.Lane), 2 + 7 * k, TracerGlow.WithAlpha((0.15f + 0.35f * k) * flicker));
        }

        foreach (var human in _humans)
        {
            if (!human.Active || human.State != HumanState.Aim || _phase != Phase.Run)
                continue;

            var muzzle = Barrel(human.X - _scroll);
            var aim = MathF.Min(1, human.Timer / MarineAim);
            Line(handle, muzzle, Vector2.Lerp(muzzle, Target(), aim), Laser.WithAlpha(0.35f + 0.3f * MathF.Sin(_clock * 50)));
            if (aim >= 1)
                Circle(handle, Target(), 1.5f, Laser);
        }

        for (var i = 0; i < _bulletCount; i++)
        {
            var bullet = _bullets[i];
            var tail = bullet.At - Vector2.Normalize(bullet.Velocity) * Tracer;
            Beam(handle, tail, bullet.At, 5, TracerGlow.WithAlpha(0.3f));
            Beam(handle, tail, bullet.At, 2, TracerCore);
            Fill(handle, bullet.At.X - 1, bullet.At.Y - 1, 2, 2, Color.White);
        }

        for (var i = 0; i < _flashCount; i++)
        {
            var flash = _flashes[i];
            var k = 1 - flash.Age / 0.07f;
            Circle(handle, flash.At, 7 * k + 2, TracerGlow.WithAlpha(0.5f * k));
            Circle(handle, flash.At, 3 * k + 1, TracerCore.WithAlpha(k));
            Beam(handle, flash.At + new Vector2(-12 * k, 0), flash.At + new Vector2(4, 0), 2, TracerCore.WithAlpha(k));
            Beam(handle, flash.At + new Vector2(0, -6 * k), flash.At + new Vector2(0, 6 * k), 1.5f, TracerGlow.WithAlpha(k));
        }
    }

    private void DrawParticles(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _particleCount; i++)
        {
            var p = _particles[i];
            var alpha = Math.Clamp(1 - p.Age / p.Life, 0, 1);
            var colour = p.Colour.WithAlpha(p.Colour.A * alpha);
            var at = new Vector2(p.At.X - _scroll, p.At.Y);
            switch (p.Kind)
            {
                case Spark:
                    Line(handle, at, at - p.Velocity * 0.03f, colour);
                    break;
                case Smoke:
                    Circle(handle, at, p.Size, colour.WithAlpha(colour.A * 0.6f));
                    break;
                default:
                    Fill(handle, at.X - p.Size / 2, at.Y - p.Size / 2, p.Size, p.Size, colour);
                    break;
            }
        }
    }

    /// <summary>Speed lines once the run gets going.</summary>
    private void DrawStreaks(DrawingHandleScreen handle)
    {
        if (_phase != Phase.Run || _speed < 250)
            return;

        var alpha = (_speed - 250) / (MaxSpeed - 250) * 0.18f;
        const float cell = 70;
        var u = _scroll * 1.25f;
        for (var c = (int) MathF.Floor(u / cell); c * cell - u < ScreenWidth; c++)
        {
            if ((Hash(c) & 3) != 0)
                continue;

            var x = c * cell - u + Hash01(c, 1) * cell;
            Fill(handle, x, 50 + Hash01(c, 2) * 120, 20 + Hash01(c, 3) * 40, 1, Bright.WithAlpha(alpha));
        }
    }

    private void DrawPopups(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _popupCount; i++)
        {
            var popup = _popups[i];
            if (popup.Text == null)
                continue;

            var progress = popup.Age / popup.Life;
            var colour = popup.Colour.WithAlpha(Math.Clamp(2 - progress * 2, 0, 1));
            CentreText(handle, _smallFont, popup.Text, popup.At.X - _scroll, popup.At.Y - 18 * progress, colour);
        }
    }

    private void DrawHud(DrawingHandleScreen handle)
    {
        Fill(handle, 0, 0, ScreenWidth, HudHeight, HudColour);
        Fill(handle, 0, HudHeight - 1, ScreenWidth, 1, _hudFlash > 0 && _hudFlash % 0.2f > 0.1f ? Bright : Dim);
        if (_best != _bestShown)
        {
            _bestShown = _best;
            _bestText = Loc.GetString("cmu-game-hugger-best", ("score", _best.ToString("D5")));
        }

        var width = handle.GetDimensions(_smallFont, _bestText, _s).X / _s;
        Text(handle, _smallFont, _bestText, ScreenWidth - 6 - width, 3, Dim);

        var focused = HasKeyboardFocus();
        var blink = _clock % 1.1f < 0.75f;
        switch (_phase)
        {
            case Phase.Attract:
                CentreText(handle, _bigFont, _title, ScreenWidth / 2, 62, Green);
                if (focused && blink)
                    CentreText(handle, _font, _start, ScreenWidth / 2, 92, Bright);

                CentreText(handle, _smallFont, _keys, ScreenWidth / 2, 108, Dim);
                break;
            case Phase.Run:
                if (_paused)
                    CentreText(handle, _bigFont, _pausedText, ScreenWidth / 2, 80, Green);
                break;
            default:
                if (_over < 0.15f)
                    break;

                CentreText(handle, _bigFont, _endText, ScreenWidth / 2, 66, _endColour);
                if (focused && _over >= RestartDelay && blink)
                    CentreText(handle, _font, _again, ScreenWidth / 2, 96, Bright);
                break;
        }
    }

    /// <summary>Scanlines, a slow roll bar and darkened edges, so the whole screen reads as an old tube.</summary>
    private void DrawCrt(DrawingHandleScreen handle)
    {
        var line = Color.Black.WithAlpha(0.22f);
        for (var y = 1f; y < ScreenHeight; y += 2)
        {
            Fill(handle, 0, y, ScreenWidth, 1, line);
        }

        var roll = _clock * 26 % (ScreenHeight + 60) - 30;
        Fill(handle, 0, roll, ScreenWidth, 22, Green.WithAlpha(0.025f));
        for (var k = 0; k < 3; k++)
        {
            var edge = Color.Black.WithAlpha(0.14f - k * 0.04f);
            var inset = k * 3;
            Fill(handle, inset, inset, ScreenWidth - inset * 2, 3, edge);
            Fill(handle, inset, ScreenHeight - inset - 3, ScreenWidth - inset * 2, 3, edge);
            Fill(handle, inset, inset + 3, 3, ScreenHeight - inset * 2 - 6, edge);
            Fill(handle, ScreenWidth - inset - 3, inset + 3, 3, ScreenHeight - inset * 2 - 6, edge);
        }
    }

    #endregion

    #region Drawing helpers

    // Everything takes screen units and applies the UI scale and the current shake.

    private void Fill(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        x += _o.X;
        y += _o.Y;
        handle.DrawRect(new UIBox2(x * _s, y * _s, (x + width) * _s, (y + height) * _s), colour);
    }

    private void Outline(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        x += _o.X;
        y += _o.Y;
        handle.DrawRect(new UIBox2(x * _s, y * _s, (x + width) * _s, (y + height) * _s), colour, false);
    }

    private void Line(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color colour)
    {
        handle.DrawLine((from + _o) * _s, (to + _o) * _s, colour);
    }

    private void Circle(DrawingHandleScreen handle, Vector2 at, float radius, Color colour, bool filled = true)
    {
        handle.DrawCircle((at + _o) * _s, radius * _s, colour, filled);
    }

    private void Ellipse(DrawingHandleScreen handle, Vector2 at, float radiusX, float radiusY, Color colour)
    {
        var saved = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(1, radiusY / radiusX) * Matrix3x2.CreateTranslation((at + _o) * _s) * saved);
        handle.DrawCircle(Vector2.Zero, radiusX * _s, colour);
        handle.SetTransform(saved);
    }

    /// <summary>A straight bar of some width from one point to another, for tracers.</summary>
    private void Beam(DrawingHandleScreen handle, Vector2 from, Vector2 to, float width, Color colour)
    {
        var along = to - from;
        var length = along.Length();
        if (length < 0.01f)
            return;

        var side = new Vector2(-along.Y, along.X) / length * (width / 2);
        Span<Vector2> points = stackalloc Vector2[4];
        points[0] = (from + side + _o) * _s;
        points[1] = (from - side + _o) * _s;
        points[2] = (to + side + _o) * _s;
        points[3] = (to - side + _o) * _s;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, points, colour);
    }

    private void Quad(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
    {
        Span<Vector2> points = stackalloc Vector2[4];
        points[0] = (a + _o) * _s;
        points[1] = (b + _o) * _s;
        points[2] = (c + _o) * _s;
        points[3] = (d + _o) * _s;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, colour);
    }

    /// <summary>Draws a texture centred on a spot at a size, turned by <paramref name="rotation"/> radians.</summary>
    private void DrawSprite(DrawingHandleScreen handle, Texture texture, Vector2 at, Vector2 size, float rotation, Color modulate)
    {
        var centre = (at + _o) * _s;
        var half = size * _s / 2;
        if (rotation == 0)
        {
            handle.DrawTextureRect(texture, new UIBox2(centre - half, centre + half), modulate);
            return;
        }

        var saved = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(centre) * saved);
        handle.DrawTextureRect(texture, new UIBox2(-half, half), modulate);
        handle.SetTransform(saved);
    }

    /// <summary>Phosphor text: a soft glow behind, then the letters.</summary>
    private void Text(DrawingHandleScreen handle, Font font, string text, float x, float y, Color colour)
    {
        var at = (new Vector2(x, y) + _o) * _s;
        handle.DrawString(font, at + new Vector2(_s, _s), text, _s, Color.Black.WithAlpha(colour.A * 0.8f));
        handle.DrawString(font, at + new Vector2(-_s, 0), text, _s, colour.WithAlpha(colour.A * 0.25f));
        handle.DrawString(font, at + new Vector2(_s, 0), text, _s, colour.WithAlpha(colour.A * 0.25f));
        handle.DrawString(font, at, text, _s, colour);
    }

    private void CentreText(DrawingHandleScreen handle, Font font, string text, float x, float y, Color colour)
    {
        var width = handle.GetDimensions(font, text, _s).X / _s;
        Text(handle, font, text, x - width / 2, y, colour);
    }

    #endregion
}
