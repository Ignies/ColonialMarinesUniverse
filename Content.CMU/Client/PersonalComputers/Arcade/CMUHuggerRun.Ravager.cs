using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Stage 5, the finale: the runner has become a ravager, and it bulldozes up a burning colony street through five
/// waves of marines, seen side-on like a brawler. Space slashes in a three-hit claw chain that ends in a tail sweep;
/// a direction held with Space, or a double-tapped direction, charges, flattening everything in the path and
/// breaking riot shields. Riflemen shoot down their lane, smartgunners hose a long stream, flamers leave the ground
/// burning, riot marines block claws from the front, a sentry is dropped in, and an APC brings the heavy squad.
/// Marines fly when hit, bleed where they land, scream and shed their guns. Kills chain a combo multiplier and fill
/// a rage meter that sets off a short unstoppable frenzy. Clearing the last wave counts up the campaign tally and
/// wins the run; running out of health loses it.
/// </summary>
public sealed class CMUHuggerRavager : CMUHuggerStage
{
    #region Layout and tuning

    // The kerb at the back of the street; feet walk between StreetTop and StreetBottom.
    private const float CurbY = 130;
    private const float StreetTop = 136;
    private const float StreetBottom = 210;

    /// <summary>Each wave is fought with the camera locked on its own stretch of street, this far apart.</summary>
    private const float ArenaWidth = 360;

    private const float RavagerScale = 1.6f;
    private const float RavagerBox = 64 * RavagerScale;
    private const float HumanScale = 2.2f;
    private const float HumanBox = 32 * HumanScale;
    private const float GearBox = 32 * 1.4f;
    private const float BarWidth = 84;

    private const float MaxHealth = 100;
    private const float WalkSpeed = 92;
    private const float DepthSpeed = 58;
    private const float FrenzySpeed = 1.35f;
    private const float StepTime = 0.34f;

    // The ravager's body for bullets: half its width, and how far off its depth a lane still hits it.
    private const float HalfWidth = 24;
    private const float BodyDepth = 15;

    // Slashes come in a chain of three; the third is a wide tail sweep that launches.
    private const float SlashTime = 0.24f;
    private const float SweepTime = 0.34f;
    private const float StrikeAt = 0.07f;
    private const float ChainWindow = 0.22f;
    private const float SlashReach = 64;
    private const float SweepReach = 80;
    private const float SlashDepth = 18;
    private const float SweepDepth = 22;
    private const float LungeSpeed = 110;

    // The charge: a crouch, a straight run that flattens what it meets, a skid.
    private const float ChargeWindup = 0.14f;
    private const float ChargeTime = 0.5f;
    private const float SkidTime = 0.22f;
    private const float ChargeSpeed = 340;
    private const float ChargeRegen = 70;
    private const float DoubleTap = 0.28f;
    private const float ChargeReach = 42;
    private const float ChargeDepth = 20;
    private const float TrampleDepth = 7;

    private const float ComboWindow = 2.8f;
    private const int ComboStep = 4;
    private const int MaxMultiplier = 5;
    private const float FrenzyTime = 6;
    private const float RagePerKill = 9;
    private const float RageCharge = 4;
    private const float HealPerKill = 2;

    private const float Gravity = 900;
    private const float StaggerTime = 0.32f;
    private const float BulletReachZ = 95;
    private const float RifleSpeed = 380;
    private const float SmartSpeed = 420;
    private const float SentrySpeed = 400;

    // Damage to the ravager, of its 100 health.
    private const float RifleDamage = 3;
    private const float SmartDamage = 1.5f;
    private const float SentryDamage = 3;
    private const float ShellDamage = 10;
    private const float FlameDps = 16;
    private const float BurnDps = 9;
    private const float BashDamage = 7;
    private const float BlastDamage = 8;

    private const float FlameReach = 96;
    private const float FlameTime = 1.3f;
    private const float FireLife = 2.6f;
    private const float Fuse = 0.35f;

    // The APC parks at the back of the street; its wheels stand on ApcY.
    private const float ApcY = 152;
    private const float ApcHalf = 84;

    /// <summary>The APC sprite's 128 px frame at this scale; its wheels stand on the frame's row 112.</summary>
    private const float ApcScale = 1.35f;
    private const int ApcHealth = 40;
    private const int ApcBonus = 2000;
    private const int ApcDrops = 6;
    private const float ApcDriveTime = 2.4f;
    private const float ApcDropEvery = 5;
    private const float ApcFireEvery = 3.4f;
    private const float ShellTime = 0.9f;

    // The finale's tally: rows reveal one by one, then the run is won.
    private const float TallyStart = 1.4f;
    private const float TallyRow = 0.55f;
    private const int TallyRows = 6;
    private const float WinAfter = TallyStart + TallyRows * TallyRow + 0.9f;
    private const int ComboBonus = 50;
    private const int HealthBonus = 20;
    private const int WaveBonus = 300;

    private const int MaxTroopers = 26;
    private const int MaxBullets = 48;
    private const int MaxShells = 4;
    private const int MaxFires = 24;
    private const int MaxDebris = 40;
    private const int MaxBlasts = 8;
    private const int MaxGhosts = 8;
    private const int MaxFlashes = 10;
    private const int MaxScorches = 16;
    private const int MaxDrawn = MaxTroopers + 2;

    private const byte Rifleman = 0;
    private const byte Smartgunner = 1;
    private const byte Flamer = 2;
    private const byte Riot = 3;
    private const byte Sentry = 4;

    // Per kind of trooper: health, points, the distance kept from the ravager and how far off it still attacks,
    // walking speed, aim time, and the muzzle in units in front of and above the feet.
    private static readonly int[] KindHealth = { 1, 2, 2, 3, 4 };
    private static readonly int[] KindPoints = { 100, 200, 200, 250, 300 };
    private static readonly float[] KindRange = { 135, 170, 62, 24, 0 };
    private static readonly float[] KindTolerance = { 50, 45, 26, 14, 999 };
    private static readonly float[] KindSpeed = { 48, 30, 42, 30, 0 };
    private static readonly float[] KindAim = { 0.45f, 0.65f, 0.35f, 0.3f, 0.4f };
    private static readonly float[] MuzzleX = { 29, 35, 33, 18, 33 };
    private static readonly float[] MuzzleZ = { 33, 27, 33, 30, 37 };

    /// <summary>Who comes in when, and from which side (1 the right, -1 the left), in seconds from the wave's start.</summary>
    private readonly record struct Spawn(float At, byte Kind, sbyte Side);

    private static readonly Spawn[][] Waves =
    {
        new Spawn[]
        {
            new(1.4f, Rifleman, 1), new(1.9f, Rifleman, 1), new(3.5f, Rifleman, -1), new(5.5f, Rifleman, 1), new(6f, Rifleman, 1),
        },
        new Spawn[]
        {
            new(0.4f, Rifleman, 1), new(0.8f, Rifleman, 1), new(2f, Smartgunner, 1), new(3.5f, Rifleman, -1), new(4f, Rifleman, -1),
            new(6.5f, Rifleman, 1), new(7f, Rifleman, 1),
        },
        new Spawn[]
        {
            new(0.4f, Flamer, 1), new(0.8f, Rifleman, 1), new(1.2f, Rifleman, 1), new(3f, Riot, -1), new(3.4f, Rifleman, -1),
            new(6f, Flamer, -1), new(6.4f, Riot, 1), new(6.8f, Rifleman, 1),
        },
        new Spawn[]
        {
            new(0.3f, Sentry, 1), new(0.8f, Riot, 1), new(1.2f, Rifleman, 1), new(1.5f, Rifleman, 1), new(3.5f, Smartgunner, -1),
            new(3.9f, Riot, -1), new(6f, Flamer, 1), new(6.4f, Rifleman, 1), new(6.8f, Smartgunner, 1), new(9f, Rifleman, -1),
            new(9.4f, Rifleman, -1),
        },
        new Spawn[]
        {
            new(2.6f, Riot, 1), new(3f, Riot, -1), new(3.4f, Smartgunner, -1), new(5.5f, Flamer, 1), new(5.9f, Flamer, -1),
            new(6.3f, Rifleman, 1), new(6.7f, Rifleman, -1), new(9f, Smartgunner, 1), new(9.4f, Rifleman, -1), new(9.8f, Sentry, -1),
        },
    };

    #endregion

    #region Art and sound

    private const string M41 = "/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi";
    private const string SmartGun = "/Textures/_RMC14/Objects/Weapons/Guns/SmartGuns/smart_gun.rsi";
    private const string M34 = "/Textures/_RMC14/Objects/Weapons/Guns/Flamethrower/m34.rsi";
    private const string Shields = "/Textures/Objects/Weapons/Melee/shields.rsi";
    private const string SmartPack = "/Textures/_RMC14/Objects/Clothing/Back/Smartpacks/V42/smartpack.rsi";
    private const string FuelPack = "/Textures/_RMC14/Objects/Clothing/Back/Satchels/Marines/flamethrower.rsi";
    private const string ApcRsi = "/Textures/_RMC14/Structures/Tyrargo_Vehicles/tyrargo_apc.rsi";
    private const string Barricades = "/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi";
    private const string Razorwire = "/Textures/_RMC14/Structures/Walls/Barricades/razorwire.rsi";
    private const string AmmoCrate = "/Textures/_RMC14/Structures/Storage/Crates/ammo.rsi";
    private const string BasicCrate = "/Textures/_RMC14/Structures/Storage/Crates/basic.rsi";
    private const string Barrels = "/Textures/_RMC14/Structures/barrels.rsi";
    private const string Trucks = "/Textures/_RMC14/Structures/Vehicles/vehicles2.rsi";
    private const string Floodlight = "/Textures/_RMC14/Structures/illuminator.rsi";
    private const string LandingSigns = "/Textures/_RMC14/Structures/landing_signs.rsi";
    private const string Asphalt = "/Textures/_RMC14/Tiles/asphalt/asphalt.png";
    private const string AsphaltWorn = "/Textures/_RMC14/Tiles/asphalt/asphalt13.png";
    private const string SentryRsi = "/Textures/_RMC14/Objects/Weapons/Sentries/sentry.rsi";
    private const string Wielded = "wielded-inhand-left";
    private const string Backpack = "equipped-BACKPACK";

    private const string RifleShot = "/Audio/Weapons/Guns/Gunshots/m41.ogg";
    private const string SentryShot = "/Audio/Weapons/Guns/Gunshots/gun_sentry.ogg";
    private const string Ignite = "/Audio/_RMC14/Weapons/Guns/Flamer/surv_flamer_ignite.ogg";
    private const string Cannon = "/Audio/_RMC14/Vehicle/weapons/autocannon_fire.ogg";
    private const string Engine = "/Audio/_RMC14/Vehicle/tank_driving.ogg";
    private const string Hydraulics = "/Audio/_RMC14/Machines/hydraulics_1.ogg";
    private const string BigBoom = "/Audio/_RMC14/Explosion/bigboom2.ogg";
    private const string MetalCrash = "/Audio/_RMC14/Effects/metal_crash.ogg";
    private const string MetalHit = "/Audio/_RMC14/Structures/metalhit.ogg";
    private const string Radio = "/Audio/_RMC14/Effects/radiostatic.ogg";
    private const string Tick = "/Audio/_RMC14/Effects/tick.ogg";
    private const string Windup = "/Audio/_RMC14/Xeno/crusher_windup_sound.ogg";
    private const string FrenzyRoar = "/Audio/_RMC14/Xeno/xenos_roaring.ogg";
    private const string Block = "/Audio/_RMC14/Xeno/alien_claw_block.ogg";
    private const string ShieldBreak = "/Audio/_RMC14/Bullets/shield_break_c1.ogg";
    private const string Knockdown = "/Audio/_RMC14/Weapons/alien_knockdown.ogg";
    private const string Gibbed = "/Audio/_RMC14/Effects/gibbed.ogg";
    private const string Splash = "/Audio/Effects/Fluids/splat.ogg";
    private const string BodyFall = "/Audio/CMU14/PersonalComputers/Games/fall.ogg";
    private const string DistantRoar = "/Audio/_RMC14/Xeno/alien_distantroar_3.ogg";

    private static readonly string[] Roars =
    {
        "/Audio/_RMC14/Xeno/alien_roar1.ogg", "/Audio/_RMC14/Xeno/alien_roar2.ogg", "/Audio/_RMC14/Xeno/alien_roar3.ogg",
        "/Audio/_RMC14/Xeno/alien_roar4.ogg", "/Audio/_RMC14/Xeno/alien_roar5.ogg", "/Audio/_RMC14/Xeno/alien_roar6.ogg",
    };

    private static readonly string[] EchoRoars = { "/Audio/_RMC14/Xeno/alien_echoroar_1.ogg", "/Audio/_RMC14/Xeno/alien_echoroar_2.ogg" };

    private static readonly string[] Claws =
    {
        "/Audio/_RMC14/Xeno/alien_claw_flesh1.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh2.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh3.ogg",
        "/Audio/_RMC14/Xeno/alien_claw_flesh4.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh5.ogg",
    };

    private static readonly string[] Swooshes =
    {
        "/Audio/CMU14/PersonalComputers/Games/swoosh_1.ogg", "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg",
        "/Audio/CMU14/PersonalComputers/Games/swoosh_3.ogg",
    };

    private static readonly string[] TailSwipes =
    {
        "/Audio/_RMC14/Xeno/alien_tail_swipe1.ogg", "/Audio/_RMC14/Xeno/alien_tail_swipe2.ogg", "/Audio/_RMC14/Xeno/alien_tail_swipe3.ogg",
    };

    private static readonly string[] ChargeSteps =
    {
        "/Audio/_RMC14/Xeno/alien_footstep_charge1.ogg", "/Audio/_RMC14/Xeno/alien_footstep_charge2.ogg",
        "/Audio/_RMC14/Xeno/alien_footstep_charge3.ogg",
    };

    private static readonly string[] Stomps =
    {
        "/Audio/_RMC14/Xeno/alien_footstep_large1.ogg", "/Audio/_RMC14/Xeno/alien_footstep_large2.ogg",
        "/Audio/_RMC14/Xeno/alien_footstep_large3.ogg",
    };

    private static readonly string[] Hisses =
    {
        "/Audio/_RMC14/Xeno/alien_hiss1.ogg", "/Audio/_RMC14/Xeno/alien_hiss2.ogg", "/Audio/_RMC14/Xeno/alien_hiss3.ogg",
    };

    private static readonly string[] Deaths = { "/Audio/_RMC14/Xeno/alien_death.ogg", "/Audio/_RMC14/Xeno/alien_death2.ogg" };

    private static readonly string[] Punches =
    {
        "/Audio/CMU14/PersonalComputers/Games/punch_1.ogg", "/Audio/CMU14/PersonalComputers/Games/punch_2.ogg",
    };

    private static readonly string[] Bones = { "/Audio/CMU14/Medical/bone_crack_1.ogg", "/Audio/CMU14/Medical/bone_crack_2.ogg" };

    private static readonly string[] Flesh =
    {
        "/Audio/_RMC14/Bullets/impact_flesh_1.ogg", "/Audio/_RMC14/Bullets/impact_flesh_2.ogg",
        "/Audio/_RMC14/Bullets/impact_flesh_3.ogg", "/Audio/_RMC14/Bullets/impact_flesh_4.ogg",
    };

    private static readonly string[] Ricochets = { "/Audio/_RMC14/Bullets/bullet_impact1.ogg", "/Audio/_RMC14/Bullets/bullet_impact2.ogg" };
    private static readonly string[] ShieldHits = { "/Audio/_RMC14/Bullets/shield_impact_c1.ogg", "/Audio/_RMC14/Bullets/shield_impact_c2.ogg" };

    private static readonly string[] SmartShots =
    {
        "/Audio/CMU14/Weapons/smartgun/gun_smartgun1.ogg", "/Audio/CMU14/Weapons/smartgun/gun_smartgun2.ogg",
        "/Audio/CMU14/Weapons/smartgun/gun_smartgun3.ogg", "/Audio/CMU14/Weapons/smartgun/gun_smartgun4.ogg",
    };

    private static readonly string[] FlamerFire =
    {
        "/Audio/_RMC14/Weapons/Guns/Flamer/surv_flamer_fire1.ogg", "/Audio/_RMC14/Weapons/Guns/Flamer/surv_flamer_fire2.ogg",
        "/Audio/_RMC14/Weapons/Guns/Flamer/surv_flamer_fire3.ogg", "/Audio/_RMC14/Weapons/Guns/Flamer/surv_flamer_fire4.ogg",
    };

    private static readonly string[] Explosions =
    {
        "/Audio/_RMC14/Explosion/explosion1.ogg", "/Audio/_RMC14/Explosion/explosion2.ogg", "/Audio/_RMC14/Explosion/explosion3.ogg",
        "/Audio/_RMC14/Explosion/explosion4.ogg", "/Audio/_RMC14/Explosion/explosion5.ogg",
    };

    private static readonly Color Sky = Color.FromHex("#010502");
    private static readonly Color FarColour = Color.FromHex("#031008");
    private static readonly Color BuildingColour = Color.FromHex("#05190B");
    private static readonly Color Tarmac = Color.FromHex("#031209");
    private static readonly Color Kerb = Color.FromHex("#0A2412");
    private static readonly Color Hull = Color.FromHex("#0B2A12");
    private static readonly Color TileTint = Color.FromHex("#5A7A60");

    /// <summary>Scenery props sit back in the dark, dimmer than the fighters.</summary>
    private static readonly Color PropTint = Color.FromHex("#8FAF92");
    private static readonly Color Shard = Color.FromHex("#2E4A30");
    private static readonly Color HurtTint = Color.FromHex("#FF7A60");
    private static readonly Color FrenzyTint = Color.FromHex("#FF4A3A");
    private static readonly Color Green = CMUHuggerRun.PhosphorGreen;
    private static readonly Color Dim = CMUHuggerRun.PhosphorDim;

    #endregion

    #region State

    private enum Pose : byte
    {
        Walk,
        Slash,
        Windup,
        Charge,
        Skid,
        Dead,
        Roar,
    }

    private enum Act : byte
    {
        Move,
        Aim,
        Fire,
        Stagger,
        Flying,
        Dead,
    }

    private enum WaveState : byte
    {
        Fight,
        Advance,
        Done,
    }

    /// <summary>A marine (or a sentry gun) on the street.</summary>
    private sealed class Trooper
    {
        public readonly CMUHuggerPerson Person = new();
        public bool Active;
        public byte Kind;
        public Act State;

        // X along the street, Y the depth (the feet's screen y), Z the height above the ground
        public float X;
        public float Y;
        public float Z;
        public float Vx;
        public float Vy;
        public float Vz;
        public float Rotation;
        public float Spin;
        public float Facing = -1;
        public int Hp;
        public float Timer;
        public float Cooldown;
        public float Gap;
        public float Step;
        public int Shots;
        public float Lane;

        /// <summary>Still holding its gun (or shield); it flies off when the trooper dies.</summary>
        public bool Armed;

        /// <summary>A riot marine's count of how long the ravager has been behind him.</summary>
        public float Turn;

        /// <summary>The charge that last hit it, so one charge hits each body once.</summary>
        public int Stamp;

        public float Fuse;
        public float Drop;
        public bool Struck;
        public bool Pooled;
        public float DiedAt;
    }

    private struct Bullet
    {
        public float X;
        public float Y;
        public float Z;
        public float Vx;
        public float Damage;
        public byte Kind;
        public bool Passed;
        public int Owner;
    }

    /// <summary>The APC's cannon round, lobbed from the turret onto where the ravager stood.</summary>
    private struct Shell
    {
        public Vector2 From;
        public Vector2 To;
        public float Age;
    }

    private struct Fire
    {
        public float X;
        public float Y;
        public float Age;
        public float Life;
        public float Size;
    }

    private struct Debris
    {
        public float X;
        public float Y;
        public float Z;
        public float Vx;
        public float Vy;
        public float Vz;
        public float Rotation;
        public float Spin;
        public float Size;
        public byte Kind;
        public bool Resting;
    }

    private struct Blast
    {
        public float X;
        public float Y;
        public float Age;
        public float Size;
    }

    private struct Ghost
    {
        public float X;
        public float Y;
        public float Facing;
        public float Tilt;
        public float Age;
    }

    private struct Flare
    {
        public Vector2 At;
        public float Age;
    }

    private struct Scorch
    {
        public float X;
        public float Y;
        public float Size;
    }

    private readonly Trooper[] _troopers = new Trooper[MaxTroopers];
    private readonly Bullet[] _bullets = new Bullet[MaxBullets];
    private int _bulletCount;
    private readonly Shell[] _shells = new Shell[MaxShells];
    private int _shellCount;
    private readonly Fire[] _fires = new Fire[MaxFires];
    private int _fireCount;
    private readonly Debris[] _debris = new Debris[MaxDebris];
    private int _debrisCount;
    private int _debrisNext;
    private readonly Blast[] _blasts = new Blast[MaxBlasts];
    private int _blastCount;
    private readonly Ghost[] _ghosts = new Ghost[MaxGhosts];
    private int _ghostCount;
    private int _ghostNext;
    private readonly Flare[] _flares = new Flare[MaxFlashes];
    private int _flareCount;
    private readonly Scorch[] _scorches = new Scorch[MaxScorches];
    private int _scorchCount;
    private int _scorchNext;

    // the street's asphalt, which is a tile texture rather than a sprite state
    private Texture? _asphalt;
    private Texture? _asphaltWorn;

    // reused every frame to draw the actors back to front
    private readonly int[] _order = new int[MaxDrawn];
    private readonly float[] _depths = new float[MaxDrawn];

    // the ravager
    private float _x;
    private float _y;
    private float _facing;
    private Pose _pose;
    private float _timer;
    private float _hp;
    private float _hurt;
    private float _push;
    private float _stride;
    private bool _moving;
    private float _step;
    private int _slash;
    private int _lastSlash;
    private float _chain;
    private bool _queued;
    private bool _struck;
    private Vector2 _chargeDir;
    private float _chargeMeter;
    private int _stamp;
    private float _chargeStep;
    private float _ghostTimer;
    private float _rage;
    private float _frenzy;
    private float _hissCool;
    private float _screamCool;
    private CMUArcadeKey _tapKey;
    private float _tapTime = -1;
    private bool _pooled;

    // the stage
    private float _time;
    private float _cam;
    private float _hitStop;
    private int _wave;
    private float _waveTime;
    private int _spawned;
    private WaveState _waveState;
    private float _banner;
    private float _ambient;
    private int _entryScore;
    private int _kills;
    private int _combo;
    private float _comboTime;
    private int _bestCombo;
    private float _cheer;
    private string? _cheerText;
    private float _finale = -1;
    private int _tallyShown;
    private readonly int[] _tally = new int[TallyRows];

    // the APC: 0 none, 1 driving in, 2 parked, 3 wrecked
    private int _apc;
    private float _apcX;
    private int _apcHp;
    private float _apcTimer;
    private float _apcDoor;
    private float _apcDropTimer;
    private int _apcDrops;
    private float _apcFire;
    private float _apcAim;
    private float _apcHurt;
    private float _apcWheel;
    private float _apcRecoil;
    private int _apcBooms;
    private float _apcBoomTimer;

    // strings, looked up once
    private string _waveText = string.Empty;
    private string _waveCount = string.Empty;
    private string _comboText = string.Empty;
    private int _comboShown = -1;
    private string? _blocked;
    private string? _shieldBroken;
    private string? _friendly;
    private string? _frenzyText;
    private string? _go;
    private string? _keys;
    private string? _final;
    private string? _heavy;
    private string? _purged;
    private string? _hpLabel;
    private string? _rageLabel;
    private string? _stencil;
    private string? _deadText;
    private string?[]? _tallyLabels;
    private readonly string?[] _tallyValues = new string?[TallyRows];
    private readonly int[] _tallyCached = new int[TallyRows];

    #endregion

    public CMUHuggerRavager()
    {
        for (var i = 0; i < MaxTroopers; i++)
        {
            _troopers[i] = new Trooper();
        }
    }

    public override string Title => Loc.GetString("cmu-game-hugger-ravager-title");
    public override string Hint => Loc.GetString("cmu-game-hugger-ravager-hint");

    /// <summary>The ravager can be hurt and the marines fight on.</summary>
    private bool Hostile => _pose != Pose.Dead && _finale < 0 && !Run.Over;

    /// <summary>Bullets and fire do nothing to a charging or frenzied ravager.</summary>
    private bool Immune => _frenzy > 0 || _pose is Pose.Windup or Pose.Charge;

    private float ArenaX(int wave)
    {
        return wave * ArenaWidth;
    }

    public override void Enter()
    {
        _x = 70;
        _y = 176;
        _facing = 1;
        _pose = Pose.Walk;
        _timer = 0;
        _hp = MaxHealth;
        _hurt = 0;
        _push = 0;
        _stride = 0;
        _moving = false;
        _step = 0;
        _slash = 0;
        _lastSlash = 0;
        _chain = 0;
        _queued = false;
        _struck = false;
        _chargeMeter = 100;
        _stamp = 0;
        _ghostTimer = 0;
        _rage = 0;
        _frenzy = 0;
        _hissCool = 0;
        _screamCool = 0;
        _tapKey = CMUArcadeKey.None;
        _tapTime = -1;
        _pooled = false;

        _time = 0;
        _cam = 0;
        _hitStop = 0;
        _wave = 0;
        _waveTime = 0;
        _spawned = 0;
        _waveState = WaveState.Fight;
        _banner = 0;
        _ambient = 3;
        _entryScore = Run.Points;
        _kills = 0;
        _combo = 0;
        _comboTime = 0;
        _bestCombo = 0;
        _cheer = 0;
        _finale = -1;
        _tallyShown = 0;
        Array.Clear(_tally);
        Array.Fill(_tallyCached, -1);

        _apc = 0;
        _apcHp = ApcHealth;
        _apcBooms = 0;

        foreach (var trooper in _troopers)
        {
            trooper.Active = false;
        }

        _bulletCount = 0;
        _shellCount = 0;
        _fireCount = 0;
        _debrisCount = 0;
        _debrisNext = 0;
        _blastCount = 0;
        _ghostCount = 0;
        _ghostNext = 0;
        _flareCount = 0;
        _scorchCount = 0;
        _scorchNext = 0;

        _blocked ??= Loc.GetString("cmu-game-hugger-ravager-blocked");
        _shieldBroken ??= Loc.GetString("cmu-game-hugger-ravager-shield");
        _friendly ??= Loc.GetString("cmu-game-hugger-ravager-friendly");
        _frenzyText ??= Loc.GetString("cmu-game-hugger-ravager-frenzy");
        _go ??= Loc.GetString("cmu-game-hugger-ravager-go");
        _keys ??= Loc.GetString("cmu-game-hugger-ravager-keys");
        _final ??= Loc.GetString("cmu-game-hugger-ravager-final");
        _heavy ??= Loc.GetString("cmu-game-hugger-ravager-heavy");
        _purged ??= Loc.GetString("cmu-game-hugger-ravager-purged");
        _hpLabel ??= Loc.GetString("cmu-game-hugger-ravager-hp");
        _rageLabel ??= Loc.GetString("cmu-game-hugger-ravager-rage");
        _stencil ??= Loc.GetString("cmu-game-hugger-ravager-stencil");
        _deadText ??= Loc.GetString("cmu-game-hugger-ravager-dead");
        _tallyLabels ??= new string?[]
        {
            Loc.GetString("cmu-game-hugger-ravager-tally-campaign"), Loc.GetString("cmu-game-hugger-ravager-tally-rampage"),
            Loc.GetString("cmu-game-hugger-ravager-tally-kills"), Loc.GetString("cmu-game-hugger-ravager-tally-combo"),
            Loc.GetString("cmu-game-hugger-ravager-tally-health"), Loc.GetString("cmu-game-hugger-ravager-tally-total"),
        };

        StartWave(0);
        Run.Camera = Vector2.Zero;
    }

    #region Input

    public override void OnKey(CMUArcadeInput input)
    {
        if (!input.Down || input.Repeat || !Hostile)
            return;

        switch (input.Key)
        {
            case CMUArcadeKey.Left:
            case CMUArcadeKey.Right:
                // a double tap charges that way
                if (input.Key == _tapKey && _time - _tapTime < DoubleTap)
                {
                    _tapKey = CMUArcadeKey.None;
                    if (TryCharge(new Vector2(input.Key == CMUArcadeKey.Right ? 1 : -1, 0)))
                        return;
                }

                _tapKey = input.Key;
                _tapTime = _time;
                break;
            case CMUArcadeKey.Action:
                var dx = Axis(CMUArcadeKey.Left, CMUArcadeKey.Right);
                var dy = Axis(CMUArcadeKey.Up, CMUArcadeKey.Down);
                if ((dx != 0 || dy != 0) && TryCharge(new Vector2(dx, dy)))
                    return;

                Slash();
                break;
        }
    }

    public override void OnRelease()
    {
        _tapKey = CMUArcadeKey.None;
    }

    private float Axis(CMUArcadeKey negative, CMUArcadeKey positive)
    {
        return (Run.Held(positive) ? 1 : 0) - (Run.Held(negative) ? 1 : 0);
    }

    #endregion

    #region Demo bot

    // The demo's ravager: it wades into the nearest marine, lines up on his lane and claws in chains, charges down a
    // lane with two or more marines in it when the meter is full, steps out of a gunman's lane when a round is coming,
    // goes round the front of a raised riot shield, and walks on up the street between waves.
    private float _botSlash;
    private float _botDodge;
    private float _botDodgeDir;
    private float _botCharge;

    public override void DemoThink(float dt)
    {
        _botSlash -= dt;
        _botDodge -= dt;
        _botCharge -= dt;
        if (!Hostile || _pose is Pose.Windup or Pose.Charge or Pose.Skid or Pose.Roar)
        {
            BotHold(0, 0);
            return;
        }

        var target = BotTarget();
        if (target == null && _apc is 1 or 2)
        {
            // the APC: get alongside and claw it
            var gap = _apcX - _x;
            if (MathF.Abs(gap) < ApcHalf + SlashReach * 0.4f && _y - ApcY < 26 && MathF.Sign(gap) == MathF.Sign(_facing))
            {
                BotHold(0, 0);
                if (_botSlash <= 0)
                {
                    Run.BotTap(CMUArcadeKey.Action);
                    _botSlash = 0.2f + Run.Fx() * 0.12f;
                }

                return;
            }

            BotHold(MathF.Abs(gap) > ApcHalf + 10 ? MathF.Sign(gap) : MathF.Sign(gap) * (MathF.Sign(gap) == MathF.Sign(_facing) ? 0 : 1),
                _y - ApcY > 20 ? -1 : 0);
            return;
        }

        if (target == null)
        {
            BotHold(_waveState == WaveState.Advance ? 1 : MathF.Sign(_cam + CMUHuggerRun.ScreenWidth / 2 - _x) * (MathF.Abs(_cam + 160 - _x) > 30 ? 1 : 0), 0);
            return;
        }

        if (_botDodge <= 0 && BotIncoming())
        {
            _botDodge = 0.4f + Run.Fx() * 0.2f;
            _botDodgeDir = _y > (StreetTop + StreetBottom) / 2 ? -1 : 1;
        }

        var dx = target.X - _x;
        var dy = target.Y - _y;

        // a lane with a crowd in it and a full meter: charge it
        if ((_chargeMeter >= 100 || _frenzy > 0) && _botCharge <= 0 && MathF.Abs(dy) < 12 && MathF.Abs(dx) is > 50 and < 220 && BotLane(target) >= 2)
        {
            _botCharge = 1.5f;
            BotHold(MathF.Sign(dx), 0);
            Run.BotTap(CMUArcadeKey.Action);
            return;
        }

        // stand just short of him on our side, or round the back of a raised shield
        var stand = target.X - MathF.Sign(dx == 0 ? _facing : dx) * 34;
        if (target.Kind == Riot && target.Armed && target.Facing * (_x - target.X) > 0 && _chargeMeter < 100)
            stand = target.X - target.Facing * 34;

        var ahead = (target.X - _x) * _facing;
        if (ahead is > -6 and < SlashReach - 10 && MathF.Abs(dy) < SlashDepth - 4 && _botSlash <= 0)
        {
            // keys off so the claw does not turn into a charge
            BotHold(0, 0);
            Run.BotTap(CMUArcadeKey.Action);
            _botSlash = (_frenzy > 0 ? 0.1f : 0.17f) + Run.Fx() * 0.14f;
            return;
        }

        var mx = MathF.Abs(stand - _x) > 6 ? MathF.Sign(stand - _x) : MathF.Abs(ahead) < 1 || ahead < 0 ? MathF.Sign(dx) : 0;
        var my = _botDodge > 0 ? _botDodgeDir : MathF.Abs(dy) > 4 ? MathF.Sign(dy) : 0;
        BotHold(mx, my);
    }

    private void BotHold(float x, float y)
    {
        Run.Bot(CMUArcadeKey.Right, x > 0);
        Run.Bot(CMUArcadeKey.Left, x < 0);
        Run.Bot(CMUArcadeKey.Down, y > 0);
        Run.Bot(CMUArcadeKey.Up, y < 0);
    }

    /// <summary>The nearest marine still standing on this stretch of street.</summary>
    private Trooper? BotTarget()
    {
        Trooper? best = null;
        var bestDistance = float.MaxValue;
        foreach (var t in _troopers)
        {
            if (!Alive(t) || t.X < _cam - 20 || t.X > _cam + CMUHuggerRun.ScreenWidth + 20)
                continue;

            var distance = MathF.Abs(t.X - _x) + MathF.Abs(t.Y - _y) * 2;
            if (distance >= bestDistance)
                continue;

            best = t;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>How many marines stand in a target's lane, for deciding whether a charge is worth it.</summary>
    private int BotLane(Trooper target)
    {
        var count = 0;
        foreach (var t in _troopers)
        {
            if (Alive(t) && MathF.Abs(t.Y - target.Y) < 14 && MathF.Sign(t.X - _x) == MathF.Sign(target.X - _x))
                count++;
        }

        return count;
    }

    /// <summary>Whether a marine's round is coming down the ravager's lane.</summary>
    private bool BotIncoming()
    {
        for (var i = 0; i < _bulletCount; i++)
        {
            var b = _bullets[i];
            if (b.Passed || MathF.Abs(b.Y - _y) > 9 || b.Vx == 0)
                continue;

            var eta = (_x - b.X) / b.Vx;
            if (eta is > 0 and < 0.45f && Run.Fx() < 0.9f)
                return true;
        }

        return false;
    }

    #endregion

    #region Update

    public override void Update(float dt)
    {
        _time += dt;
        if (_hitStop > 0)
        {
            // the impact frame: everything on the street holds still for a moment
            _hitStop -= dt;
            return;
        }

        UpdateRavager(dt);
        UpdateTroopers(dt);
        UpdateBullets(dt);
        UpdateShells(dt);
        UpdateFires(dt);
        UpdateDebris(dt);
        UpdateApc(dt);
        UpdateBlasts(dt);
        UpdateWaves(dt);
        UpdateCombo(dt);
        UpdateCamera(dt);
        Ambience(dt);
        UpdateFinale(dt);
    }

    private void HitStop(float seconds)
    {
        _hitStop = MathF.Max(_hitStop, seconds);
    }

    private static float MoveTowards(float value, float target, float step)
    {
        return value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);
    }

    #endregion

    #region The ravager

    private void UpdateRavager(float dt)
    {
        _timer += dt;
        _chargeMeter = MathF.Min(100, _chargeMeter + ChargeRegen * dt);
        _hurt -= dt;
        _hissCool -= dt;
        _screamCool -= dt;
        _chain -= dt;
        if (_frenzy > 0)
        {
            _frenzy -= dt;
            _rage = MathF.Max(0, 100 * _frenzy / FrenzyTime);
            if (_frenzy <= 0)
                _rage = 0;
        }

        // riot bashes and blasts shove it
        if (_push != 0)
        {
            _x += _push * dt;
            _push = MoveTowards(_push, 0, 700 * dt);
        }

        switch (_pose)
        {
            case Pose.Walk:
                Walk(dt);
                break;
            case Pose.Slash:
                Slashing(dt);
                break;
            case Pose.Windup:
                if (Run.Fx() < dt * 30)
                    Dust(_x - _facing * 20, _y, 1);

                if (_timer >= ChargeWindup)
                    BeginCharge();
                break;
            case Pose.Charge:
                Charging(dt);
                break;
            case Pose.Skid:
                var slow = 1 - Math.Clamp(_timer / SkidTime, 0, 1);
                _x += _chargeDir.X * ChargeSpeed * 0.35f * slow * dt;
                _y += _chargeDir.Y * ChargeSpeed * 0.35f * slow * dt;
                if (Run.Fx() < dt * 40)
                    Dust(_x, _y, 1);

                if (_timer >= SkidTime)
                    SetPose(Pose.Walk);
                break;
            case Pose.Dead:
                Dying(dt);
                break;
        }

        Keep();
        UpdateGhosts(dt);
    }

    private void SetPose(Pose pose)
    {
        _pose = pose;
        _timer = 0;
    }

    /// <summary>Keeps the ravager on the street, inside the camera, and out of the APC's hull.</summary>
    private void Keep()
    {
        _y = Math.Clamp(_y, StreetTop, StreetBottom);
        _x = Math.Clamp(_x, _cam + 26, _cam + CMUHuggerRun.ScreenWidth - 26);
        if (_apc == 0 || _y > ApcY + 10 || MathF.Abs(_x - _apcX) > ApcHalf + 22)
            return;

        // pushed out the shortest way: in front of the hull, or off either end
        var front = ApcY + 10 - _y;
        var left = _x - (_apcX - ApcHalf - 22);
        var right = _apcX + ApcHalf + 22 <= _cam + CMUHuggerRun.ScreenWidth - 26 ? _apcX + ApcHalf + 22 - _x : float.MaxValue;
        if (front <= left && front <= right)
            _y = ApcY + 10;
        else if (left < right)
            _x = _apcX - ApcHalf - 22;
        else
            _x = _apcX + ApcHalf + 22;
    }

    private void Walk(float dt)
    {
        var dx = Hostile ? Axis(CMUArcadeKey.Left, CMUArcadeKey.Right) : 0;
        var dy = Hostile ? Axis(CMUArcadeKey.Up, CMUArcadeKey.Down) : 0;
        var speed = _frenzy > 0 ? FrenzySpeed : 1;
        _x += dx * WalkSpeed * speed * dt;
        _y += dy * DepthSpeed * speed * dt;
        if (dx != 0)
            _facing = dx;

        _moving = dx != 0 || dy != 0;
        if (!_moving)
        {
            _step = MathF.Min(_step, 0.08f);
            return;
        }

        _stride += dt * 7.5f * speed;
        _step -= dt;
        if (_step > 0)
            return;

        // heavy feet: a thud and a puff of dust with every step
        _step = StepTime / speed;
        Run.PlayAny(Stomps, -9, 0.85f);
        Dust(_x - _facing * 8, _y, 2);
        Run.Shake(0.6f, 0.08f);
    }

    private void Dust(float x, float y, int count)
    {
        Run.Emit(new Vector2(x, y - 1), count, CMUHuggerRun.Dust.WithAlpha(0.45f), 34, 0.5f, 2.4f, -MathF.PI / 2, 2.4f, -16,
            kind: CMUHuggerRun.Smoke);
    }

    private void Slash()
    {
        if (_pose == Pose.Slash)
        {
            // a press during a slash chains the next one
            if (_struck)
                _queued = true;
            return;
        }

        if (_pose != Pose.Walk)
            return;

        StartSlash(_chain > 0 ? (_lastSlash + 1) % 3 : 0);
    }

    private void StartSlash(int step)
    {
        SetPose(Pose.Slash);
        _slash = step;
        _struck = false;
        _queued = false;
        var dx = Axis(CMUArcadeKey.Left, CMUArcadeKey.Right);
        if (dx != 0)
            _facing = dx;

        if (step == 2)
            Run.PlayAny(TailSwipes, -2);
        else
            Run.PlayAny(Swooshes, -3, step == 0 ? 0.9f : 1.05f);
    }

    private float SlashLength => (_slash == 2 ? SweepTime : SlashTime) * (_frenzy > 0 ? 0.7f : 1);

    private void Slashing(float dt)
    {
        var strike = StrikeAt * (_frenzy > 0 ? 0.7f : 1);
        if (_timer < strike + 0.06f)
            _x += _facing * LungeSpeed * dt;

        if (!_struck && _timer >= strike)
        {
            _struck = true;
            Strike();
        }

        if (_timer < SlashLength)
            return;

        _lastSlash = _slash;
        _chain = _slash == 2 ? 0 : ChainWindow;
        if (_queued && _slash < 2)
        {
            StartSlash(_slash + 1);
            return;
        }

        SetPose(Pose.Walk);
    }

    /// <summary>The claws (or the tail) land: everything in reach in front is hit.</summary>
    private void Strike()
    {
        var sweep = _slash == 2;
        var reach = sweep ? SweepReach : SlashReach;
        var depth = sweep ? SweepDepth : SlashDepth;
        var damage = _frenzy > 0 ? 3 : sweep ? 2 : 1;
        var hits = 0;
        foreach (var t in _troopers)
        {
            if (!Alive(t) || t.Z > 40)
                continue;

            var ahead = (t.X - _x) * _facing;
            if (ahead < -12 || ahead > reach || MathF.Abs(t.Y - _y) > depth)
                continue;

            hits++;
            Hit(t, damage, sweep, false);
        }

        if (_apc is 1 or 2 && MathF.Abs(_x + _facing * reach * 0.5f - _apcX) < ApcHalf + reach * 0.5f && _y - ApcY < 30)
        {
            hits++;
            DamageApc(_frenzy > 0 ? 2 : 1, new Vector2(_apcX - _facing * ApcHalf * 0.8f, _y - 40));
        }

        if (hits == 0)
            return;

        HitStop(sweep ? 0.07f : 0.05f);
        Run.Shake(sweep ? 3 : 2, 0.15f);
    }

    private bool TryCharge(Vector2 direction)
    {
        if (!Hostile || _chargeMeter < 100 && _frenzy <= 0)
            return false;

        if (_pose != Pose.Walk && !(_pose == Pose.Slash && _struck))
            return false;

        _chargeDir = Vector2.Normalize(new Vector2(direction.X, direction.Y * 0.6f));
        if (direction.X != 0)
            _facing = MathF.Sign(direction.X);

        if (_frenzy <= 0)
            _chargeMeter = 0;

        _stamp++;
        SetPose(Pose.Windup);
        Run.Play(Windup, -2);
        return true;
    }

    private void BeginCharge()
    {
        SetPose(Pose.Charge);
        _chargeStep = 0;
        Run.PlayAny(Roars, -3, 1.05f);
        Run.Shake(1.5f, 0.2f);
    }

    private void Charging(float dt)
    {
        _x += _chargeDir.X * ChargeSpeed * dt;
        _y += _chargeDir.Y * ChargeSpeed * dt;
        _chargeStep -= dt;
        if (_chargeStep <= 0)
        {
            _chargeStep = 0.11f;
            Run.PlayAny(ChargeSteps, -4);
            Dust(_x - _facing * 14, _y, 3);
            Run.Shake(1.2f, 0.08f);
        }

        foreach (var t in _troopers)
        {
            if (!t.Active || t.Stamp == _stamp || t.Kind == Sentry && t.State == Act.Dead)
                continue;

            var ahead = (t.X - _x) * _facing;
            if (ahead < -14 || ahead > ChargeReach || MathF.Abs(t.Y - _y) > ChargeDepth || t.Z > 50)
                continue;

            t.Stamp = _stamp;
            if (t.State == Act.Dead)
            {
                Kick(t);
                continue;
            }

            if (t.State == Act.Flying)
                continue;

            Hit(t, 4, true, true);
        }

        for (var i = 0; i < _debrisCount; i++)
        {
            ref var d = ref _debris[i];
            if (!d.Resting || MathF.Abs(d.X - _x - _facing * 20) > 26 || MathF.Abs(d.Y - _y) > ChargeDepth)
                continue;

            d.Resting = false;
            d.Vx = _facing * (160 + Run.Fx() * 80);
            d.Vz = 90 + Run.Fx() * 80;
            d.Spin = _facing * (6 + Run.Fx() * 8);
        }

        // the APC stops the charge dead, and takes a beating for it
        if (_apc is 1 or 2 && _y < ApcY + 14 && MathF.Abs(_x + _facing * 30 - _apcX) < ApcHalf + 8)
        {
            DamageApc(_frenzy > 0 ? 8 : 6, new Vector2(_x + _facing * 30, _y - 40));
            Run.Play(MetalCrash, 0, 0.9f);
            Run.Shake(6, 0.35f);
            HitStop(0.12f);
            _push = -_facing * 220;
            SetPose(Pose.Skid);
            return;
        }

        var edge = _x <= _cam + 27 || _x >= _cam + CMUHuggerRun.ScreenWidth - 27;
        if (_timer >= ChargeTime || edge && _chargeDir.X * (_x - _cam - CMUHuggerRun.ScreenWidth / 2) > 0)
            SetPose(Pose.Skid);
    }

    private void UpdateGhosts(float dt)
    {
        for (var i = 0; i < _ghostCount; i++)
        {
            _ghosts[i].Age += dt;
        }

        var trail = _pose is Pose.Charge or Pose.Skid || _frenzy > 0 && _moving;
        if (!trail)
            return;

        _ghostTimer -= dt;
        if (_ghostTimer > 0)
            return;

        _ghostTimer = 0.035f;
        _ghosts[_ghostNext] = new Ghost { X = _x, Y = _y, Facing = _facing, Tilt = Tilt(), Age = 0 };
        _ghostNext = (_ghostNext + 1) % MaxGhosts;
        _ghostCount = Math.Min(_ghostCount + 1, MaxGhosts);
    }

    /// <summary>Hurts the ravager unless it is charging or frenzied. False when nothing happened.</summary>
    private bool TakeDamage(float amount, Vector2 at, bool burning = false)
    {
        if (!Hostile || Immune)
            return false;

        _hp -= amount;
        _hurt = burning ? 0.06f : 0.12f;
        if (!burning || Run.Fx() < 0.1f)
        {
            Run.Emit(at, burning ? 2 : 6, CMUHuggerRun.Acid, 120, 0.7f, 2, -MathF.PI / 2, 2.4f, 520, _y + 1, 8, stains: true, acid: true);
            if (!burning)
                Run.PlayAny(Flesh, -8, 0.8f);
        }

        if (_hissCool <= 0)
        {
            _hissCool = 0.9f;
            Run.PlayAny(Hisses, -4, 0.8f);
        }

        if (_hp <= 0)
            Die();

        return true;
    }

    private void Die()
    {
        _hp = 0;
        _frenzy = 0;
        SetPose(Pose.Dead);
        _pooled = false;
        var chest = new Vector2(_x, _y - 50);
        Run.Emit(chest, 50, CMUHuggerRun.Acid, 240, 1.2f, 2.6f, -MathF.PI / 2, 2.8f, 600, _y + 1, 12, stains: true, acid: true);
        Run.Emit(chest, 14, CMUHuggerRun.DarkAcid, 150, 1, 3.4f, -MathF.PI / 2, 2, 600, _y + 1, 12, stains: true, acid: true);
        Run.PlayAny(Deaths);
        Run.Flash(CMUHuggerRun.Alarm.WithAlpha(0.3f), 0.4f);
        Run.Shake(5, 0.5f);
        Run.SlowMotion(0.7f);
        Run.Lose(_deadText ?? string.Empty, CMUHuggerRun.Alarm);
    }

    private void Dying(float dt)
    {
        if (_pooled || _timer < 0.55f)
            return;

        _pooled = true;
        Run.Splat(new Vector2(_x, _y + 4), 70, CMUHuggerRun.Acid.WithAlpha(0.9f), acid: true);
        Run.Splat(new Vector2(_x - 24, _y + 8), 30, CMUHuggerRun.Acid.WithAlpha(0.85f), acid: true);
        Run.Play(BodyFall, 0, 0.6f);
        Run.Shake(4, 0.3f);
        Dust(_x, _y, 8);
    }

    #endregion

    #region Kills and combo

    private static bool Alive(Trooper t)
    {
        return t.Active && t.State is not (Act.Flying or Act.Dead);
    }

    /// <summary>A claw, tail or charge connects. Riot shields turn claws from the front; a charge breaks them.</summary>
    private void Hit(Trooper t, int damage, bool heavy, bool charge)
    {
        var shielded = t.Kind == Riot && t.Armed && t.Facing * (_x - t.X) > 0;
        if (shielded && !charge && _frenzy <= 0)
        {
            Blocked(t);
            return;
        }

        if (t.Kind == Riot && t.Armed && (charge || _frenzy > 0))
            BreakShield(t);

        t.Hp -= damage;
        var chest = new Vector2(t.X, t.Y - t.Z - 40);
        if (t.Hp > 0)
        {
            t.State = Act.Stagger;
            t.Timer = 0;
            t.Vx = t.Kind == Sentry ? 0 : _facing * (heavy ? 160 : 110);
            Run.Emit(chest, 12, CMUHuggerRun.Blood, 160, 0.9f, 2.2f, _facing > 0 ? -0.5f : MathF.PI + 0.5f, 1.4f, 520, t.Y + 1, 10,
                stains: true);
            Run.PlayAny(t.Kind == Sentry ? Ricochets : Claws, -2);
            if (t.Kind == Sentry)
                Sparks(chest, 10);
            return;
        }

        if (t.Kind == Sentry)
        {
            Wreck(t);
            return;
        }

        if (charge && MathF.Abs(t.Y - _y) < TrampleDepth && Run.Fx() < 0.45f)
        {
            Trample(t);
            return;
        }

        var force = charge ? 1.5f : heavy ? 1.2f : 0.85f;
        Launch(t, _facing * (115 + Run.Fx() * 60) * force, (175 + Run.Fx() * 80) * force, _facing * (6 + Run.Fx() * 6) * force);
        Run.Emit(chest, charge ? 34 : 24, CMUHuggerRun.Blood, 230, 1.2f, 2.4f, _facing > 0 ? -0.6f : MathF.PI + 0.6f, 1.8f, 520, t.Y + 1, 14,
            stains: true);
        Run.Emit(chest, 8, CMUHuggerRun.DarkBlood, 130, 1, 3.4f, -MathF.PI / 2, 2, 520, t.Y + 1, 14, stains: true);
        if (charge)
        {
            Run.Play(Knockdown, -1);
            Run.PlayAny(Bones, -2);
            Run.Shake(3.5f, 0.2f);
            HitStop(0.06f);
        }
        else
        {
            Run.PlayAny(Claws);
        }

        Killed(t, charge ? 1 : 0);
    }

    private void Blocked(Trooper t)
    {
        var at = new Vector2(t.X + t.Facing * 12, t.Y - 34);
        Sparks(at, 14);
        Run.Play(Block, -1);
        Run.PlayAny(ShieldHits, -4);
        Run.Pop(_blocked ?? string.Empty, at - new Vector2(0, 20), CMUHuggerRun.Bright, 0.6f);
        Run.Shake(1.5f, 0.1f);
        _push = -_facing * 170;
        t.X -= t.Facing * 6;
        t.Cooldown = MathF.Min(t.Cooldown, 0.25f);
        HitStop(0.04f);
    }

    private void BreakShield(Trooper t)
    {
        t.Armed = false;
        var at = new Vector2(t.X + t.Facing * 12, t.Y - 34);
        AddDebris(3, at, t.Y, _facing * (220 + Run.Fx() * 80), 200 + Run.Fx() * 80, _facing * 9);
        Sparks(at, 18);
        Run.Play(ShieldBreak, 0);
        Run.Pop(_shieldBroken ?? string.Empty, at - new Vector2(0, 24), CMUHuggerRun.Amber, 0.8f);
    }

    private void Sparks(Vector2 at, int count)
    {
        Run.Emit(at, count, CMUHuggerRun.TracerCore, 220, 0.3f, 1, -MathF.PI / 2, MathF.Tau, 300, kind: CMUHuggerRun.Spark);
    }

    /// <summary>Sends a dying trooper flying, with its gun and some armour coming off.</summary>
    private void Launch(Trooper t, float vx, float vz, float spin)
    {
        t.State = Act.Flying;
        t.Timer = 0;
        t.Vx = vx;
        t.Vz = vz;
        t.Vy = (t.Y - _y) * 2 + (Run.Fx() - 0.5f) * 30;
        t.Spin = spin;
        t.Struck = false;
        t.Pooled = false;
        ShedGear(t, vx, vz);
    }

    private void ShedGear(Trooper t, float vx, float vz)
    {
        var chest = new Vector2(t.X, t.Y - t.Z - 36);
        if (t.Armed)
        {
            t.Armed = false;
            AddDebris(t.Kind, chest, t.Y, vx * 0.6f + (Run.Fx() - 0.5f) * 80, vz * 0.8f + Run.Fx() * 60, (Run.Fx() - 0.5f) * 24);
        }

        var shards = 2 + Run.Rng.Next(3);
        for (var i = 0; i < shards; i++)
        {
            AddDebris(5, chest, t.Y, vx * 0.5f + (Run.Fx() - 0.5f) * 160, vz * 0.7f + Run.Fx() * 120, (Run.Fx() - 0.5f) * 30);
        }
    }

    /// <summary>Run down under the ravager's feet: flat, in pieces, in a lake of blood.</summary>
    private void Trample(Trooper t)
    {
        t.State = Act.Dead;
        t.Timer = 0;
        t.Z = 0;
        t.Vx = 0;
        t.Rotation = _facing * MathF.PI / 2;
        t.Pooled = true;
        ShedGear(t, _facing * 200, 160);
        var at = new Vector2(t.X, t.Y - 8);
        Run.Emit(at, 46, CMUHuggerRun.Blood, 260, 1.2f, 2.6f, -MathF.PI / 2, 2.6f, 560, t.Y + 1, 16, stains: true);
        Run.Emit(at, 14, CMUHuggerRun.DarkBlood, 160, 1.1f, 4, -MathF.PI / 2, 2.2f, 560, t.Y + 1, 16, stains: true);
        Run.Splat(new Vector2(t.X, t.Y + 2), 64, CMUHuggerRun.Blood.WithAlpha(0.95f));
        Run.Splat(new Vector2(t.X - _facing * 24, t.Y + 4), 36, CMUHuggerRun.DarkBlood.WithAlpha(0.9f));
        Run.Play(Gibbed, 0);
        Run.PlayAny(Bones);
        Run.Shake(4, 0.25f);
        HitStop(0.08f);
        Killed(t, 2);
    }

    /// <summary>Kicks a body already on the ground back into the air.</summary>
    private void Kick(Trooper t)
    {
        t.State = Act.Flying;
        t.Timer = 0;
        t.Vx = _facing * (160 + Run.Fx() * 60);
        t.Vz = 120 + Run.Fx() * 60;
        t.Spin = _facing * 6;
        t.Pooled = true;
        Run.Emit(new Vector2(t.X, t.Y - 10), 10, CMUHuggerRun.Blood, 150, 0.9f, 2.2f, -MathF.PI / 2, 2, 520, t.Y + 1, 12, stains: true);
        Run.Play(Splash, -6);
    }

    /// <summary>Scores a kill: the combo grows, the multiplier with it, rage builds and the ravager feeds.</summary>
    private void Killed(Trooper t, int how)
    {
        t.DiedAt = _time;
        if (t.Kind == Flamer && (how > 0 || Run.Fx() < 0.5f))
            t.Fuse = Fuse;

        if (_screamCool <= 0 && t.Kind != Sentry)
        {
            _screamCool = 0.22f;
            Run.PendScream(0.04f, t.Person.Female, -1);
        }

        Score(KindPoints[t.Kind], new Vector2(t.X, t.Y - 90), how > 0);
    }

    private int Multiplier => Math.Min(MaxMultiplier, 1 + _combo / ComboStep);

    private void Score(int points, Vector2 at, bool charge)
    {
        _kills++;
        _combo++;
        _comboTime = ComboWindow;
        _bestCombo = Math.Max(_bestCombo, _combo);
        var multiplier = Multiplier;
        var total = points * multiplier;
        Run.AddScore(total);
        Run.Pop(multiplier > 1
            ? Loc.GetString("cmu-game-hugger-ravager-points-mult", ("points", points), ("mult", multiplier))
            : Loc.GetString("cmu-game-hugger-ravager-points", ("points", points)), at, multiplier > 2 ? CMUHuggerRun.Amber : CMUHuggerRun.Bright);
        _hp = MathF.Min(MaxHealth, _hp + HealPerKill);
        if (_frenzy <= 0)
        {
            _rage = MathF.Min(100, _rage + RagePerKill + (charge ? RageCharge : 0));
            if (_rage >= 100 && Hostile)
                StartFrenzy();
        }

        var word = _combo switch
        {
            6 => "cmu-game-hugger-ravager-brutal",
            12 => "cmu-game-hugger-ravager-savage",
            20 => "cmu-game-hugger-ravager-carnage",
            30 => "cmu-game-hugger-ravager-unstoppable",
            _ => null,
        };

        if (word == null)
            return;

        _cheer = 1.1f;
        _cheerText = Loc.GetString(word);
    }

    private void StartFrenzy()
    {
        _frenzy = FrenzyTime;
        _rage = 100;
        _cheer = 0;
        Run.Play(FrenzyRoar, 0);
        Run.Flash(CMUHuggerRun.Alarm.WithAlpha(0.35f), 0.45f);
        Run.Shake(5, 0.5f);
        Run.SlowMotion(0.3f);
        Run.Pop(_frenzyText ?? string.Empty, new Vector2(_x, _y - 112), CMUHuggerRun.Alarm, 1.2f);
    }

    private void UpdateCombo(float dt)
    {
        if (_frenzy > 0 || _combo == 0)
            return;

        _comboTime -= dt;
        if (_comboTime <= 0)
            _combo = 0;
    }

    #endregion

    #region Troopers

    private Trooper? Free()
    {
        foreach (var t in _troopers)
        {
            if (!t.Active)
                return t;
        }

        // the street is full of bodies: reuse the longest dead, off screen if we can
        Trooper? oldest = null;
        foreach (var t in _troopers)
        {
            if (t.State != Act.Dead)
                continue;

            var off = t.X < _cam - 40 || t.X > _cam + CMUHuggerRun.ScreenWidth + 40;
            if (oldest == null || off && !OffScreen(oldest) || off == OffScreen(oldest) && t.DiedAt < oldest.DiedAt)
                oldest = t;
        }

        return oldest;
    }

    private bool OffScreen(Trooper t)
    {
        return t.X < _cam - 40 || t.X > _cam + CMUHuggerRun.ScreenWidth + 40;
    }

    private Trooper? AddTrooper(byte kind, float x, float y)
    {
        var t = Free();
        if (t == null)
            return null;

        t.Active = true;
        t.Kind = kind;
        t.State = Act.Move;
        t.X = x;
        t.Y = y;
        t.Z = 0;
        t.Vx = t.Vy = t.Vz = 0;
        t.Rotation = 0;
        t.Spin = 0;
        t.Facing = x > _x ? -1 : 1;
        t.Hp = KindHealth[kind];
        t.Timer = 0;
        t.Cooldown = 0.6f + Run.Fx() * 0.9f;
        t.Gap = 0;
        t.Step = Run.Fx() * 3;
        t.Shots = 0;
        t.Lane = kind switch
        {
            Rifleman => (Run.Fx() - 0.5f) * 18,
            Smartgunner => (Run.Fx() - 0.5f) * 8,
            _ => 0,
        };
        t.Armed = true;
        t.Turn = 0;
        t.Stamp = 0;
        t.Fuse = 0;
        t.Drop = 0;
        t.Struck = false;
        t.Pooled = false;
        if (kind != Sentry)
            Run.Dress(t.Person, CMUHuggerOutfit.Knifeman);

        return t;
    }

    private void SpawnWave(Spawn spawn)
    {
        var y = StreetTop + 4 + Run.Fx() * (StreetBottom - StreetTop - 8);
        if (spawn.Kind == Sentry)
        {
            // air-dropped onto the street, landing with a clang
            var sentry = AddTrooper(Sentry, _cam + (spawn.Side > 0 ? 262 : 58), y);
            if (sentry != null)
                sentry.Z = 170;
            return;
        }

        var x = spawn.Side > 0 ? _cam + CMUHuggerRun.ScreenWidth + 24 + Run.Fx() * 30 : _cam - 24 - Run.Fx() * 30;
        AddTrooper(spawn.Kind, x, y);
    }

    private void UpdateTroopers(float dt)
    {
        foreach (var t in _troopers)
        {
            if (!t.Active)
                continue;

            t.Timer += dt;
            switch (t.State)
            {
                case Act.Move:
                    Think(t, dt);
                    break;
                case Act.Aim:
                    Aiming(t);
                    break;
                case Act.Fire:
                    Firing(t, dt);
                    break;
                case Act.Stagger:
                    t.X += t.Vx * dt;
                    t.Vx = MoveTowards(t.Vx, 0, 500 * dt);
                    if (t.Timer >= StaggerTime)
                    {
                        t.State = Act.Move;
                        t.Timer = 0;
                    }

                    break;
                case Act.Flying:
                    Fly(t, dt);
                    break;
                case Act.Dead:
                    Lie(t, dt);
                    break;
            }

            if (t.Fuse > 0)
            {
                t.Fuse -= dt;
                if (Run.Fx() < dt * 30)
                    Run.Emit(new Vector2(t.X - t.Facing * 6, t.Y - t.Z - 30), 1, CMUHuggerRun.TracerGlow, 60, 0.3f, 1, -MathF.PI / 2, 1.6f, kind: CMUHuggerRun.Spark);

                if (t.Fuse <= 0)
                    Explode(new Vector2(t.X, t.Y), 46, BlastDamage, false);
            }
        }

        Separate(dt);
    }

    /// <summary>Living troopers spread out instead of standing in each other, and give the ravager room.</summary>
    private void Separate(float dt)
    {
        for (var i = 0; i < MaxTroopers; i++)
        {
            var a = _troopers[i];
            if (!Alive(a) || a.Kind == Sentry)
                continue;

            if (MathF.Abs(a.X - _x) < 20 && MathF.Abs(a.Y - _y) < 10 && _pose != Pose.Dead)
                a.X += MathF.Sign(a.X - _x + 0.01f) * 60 * dt;

            if (_apc != 0 && MathF.Abs(a.X - _apcX) < ApcHalf + 6 && a.Y < ApcY + 8)
                a.Y = MoveTowards(a.Y, ApcY + 8, 80 * dt);

            for (var j = i + 1; j < MaxTroopers; j++)
            {
                var b = _troopers[j];
                if (!Alive(b) || b.Kind == Sentry || MathF.Abs(a.X - b.X) > 16 || MathF.Abs(a.Y - b.Y) > 7)
                    continue;

                var side = a.Y < b.Y ? -1 : 1;
                a.Y += side * 20 * dt;
                b.Y -= side * 20 * dt;
            }

            a.Y = Math.Clamp(a.Y, StreetTop, StreetBottom);
        }
    }

    private bool OnScreen(Trooper t)
    {
        return t.X > _cam + 6 && t.X < _cam + CMUHuggerRun.ScreenWidth - 6;
    }

    /// <summary>Walks to its kind's distance from the ravager on its own side and lines up a shot.</summary>
    private void Think(Trooper t, float dt)
    {
        if (t.Kind == Sentry)
        {
            Sentinel(t, dt);
            return;
        }

        var kind = t.Kind;
        var side = t.X >= _x ? 1 : -1;
        var want = KindRange[kind];
        var targetX = Math.Clamp(_x + side * want, _cam + 14, _cam + CMUHuggerRun.ScreenWidth - 14);
        var targetY = Math.Clamp(_y + t.Lane, StreetTop, StreetBottom);
        var speed = KindSpeed[kind];
        if (!Hostile)
        {
            // nobody left to shoot at: they stop and stare
            targetX = t.X;
            targetY = t.Y;
        }
        else if (kind is Rifleman or Smartgunner && MathF.Abs(t.X - _x) < 50)
        {
            speed *= 1.5f;
        }

        var dx = targetX - t.X;
        if (MathF.Abs(dx) > 6)
        {
            t.X += MathF.Sign(dx) * speed * dt;
            t.Step += dt * (4 + speed * 0.12f);
        }

        var dy = targetY - t.Y;
        if (MathF.Abs(dy) > 2)
        {
            t.Y += MathF.Sign(dy) * speed * 0.7f * dt;
            t.Step += dt * 3;
        }

        var toward = _x >= t.X ? 1 : -1;
        if (kind == Riot)
        {
            // the shield turns slowly: get round him and his back is open
            if (toward != t.Facing)
            {
                t.Turn += dt;
                if (t.Turn > 0.55f)
                {
                    t.Facing = toward;
                    t.Turn = 0;
                }
            }
            else
            {
                t.Turn = 0;
            }
        }
        else
        {
            t.Facing = toward;
        }

        t.Cooldown -= dt;
        if (!Hostile || t.Cooldown > 0 || !OnScreen(t) || t.Facing != toward)
            return;

        var distance = MathF.Abs(t.X - _x);
        var inRange = kind is Rifleman or Smartgunner
            ? distance < want + KindTolerance[kind]
            : MathF.Abs(distance - want) < KindTolerance[kind];
        if (!inRange || MathF.Abs(t.Y - _y) > (kind == Riot ? 10 : 12))
            return;

        t.State = Act.Aim;
        t.Timer = 0;
        if (kind == Flamer)
            Run.Play(Ignite, -4);
    }

    /// <summary>A sentry drops in, then turns on the spot and fires down its lane when the ravager is in it.</summary>
    private void Sentinel(Trooper t, float dt)
    {
        if (t.Z > 0)
        {
            t.Vz -= Gravity * dt;
            t.Z = MathF.Max(0, t.Z + t.Vz * dt);
            if (t.Z > 0)
                return;

            Run.Play(MetalCrash, -3, 1.2f);
            Dust(t.X, t.Y, 8);
            Run.Shake(3, 0.2f);
            return;
        }

        t.Facing = _x >= t.X ? 1 : -1;
        t.Cooldown -= dt;
        if (!Hostile || t.Cooldown > 0 || !OnScreen(t) || MathF.Abs(t.Y - _y) > 14)
            return;

        t.State = Act.Aim;
        t.Timer = 0;
    }

    private void Aiming(Trooper t)
    {
        if (!Hostile)
        {
            t.State = Act.Move;
            return;
        }

        if (t.Kind != Riot)
            t.Facing = _x >= t.X ? 1 : -1;

        if (t.Timer < KindAim[t.Kind])
            return;

        t.State = Act.Fire;
        t.Timer = 0;
        t.Gap = 0;
        t.Struck = false;
        t.Drop = 0;
        t.Shots = t.Kind switch
        {
            Rifleman => 3,
            Smartgunner => 14,
            Sentry => 5,
            _ => 0,
        };

        if (t.Kind == Flamer)
            Run.PlayAny(FlamerFire, -2);
        else if (t.Kind == Sentry)
            Run.Play(SentryShot, -2);
    }

    private void Firing(Trooper t, float dt)
    {
        switch (t.Kind)
        {
            case Flamer:
                Flame(t, dt);
                if (t.Timer >= FlameTime || !Hostile)
                    Rest(t, 2.2f + Run.Fx() * 0.8f);
                return;
            case Riot:
                if (!t.Struck && t.Timer >= 0.08f)
                {
                    t.Struck = true;
                    var ahead = (_x - t.X) * t.Facing;
                    if (ahead > 0 && ahead < 46 && MathF.Abs(t.Y - _y) < 16)
                    {
                        Run.PlayAny(Punches, 0, 0.8f);
                        Run.Shake(2, 0.15f);
                        if (TakeDamage(BashDamage, new Vector2(_x - t.Facing * 10, _y - 50)))
                            _push = t.Facing * 260;
                    }
                }

                if (t.Timer >= 0.3f)
                    Rest(t, 1.1f + Run.Fx() * 0.4f);
                return;
        }

        if (t.Kind == Smartgunner)
            t.Y = MoveTowards(t.Y, _y, 18 * dt);

        t.Gap -= dt;
        if (t.Gap > 0)
            return;

        t.Gap = t.Kind switch
        {
            Smartgunner => 0.075f,
            Sentry => 0.09f,
            _ => 0.1f,
        };

        Shoot(t);
        t.Shots--;
        if (t.Shots > 0 && Hostile)
            return;

        Rest(t, t.Kind switch
        {
            Smartgunner => 2 + Run.Fx() * 0.6f,
            Sentry => 1 + Run.Fx() * 0.4f,
            _ => 1.3f + Run.Fx() * 0.8f,
        });
    }

    private void Rest(Trooper t, float cooldown)
    {
        t.State = Act.Move;
        t.Timer = 0;
        t.Cooldown = cooldown;
    }

    private Vector2 Muzzle(Trooper t)
    {
        return new Vector2(t.X + t.Facing * MuzzleX[t.Kind], t.Y - t.Z - MuzzleZ[t.Kind]);
    }

    private void Shoot(Trooper t)
    {
        var muzzle = Muzzle(t);
        AddFlare(muzzle);
        var speed = t.Kind switch
        {
            Smartgunner => SmartSpeed,
            Sentry => SentrySpeed,
            _ => RifleSpeed,
        };

        AddBullet(new Bullet
        {
            X = muzzle.X,
            Y = t.Y,
            Z = MuzzleZ[t.Kind],
            Vx = t.Facing * speed,
            Damage = t.Kind switch
            {
                Smartgunner => SmartDamage,
                Sentry => SentryDamage,
                _ => RifleDamage,
            },
            Kind = t.Kind,
            Owner = Array.IndexOf(_troopers, t),
        });

        switch (t.Kind)
        {
            case Rifleman:
                Run.Play(RifleShot, -3, 0.95f + Run.Fx() * 0.1f);
                break;
            case Smartgunner:
                if (t.Shots % 4 == 2)
                    Run.PlayAny(SmartShots, -2);
                break;
        }

        // brass
        Run.Emit(muzzle - new Vector2(t.Facing * 16, -4), 1, CMUHuggerRun.Amber, 80, 1, 1.6f, -MathF.PI / 2 - t.Facing * 0.6f, 0.6f, 600,
            t.Y + 1, 6);
    }

    /// <summary>The flamer's jet: a roaring cone of fire that burns the ravager and sets the ground alight.</summary>
    private void Flame(Trooper t, float dt)
    {
        var muzzle = Muzzle(t);
        var grow = Math.Clamp(t.Timer / 0.25f, 0, 1);
        var direction = t.Facing > 0 ? 0 : MathF.PI;
        if (Run.Fx() < dt * 60)
        {
            Run.Emit(muzzle, 2, CMUHuggerRun.TracerGlow.WithAlpha(0.8f), 260 * grow + 40, 0.42f, 3, direction, 0.3f, -40, kind: CMUHuggerRun.Smoke);
            Run.Emit(muzzle, 1, CMUHuggerRun.Alarm.WithAlpha(0.7f), 220 * grow + 40, 0.4f, 2.4f, direction, 0.4f, -60, kind: CMUHuggerRun.Smoke);
            Run.Emit(muzzle, 1, CMUHuggerRun.TracerCore, 280 * grow, 0.25f, 1, direction, 0.25f, kind: CMUHuggerRun.Spark);
        }

        t.Drop -= dt;
        if (t.Drop <= 0 && grow >= 1)
        {
            t.Drop = 0.2f;
            AddFire(t.X + t.Facing * (34 + Run.Fx() * (FlameReach - 34)), t.Y + (Run.Fx() - 0.5f) * 10, 10 + Run.Fx() * 6);
        }

        var ahead = (_x - muzzle.X) * t.Facing;
        if (ahead > -HalfWidth && ahead < FlameReach * grow + HalfWidth * 0.5f && MathF.Abs(t.Y - _y) < 16)
            TakeDamage(FlameDps * dt, new Vector2(_x, _y - 50), true);
    }

    private void Fly(Trooper t, float dt)
    {
        t.X += t.Vx * dt;
        t.Y = Math.Clamp(t.Y + t.Vy * dt, StreetTop - 2, StreetBottom + 6);
        t.Z += t.Vz * dt;
        t.Vz -= Gravity * dt;
        t.Rotation += t.Spin * dt;
        if (Run.Fx() < dt * 24)
        {
            Run.Emit(new Vector2(t.X, t.Y - t.Z - 20), 1, CMUHuggerRun.Blood, 40, 0.8f, 2.2f, MathF.PI / 2, 1, 520, t.Y + 1, 6,
                stains: true);
        }

        // flying bodies bowl over whoever they meet
        if (MathF.Abs(t.Vx) > 140 && t.Z < 60)
        {
            foreach (var other in _troopers)
            {
                if (other == t || !Alive(other) || other.Kind == Sentry || MathF.Abs(other.X - t.X) > 14 || MathF.Abs(other.Y - t.Y) > 12)
                    continue;

                Bowled(other, t.Vx * 0.7f);
                t.Vx *= 0.6f;
                break;
            }
        }

        // the edges of the fight throw bodies back, so the carnage stays in view
        if (t.X < _cam + 10 && t.Vx < 0 || t.X > _cam + CMUHuggerRun.ScreenWidth - 10 && t.Vx > 0)
        {
            t.Vx = -t.Vx * 0.3f;
            t.Spin = -t.Spin * 0.5f;
        }

        if (_apc != 0 && MathF.Abs(t.X - _apcX) < ApcHalf && t.Y < ApcY + 8 && t.Z < 56)
        {
            t.X = _apcX + MathF.Sign(t.X - _apcX) * ApcHalf;
            t.Vx = -t.Vx * 0.4f;
            Run.Play(MetalHit, -6, 0.9f);
        }

        if (t.Z > 0 || t.Vz > 0)
            return;

        t.Z = 0;
        var at = new Vector2(t.X, t.Y);
        if (t.Vz < -220)
        {
            // bounces once, hard
            t.Vz = -t.Vz * 0.32f;
            t.Vx *= 0.55f;
            t.Spin *= 0.5f;
            Run.Play(BodyFall, -4, 0.9f + Run.Fx() * 0.2f);
            Dust(t.X, t.Y, 4);
            Run.Splat(at + new Vector2(0, 2), 40, CMUHuggerRun.Blood.WithAlpha(0.85f));
            Run.Shake(1.5f, 0.12f);
            return;
        }

        t.State = Act.Dead;
        t.Timer = 0;
        t.Vx = 0;
        t.Vz = 0;
        t.Spin = 0;
        var turned = t.Rotation - MathF.Tau * MathF.Round(t.Rotation / MathF.Tau);
        t.Rotation = turned >= 0 ? MathF.PI / 2 : -MathF.PI / 2;
        Run.Play(BodyFall, -6, 0.95f + Run.Fx() * 0.1f);
        Dust(t.X, t.Y, 3);
        if (t.Pooled)
            return;

        // the pool spreads out from under the body towards the head
        var head = MathF.Sign(t.Rotation);
        Run.Splat(at + new Vector2(0, 3), 60, CMUHuggerRun.Blood.WithAlpha(0.9f));
        Run.Splat(at + new Vector2(head * 26, 5), 40, CMUHuggerRun.DarkBlood.WithAlpha(0.85f));
    }

    private void Bowled(Trooper t, float vx)
    {
        if (t.Kind == Riot && t.Armed)
            BreakShield(t);

        Launch(t, vx, 160 + Run.Fx() * 80, MathF.Sign(vx) * (6 + Run.Fx() * 4));
        Run.Emit(new Vector2(t.X, t.Y - 40), 16, CMUHuggerRun.Blood, 180, 1, 2.2f, -MathF.PI / 2, 2, 520, t.Y + 1, 12, stains: true);
        Run.PlayAny(Punches, -2);
        Killed(t, 0);
    }

    /// <summary>A body on the street bleeds out into a pool and twitches.</summary>
    private void Lie(Trooper t, float dt)
    {
        if (t.Kind == Sentry || t.Timer > 3)
            return;

        if (!t.Pooled && t.Timer > 0.8f)
        {
            t.Pooled = true;
            Run.Splat(new Vector2(t.X + MathF.Sign(t.Rotation) * 18 + (Run.Fx() - 0.5f) * 10, t.Y + 5), 80, CMUHuggerRun.DarkBlood.WithAlpha(0.85f));
        }

        if (Run.Fx() < dt * 6)
            Run.Emit(new Vector2(t.X, t.Y - 8), 1, CMUHuggerRun.Blood, 30, 0.5f, 2, -MathF.PI / 2, 1.2f, 400, t.Y + 1, 6, stains: true);
    }

    /// <summary>A sentry torn apart: it goes up in a small blast and leaves its wreck.</summary>
    private void Wreck(Trooper t)
    {
        t.State = Act.Dead;
        t.Timer = 0;
        t.Armed = false;
        t.DiedAt = _time;
        Explode(new Vector2(t.X, t.Y), 30, 0, false);
        for (var i = 0; i < 4; i++)
        {
            AddDebris(5, new Vector2(t.X, t.Y - 30), t.Y, (Run.Fx() - 0.5f) * 260, 140 + Run.Fx() * 160, (Run.Fx() - 0.5f) * 30);
        }

        Score(KindPoints[Sentry], new Vector2(t.X, t.Y - 80), false);
    }

    #endregion

    #region Bullets, shells, fire, debris and blasts

    private void AddBullet(Bullet bullet)
    {
        if (_bulletCount < MaxBullets)
            _bullets[_bulletCount++] = bullet;
    }

    private void AddFlare(Vector2 at)
    {
        if (_flareCount < MaxFlashes)
            _flares[_flareCount++] = new Flare { At = at };
    }

    private void UpdateBullets(float dt)
    {
        for (var i = _bulletCount - 1; i >= 0; i--)
        {
            ref var b = ref _bullets[i];
            b.X += b.Vx * dt;
            var ahead = b.Vx > 0 ? b.X - _x : _x - b.X;
            if (!b.Passed && ahead > 0)
            {
                b.Passed = true;
                if (MathF.Abs(b.Y - _y) < BodyDepth && ahead < HalfWidth * 2 && b.Z < BulletReachZ && _pose != Pose.Dead)
                {
                    var at = new Vector2(b.X, b.Y - b.Z);
                    if (Immune && Hostile)
                    {
                        // it glances off the charging carapace
                        Run.Emit(at, 5, CMUHuggerRun.TracerCore, 200, 0.25f, 1, b.Vx > 0 ? MathF.PI : 0, 1.6f, kind: CMUHuggerRun.Spark);
                        Run.PlayAny(Ricochets, -10, 1.2f);
                        RemoveBullet(i);
                        continue;
                    }

                    if (TakeDamage(b.Damage, at))
                    {
                        RemoveBullet(i);
                        continue;
                    }
                }
            }

            // past the ravager, a stray round can find a marine on the far side
            if (b.Passed && Friendly(ref b))
            {
                RemoveBullet(i);
                continue;
            }

            if (b.X < _cam - 40 || b.X > _cam + CMUHuggerRun.ScreenWidth + 40)
                RemoveBullet(i);
        }

        for (var i = _flareCount - 1; i >= 0; i--)
        {
            _flares[i].Age += dt;
            if (_flares[i].Age >= 0.07f)
                _flares[i] = _flares[--_flareCount];
        }
    }

    private void RemoveBullet(int index)
    {
        _bullets[index] = _bullets[--_bulletCount];
    }

    private bool Friendly(ref Bullet b)
    {
        for (var i = 0; i < MaxTroopers; i++)
        {
            var t = _troopers[i];
            if (i == b.Owner || !Alive(t) || t.Kind == Sentry || MathF.Abs(t.X - b.X) > 8 || MathF.Abs(t.Y - b.Y) > 10)
                continue;

            var dir = MathF.Sign(b.Vx);
            Launch(t, dir * 120, 120, dir * 5);
            Run.Emit(new Vector2(t.X, t.Y - b.Z), 18, CMUHuggerRun.Blood, 180, 1, 2.2f, dir > 0 ? 0 : MathF.PI, 1.4f, 520, t.Y + 1, 12, stains: true);
            Run.PlayAny(Flesh, -2);
            Run.Pop(_friendly ?? string.Empty, new Vector2(t.X, t.Y - 104), CMUHuggerRun.Amber, 0.9f);
            Killed(t, 0);
            return true;
        }

        return false;
    }

    private void UpdateShells(float dt)
    {
        for (var i = _shellCount - 1; i >= 0; i--)
        {
            ref var shell = ref _shells[i];
            shell.Age += dt;
            if (shell.Age < ShellTime)
                continue;

            var to = shell.To;
            _shells[i] = _shells[--_shellCount];
            Explode(to, 38, ShellDamage, false);
        }
    }

    private Vector2 ShellAt(Shell shell)
    {
        var k = Math.Clamp(shell.Age / ShellTime, 0, 1);
        return Vector2.Lerp(shell.From, shell.To, k) - new Vector2(0, MathF.Sin(k * MathF.PI) * 70);
    }

    private void AddFire(float x, float y, float size)
    {
        if (_fireCount >= MaxFires)
            return;

        _fires[_fireCount++] = new Fire { X = x, Y = Math.Clamp(y, StreetTop, StreetBottom + 4), Life = FireLife * (0.8f + Run.Fx() * 0.4f), Size = size };
    }

    private void UpdateFires(float dt)
    {
        var burning = false;
        for (var i = _fireCount - 1; i >= 0; i--)
        {
            ref var fire = ref _fires[i];
            fire.Age += dt;
            if (fire.Age >= fire.Life)
            {
                _fires[i] = _fires[--_fireCount];
                continue;
            }

            if (Run.Fx() < dt * 5)
            {
                Run.Emit(new Vector2(fire.X, fire.Y - 6), 1, Color.Black.WithAlpha(0.5f), 18, 1.2f, 3, -MathF.PI / 2, 0.6f, -20,
                    kind: CMUHuggerRun.Smoke);
            }

            if (MathF.Abs(fire.X - _x) < fire.Size + 12 && MathF.Abs(fire.Y - _y) < 8)
                burning = true;
        }

        if (burning)
            TakeDamage(BurnDps * dt, new Vector2(_x, _y - 10), true);
    }

    private void AddDebris(byte kind, Vector2 at, float floor, float vx, float vz, float spin)
    {
        _debris[_debrisNext] = new Debris
        {
            X = at.X,
            Y = floor + (Run.Fx() - 0.5f) * 6,
            Z = floor - at.Y,
            Vx = vx,
            Vy = (Run.Fx() - 0.5f) * 40,
            Vz = vz,
            Spin = spin,
            Rotation = Run.Fx() * MathF.Tau,
            Size = kind >= 5 ? 2 + Run.Fx() * 3 : GearBox,
            Kind = kind,
        };
        _debrisNext = (_debrisNext + 1) % MaxDebris;
        _debrisCount = Math.Min(_debrisCount + 1, MaxDebris);
    }

    private void UpdateDebris(float dt)
    {
        for (var i = 0; i < _debrisCount; i++)
        {
            ref var d = ref _debris[i];
            if (d.Resting)
                continue;

            d.X += d.Vx * dt;
            d.Y = Math.Clamp(d.Y + d.Vy * dt, StreetTop - 4, StreetBottom + 8);
            d.Z += d.Vz * dt;
            d.Vz -= Gravity * dt;
            d.Rotation += d.Spin * dt;
            if (d.Z > 0 || d.Vz > 0)
                continue;

            d.Z = 0;
            if (d.Vz < -140)
            {
                d.Vz = -d.Vz * 0.35f;
                d.Vx *= 0.6f;
                d.Spin *= 0.6f;
                if (d.Kind < 5 && Run.Fx() < 0.6f)
                    Run.Play(MetalHit, -14, 1.5f + Run.Fx() * 0.3f);
                continue;
            }

            d.Resting = true;
            d.Vx = d.Vy = d.Vz = d.Spin = 0;
        }
    }

    /// <summary>
    /// A blast at a ground point: fire, smoke and shrapnel, troopers in the radius thrown, bodies scattered, the
    /// ravager hurt if it is close and not charging. Leaves a scorch mark and a few small fires.
    /// </summary>
    private void Explode(Vector2 at, float radius, float damage, bool big)
    {
        if (_blastCount < MaxBlasts)
            _blasts[_blastCount++] = new Blast { X = at.X, Y = at.Y, Size = radius };

        var air = at - new Vector2(0, 14);
        Run.Emit(air, big ? 40 : 22, CMUHuggerRun.TracerGlow, big ? 260 : 190, 0.6f, 3.4f, -MathF.PI / 2, MathF.Tau, -40, kind: CMUHuggerRun.Smoke);
        Run.Emit(air, big ? 24 : 12, Color.Black.WithAlpha(0.6f), 90, 1.6f, 5, -MathF.PI / 2, 1.8f, -30, kind: CMUHuggerRun.Smoke);
        Run.Emit(air, big ? 30 : 16, CMUHuggerRun.TracerCore, 320, 0.4f, 1, -MathF.PI / 2, MathF.Tau, 400, kind: CMUHuggerRun.Spark);
        Run.PlayAny(Explosions, big ? 0 : -3);
        Run.Shake(big ? 7 : 4.5f, big ? 0.6f : 0.35f);
        Run.Flash(CMUHuggerRun.TracerGlow.WithAlpha(big ? 0.4f : 0.22f), 0.25f);
        _scorches[_scorchNext] = new Scorch { X = at.X, Y = at.Y, Size = radius * 0.8f };
        _scorchNext = (_scorchNext + 1) % MaxScorches;
        _scorchCount = Math.Min(_scorchCount + 1, MaxScorches);
        for (var k = 0; k < (big ? 5 : 2); k++)
        {
            AddFire(at.X + (Run.Fx() - 0.5f) * radius * 1.4f, at.Y + (Run.Fx() - 0.5f) * 14, 8 + Run.Fx() * 6);
        }

        foreach (var t in _troopers)
        {
            if (!t.Active || t.Kind == Sentry || MathF.Abs(t.X - at.X) > radius || MathF.Abs(t.Y - at.Y) > radius * 0.5f)
                continue;

            var away = t.X >= at.X ? 1 : -1;
            if (t.State == Act.Dead)
            {
                t.State = Act.Flying;
                t.Vx = away * (120 + Run.Fx() * 80);
                t.Vz = 200 + Run.Fx() * 100;
                t.Spin = away * 8;
                t.Pooled = true;
                continue;
            }

            if (!Alive(t))
                continue;

            if (t.Kind == Riot && t.Armed)
                BreakShield(t);

            Launch(t, away * (180 + Run.Fx() * 80), 280 + Run.Fx() * 100, away * (8 + Run.Fx() * 6));
            Run.Emit(new Vector2(t.X, t.Y - 30), 20, CMUHuggerRun.Blood, 220, 1.2f, 2.6f, -MathF.PI / 2, 2.4f, 520, t.Y + 1, 14, stains: true);
            Killed(t, 1);
        }

        if (damage > 0 && MathF.Abs(_x - at.X) < radius + 10 && MathF.Abs(_y - at.Y) < radius * 0.5f + 6
            && TakeDamage(damage, new Vector2(_x, _y - 50)))
            _push = (_x >= at.X ? 1 : -1) * 240;
    }

    private void UpdateBlasts(float dt)
    {
        for (var i = _blastCount - 1; i >= 0; i--)
        {
            _blasts[i].Age += dt;
            if (_blasts[i].Age >= 0.45f)
                _blasts[i] = _blasts[--_blastCount];
        }
    }

    #endregion

    #region The APC

    private void ArriveApc()
    {
        _apc = 1;
        _apcX = _cam + CMUHuggerRun.ScreenWidth + ApcHalf + 30;
        _apcHp = ApcHealth;
        _apcTimer = 0;
        _apcDoor = 0;
        _apcDropTimer = 1.5f;
        _apcDrops = 0;
        _apcFire = 2;
        _apcAim = -1;
        _apcHurt = 0;
        _apcRecoil = 0;
        _apcBooms = 0;
        Run.Play(Engine, -2);
    }

    private float ApcPark => _cam + 226;

    private void UpdateApc(float dt)
    {
        if (_apc == 0)
            return;

        _apcTimer += dt;
        _apcHurt -= dt;
        _apcRecoil = MathF.Max(0, _apcRecoil - dt * 4);
        if (_apc == 1)
        {
            var from = _cam + CMUHuggerRun.ScreenWidth + ApcHalf + 30;
            var k = Math.Clamp(_apcTimer / ApcDriveTime, 0, 1);
            var x = from + (ApcPark - from) * (1 - (1 - k) * (1 - k));
            _apcWheel -= (_apcX - x) / 11;
            _apcX = x;
            if (Run.Fx() < dt * 20)
                Dust(_apcX + ApcHalf, ApcY, 2);

            if (k >= 1)
            {
                _apc = 2;
                _apcTimer = 0;
                Run.Play(Hydraulics, -4);
            }

            return;
        }

        if (_apc == 3)
        {
            // the wreck burns, and goes up again a couple of times
            if (_apcBooms < 3)
            {
                _apcBoomTimer -= dt;
                if (_apcBoomTimer <= 0)
                {
                    _apcBooms++;
                    _apcBoomTimer = 0.45f;
                    Explode(new Vector2(_apcX + (Run.Fx() - 0.5f) * ApcHalf * 1.4f, ApcY + 2), 40, 0, false);
                }
            }

            if (Run.Fx() < dt * 14)
            {
                Run.Emit(new Vector2(_apcX + (Run.Fx() - 0.5f) * ApcHalf * 1.6f, ApcY - 50), 1, Color.Black.WithAlpha(0.6f), 24, 2, 5,
                    -MathF.PI / 2, 0.5f, -14, kind: CMUHuggerRun.Smoke);
            }

            return;
        }

        if (_apcHp < ApcHealth / 2 && Run.Fx() < dt * 8)
        {
            Run.Emit(new Vector2(_apcX + (Run.Fx() - 0.5f) * ApcHalf, ApcY - 52), 1, Color.Black.WithAlpha(0.5f), 20, 1.6f, 4,
                -MathF.PI / 2, 0.5f, -14, kind: CMUHuggerRun.Smoke);
        }

        if (!Hostile)
            return;

        // the rear door drops a marine at a time
        _apcDropTimer -= dt;
        if (_apcDoor > 0 || _apcDropTimer <= 0 && _apcDrops < ApcDrops && LivingCount() < 9)
        {
            var before = _apcDoor;
            _apcDoor += dt;
            if (before < 0.4f && _apcDoor >= 0.4f)
            {
                _apcDrops++;
                Run.Play(Hydraulics, -8, 1.2f);
                var kind = _apcDrops % 3 == 0 ? Smartgunner : _apcDrops % 3 == 1 ? Rifleman : Flamer;
                var t = AddTrooper(kind, _apcX - 2, ApcY + 12);
                if (t != null)
                    t.Cooldown = 1.2f;
            }

            if (_apcDoor >= 1)
            {
                _apcDoor = 0;
                _apcDropTimer = ApcDropEvery;
            }
        }

        // the turret lobs a round onto wherever the ravager stands, the impact marked on the ground
        if (_apcAim >= 0)
        {
            _apcAim += dt;
            if (_apcAim < 0.7f)
                return;

            _apcAim = -1;
            FireCannon();
            if (_apcHp < ApcHealth / 4)
                _apcFire = 0.6f;
            return;
        }

        _apcFire -= dt;
        if (_apcFire <= 0)
        {
            _apcFire = ApcFireEvery;
            _apcAim = 0;
        }
    }

    private int LivingCount()
    {
        var count = 0;
        foreach (var t in _troopers)
        {
            if (Alive(t))
                count++;
        }

        return count;
    }

    /// <summary>The APC sprite's centre, rocking as it drives and kicking back as it fires.</summary>
    private Vector2 ApcCentre()
    {
        var rock = _apc == 1 ? MathF.Sin(_apcTimer * 30) * 0.8f : 0;
        return new Vector2(_apcX + _apcRecoil * 3, ApcY - 48 * ApcScale + rock);
    }

    /// <summary>The tip of the turret's twin barrels, which face down the street.</summary>
    private Vector2 BarrelTip()
    {
        return ApcCentre() + new Vector2(12, -27) * ApcScale;
    }

    private void FireCannon()
    {
        if (_shellCount >= MaxShells)
            return;

        var tip = BarrelTip();
        _shells[_shellCount++] = new Shell { From = tip, To = new Vector2(_x, _y) };
        _apcRecoil = 1;
        AddFlare(tip);
        Run.Play(Cannon, -1);
        Run.Shake(2, 0.15f);
        Run.Emit(tip, 6, Color.Black.WithAlpha(0.5f), 40, 1, 4, -MathF.PI / 2, 1.2f, -20, kind: CMUHuggerRun.Smoke);
    }

    private void DamageApc(int damage, Vector2 at)
    {
        if (_apc is not (1 or 2))
            return;

        _apcHp -= damage;
        _apcHurt = 0.12f;
        Sparks(at, 16);
        Run.Play(MetalHit, -1, 0.8f + Run.Fx() * 0.2f);
        AddDebris(6, at, ApcY + 4, _facing * (80 + Run.Fx() * 120), 140 + Run.Fx() * 100, (Run.Fx() - 0.5f) * 20);
        if (_apcHp > 0)
            return;

        // the hull splits open: a big blast, a slow moment, and the heavy squad's ride is scrap
        _apc = 3;
        _apcBooms = 0;
        _apcBoomTimer = 0.4f;
        _apcDoor = 0;
        _shellCount = 0;
        Explode(new Vector2(_apcX, ApcY + 4), 70, 0, true);
        Run.Play(BigBoom, 0);
        Run.SlowMotion(0.5f);
        Run.Flash(Color.White.WithAlpha(0.5f), 0.4f);
        for (var i = 0; i < 6; i++)
        {
            AddDebris(6, new Vector2(_apcX + (Run.Fx() - 0.5f) * ApcHalf, ApcY - 40), ApcY + 6, (Run.Fx() - 0.5f) * 300, 200 + Run.Fx() * 200,
                (Run.Fx() - 0.5f) * 30);
        }

        _kills++;
        _combo++;
        _comboTime = ComboWindow;
        _bestCombo = Math.Max(_bestCombo, _combo);
        Run.Bonus(ApcBonus, new Vector2(_apcX, ApcY - 90), CMUHuggerRun.Acid);
    }

    #endregion

    #region Waves, camera, ambience

    private void StartWave(int wave)
    {
        _wave = wave;
        _waveTime = 0;
        _spawned = 0;
        _waveState = WaveState.Fight;
        _banner = wave == 0 ? 0 : 1.6f;
        _waveText = wave == Waves.Length - 1 ? _final ?? string.Empty : Loc.GetString("cmu-game-hugger-ravager-wave", ("wave", wave + 1));
        _waveCount = Loc.GetString("cmu-game-hugger-ravager-wave-count", ("wave", wave + 1), ("waves", Waves.Length));
        if (wave > 0)
            Run.Play(Radio, -8);
    }

    private bool AnyHostile()
    {
        if (_apc is 1 or 2)
            return true;

        foreach (var t in _troopers)
        {
            if (Alive(t))
                return true;
        }

        return false;
    }

    private void UpdateWaves(float dt)
    {
        if (_banner > 0)
            _banner -= dt;

        if (_cheer > 0)
            _cheer -= dt;

        if (!Hostile || Run.Attract)
            return;

        var wave = Waves[_wave];
        switch (_waveState)
        {
            case WaveState.Fight:
                _waveTime += dt;
                while (_spawned < wave.Length && wave[_spawned].At <= _waveTime)
                {
                    SpawnWave(wave[_spawned++]);
                }

                if (_wave == Waves.Length - 1 && _apc == 0)
                    ArriveApc();

                if (_spawned < wave.Length || AnyHostile() || _waveTime < 1)
                    return;

                Run.Bonus(WaveBonus * (_wave + 1), new Vector2(_x, _y - 120), CMUHuggerRun.Acid);
                if (_wave == Waves.Length - 1)
                {
                    StartFinale();
                    return;
                }

                _waveState = WaveState.Advance;
                break;
            case WaveState.Advance:
                if (_cam >= ArenaX(_wave + 1) - 0.5f)
                    StartWave(_wave + 1);
                break;
        }
    }

    private void UpdateCamera(float dt)
    {
        if (_waveState == WaveState.Advance)
        {
            // the street scrolls on only forwards, as far as the next fight
            var target = Math.Clamp(_x - 130, _cam, ArenaX(_wave + 1));
            _cam = MoveTowards(_cam, target, 200 * dt);
        }

        Run.Camera = new Vector2(_cam, 0);
    }

    private void Ambience(float dt)
    {
        _ambient -= dt;
        if (Run.Fx() < dt * 6)
        {
            // embers off the burning colony
            Run.Emit(new Vector2(_cam + Run.Fx() * CMUHuggerRun.ScreenWidth, 118 + Run.Fx() * 10), 1, CMUHuggerRun.TracerGlow.WithAlpha(0.7f), 20,
                2.4f, 1, -MathF.PI / 2, 1, -12, kind: CMUHuggerRun.Spark);
        }

        if (_ambient > 0)
            return;

        _ambient = 4 + Run.Fx() * 5;
        var roll = Run.Fx();
        if (roll < 0.3f)
        {
            Run.Play(DistantRoar, -16, 0.9f + Run.Fx() * 0.2f);
        }
        else if (roll < 0.55f)
        {
            Run.PlayAny(Explosions, -22, 0.8f);
        }
        else
        {
            var shots = 2 + Run.Rng.Next(4);
            for (var i = 0; i < shots; i++)
            {
                Run.Pend(i * 0.1f, RifleShot, -20, 0.75f);
            }
        }
    }

    #endregion

    #region The finale

    private void StartFinale()
    {
        _finale = 0;
        _tallyShown = 0;
        SetPose(Pose.Roar);
        _frenzy = 0;
        _rage = 0;
        _waveState = WaveState.Done;
        _tally[0] = _entryScore;
        _tally[1] = Run.Points - _entryScore;
        _tally[2] = _kills;
        _tally[3] = _bestCombo * ComboBonus;
        _tally[4] = (int) MathF.Ceiling(_hp) * HealthBonus;
        Run.PlayAny(EchoRoars);
        Run.SlowMotion(0.8f);
        Run.Flash(CMUHuggerRun.Acid.WithAlpha(0.3f), 0.5f);
        Run.Shake(4, 0.6f);
    }

    private void UpdateFinale(float dt)
    {
        if (_finale < 0 || Run.Over)
            return;

        _finale += dt;
        var due = Math.Clamp((int) MathF.Floor((_finale - TallyStart) / TallyRow) + 1, 0, TallyRows);
        while (_tallyShown < due)
        {
            var row = _tallyShown++;
            Run.Play(Tick, -4, row == TallyRows - 1 ? 0.8f : 1.1f);
            if (row is 3 or 4)
                Run.AddScore(_tally[row]);

            if (row == TallyRows - 1)
            {
                _tally[row] = Run.Points;
                Run.Shake(2, 0.2f);
            }
        }

        if (_finale >= WinAfter)
            Run.Win(Loc.GetString("cmu-game-hugger-victory"));
    }

    #endregion

    #region Drawing

    private float Tilt()
    {
        switch (_pose)
        {
            case Pose.Walk:
                return _moving ? MathF.Sin(_stride) * 0.03f : MathF.Sin(Run.Clock * 2) * 0.01f;
            case Pose.Slash:
                var strike = StrikeAt * (_frenzy > 0 ? 0.7f : 1);
                if (_timer < strike)
                    return -_facing * 0.07f * (_timer / strike);

                var k = Math.Clamp((_timer - strike) / (SlashLength - strike), 0, 1);
                return _facing * (_slash == 2 ? 0.2f : 0.14f) * (1 - k);
            case Pose.Windup:
                return -_facing * 0.09f;
            case Pose.Charge:
                return _facing * 0.16f;
            case Pose.Skid:
                return -_facing * 0.07f * (1 - Math.Clamp(_timer / SkidTime, 0, 1));
            case Pose.Roar:
                return -_facing * 0.12f * Math.Clamp(_timer / 0.3f, 0, 1) + MathF.Sin(Run.Clock * 40) * 0.015f;
            default:
                return 0;
        }
    }

    private Vector2 RavagerSize()
    {
        var squash = _pose switch
        {
            Pose.Windup => new Vector2(1.06f, 0.92f),
            Pose.Charge => new Vector2(1.08f, 0.94f),
            Pose.Roar => new Vector2(0.97f, 1.07f),
            Pose.Walk when _moving => new Vector2(1, 1 - MathF.Abs(MathF.Sin(_stride)) * 0.03f),
            _ => Vector2.One,
        };

        return squash * RavagerBox;
    }

    public override void DrawBack(DrawingHandleScreen handle)
    {
        DrawSky(handle);
        DrawSkyline(handle, _cam * 0.22f);
        DrawBuildings(handle, _cam * 0.55f);
        DrawStreet(handle);
    }

    private void DrawSky(DrawingHandleScreen handle)
    {
        Run.Fill(handle, 0, CMUHuggerRun.HudHeight, CMUHuggerRun.ScreenWidth, CurbY - CMUHuggerRun.HudHeight, Sky);

        // the colony burns along the horizon
        for (var k = 0; k < 6; k++)
        {
            Run.Fill(handle, 0, 82 + k * 8, CMUHuggerRun.ScreenWidth, 8, CMUHuggerRun.TracerGlow.WithAlpha(0.008f + k * 0.007f));
        }

        // drifting smoke plumes, very far off
        var u = _cam * 0.08f;
        for (var c = (int) MathF.Floor(u / 120) - 1; c * 120 - u < CMUHuggerRun.ScreenWidth + 60; c++)
        {
            var x = c * 120 - u + CMUHuggerRun.Hash01(c, 1) * 60;
            for (var k = 0; k < 5; k++)
            {
                var drift = (Run.Clock * 3 + k * 9) % 45;
                Run.Circle(handle, new Vector2(x + k * 6 + drift * 0.4f, 100 - k * 14 - drift * 0.3f), 10 + k * 4, Color.Black.WithAlpha(0.28f));
            }
        }
    }

    private void DrawSkyline(DrawingHandleScreen handle, float u)
    {
        const float cell = 26;
        const float baseY = 122;
        for (var c = (int) MathF.Floor(u / cell); c * cell - u < CMUHuggerRun.ScreenWidth; c++)
        {
            var x = c * cell - u;
            if (CMUHuggerRun.Hash(c) % 23 == 0)
            {
                // an atmosphere processor, its warning light blinking
                Run.Fill(handle, x + 4, baseY - 96, 18, 96, FarColour);
                Run.Fill(handle, x, baseY - 70, 26, 6, FarColour);
                Run.Fill(handle, x + 4, baseY - 96, 18, 1, Dim.WithAlpha(0.4f));
                if (Run.Clock % 1.4f < 0.7f)
                    Run.Circle(handle, new Vector2(x + 13, baseY - 99), 1.6f, CMUHuggerRun.Alarm);
                continue;
            }

            var height = 14 + CMUHuggerRun.Hash01(c, 1) * 46;
            Run.Fill(handle, x, baseY - height, cell + 0.5f, height, FarColour);
            Run.Fill(handle, x, baseY - height, cell, 1, Dim.WithAlpha(0.25f));
            for (var k = 0; k < 4; k++)
            {
                if (CMUHuggerRun.Hash01(c, k + 7) < 0.55f)
                    continue;

                var wy = baseY - height + 5 + CMUHuggerRun.Hash01(c, k + 20) * (height - 10);
                var lit = CMUHuggerRun.Hash01(c, k + 40) < 0.3f ? CMUHuggerRun.Amber.WithAlpha(0.5f) : Dim.WithAlpha(0.45f);
                Run.Fill(handle, x + 3 + k * 5.5f, wy, 2, 1.5f, lit);
            }
        }
    }

    private void DrawBuildings(DrawingHandleScreen handle, float u)
    {
        const float cell = 70;
        for (var c = (int) MathF.Floor(u / cell); c * cell - u < CMUHuggerRun.ScreenWidth; c++)
        {
            var x = c * cell - u;
            var hash = CMUHuggerRun.Hash(c + 101);
            if (hash % 5 == 0)
                continue;

            var height = 34 + CMUHuggerRun.Hash01(c, 51) * 32;
            var width = cell - 8;
            var top = CurbY - height;
            var burning = hash % 4 == 1;
            Run.Fill(handle, x + 4, top, width, height, BuildingColour);
            Run.Fill(handle, x + 4, top, width, 1.2f, Green.WithAlpha(0.35f));
            Run.Fill(handle, x + 4, top, 1, height, Dim.WithAlpha(0.4f));
            Run.Fill(handle, x + 3, top + 6, width + 2, 2, Dim.WithAlpha(0.3f));

            // windows, some lit, some on fire
            for (var row = 0; row < 2; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    var wx = x + 10 + col * 14;
                    var wy = top + 12 + row * 14;
                    if (wy + 8 > CurbY - 14)
                        continue;

                    var roll = CMUHuggerRun.Hash01(c * 8 + row * 4 + col, 3);
                    Color colour;
                    if (burning && roll < 0.5f)
                    {
                        var flicker = 0.55f + 0.45f * MathF.Sin(Run.Clock * (9 + roll * 7) + col);
                        colour = Color.InterpolateBetween(CMUHuggerRun.Alarm, CMUHuggerRun.TracerGlow, flicker).WithAlpha(0.75f);
                    }
                    else
                    {
                        colour = roll < 0.35f ? Dim.WithAlpha(0.7f) : roll < 0.5f ? CMUHuggerRun.Bright.WithAlpha(0.35f) : Color.Black.WithAlpha(0.8f);
                    }

                    Run.Fill(handle, wx, wy, 8, 7, colour);
                }
            }

            // the colony's landing sign on a roof here and there
            if (hash % 7 == 3)
            {
                Run.DrawSprite(handle, Run.Frame(LandingSigns, "laz_sign"), new Vector2(x + 4 + width / 2, top - 26 * 0.9f), new Vector2(64, 64) * 0.9f, 0,
                    PropTint);
            }

            // a door at street level
            Run.Fill(handle, x + width - 12, CurbY - 16, 10, 16, Color.Black.WithAlpha(0.8f));
            Run.Fill(handle, x + width - 12, CurbY - 16, 10, 1, Dim.WithAlpha(0.6f));
            if (!burning)
                continue;

            // flames licking off the roof, with a glow above
            Run.Ellipse(handle, new Vector2(x + 4 + width / 2, top), width * 0.6f, 10, CMUHuggerRun.TracerGlow.WithAlpha(0.08f));
            for (var k = 0; k < 5; k++)
            {
                var fx = x + 10 + k * (width - 12) / 4;
                var lick = 6 + 6 * MathF.Abs(MathF.Sin(Run.Clock * (6 + k) + c * 3 + k));
                Run.Quad(handle, new Vector2(fx - 4, top), new Vector2(fx + 4, top), new Vector2(fx + 1, top - lick), new Vector2(fx - 1, top - lick),
                    CMUHuggerRun.TracerGlow.WithAlpha(0.6f));
                Run.Quad(handle, new Vector2(fx - 2, top), new Vector2(fx + 2, top), new Vector2(fx, top - lick * 0.6f), new Vector2(fx, top - lick * 0.6f),
                    CMUHuggerRun.TracerCore.WithAlpha(0.7f));
            }
        }
    }

    private void DrawStreet(DrawingHandleScreen handle)
    {
        var cam = _cam;
        Run.Fill(handle, 0, CurbY, CMUHuggerRun.ScreenWidth, CMUHuggerRun.ScreenHeight - CurbY, Tarmac);

        // the colony's own asphalt tiles, laid in staggered rows and squashed for the slant of the street
        var cache = IoCManager.Resolve<IResourceCache>();
        _asphalt ??= cache.GetTexture(Asphalt);
        _asphaltWorn ??= cache.GetTexture(AsphaltWorn);
        for (var row = 0; row < 6; row++)
        {
            var shift = row % 2 * 16;
            var y = CurbY + 6 + row * 15 + 7.5f;
            for (var c = (int) MathF.Floor((cam - shift) / 32); c * 32 + shift - cam < CMUHuggerRun.ScreenWidth; c++)
            {
                var tile = CMUHuggerRun.Hash(c * 7 + row) % 5 == 0 ? _asphaltWorn : _asphalt;
                Run.DrawSprite(handle, tile, new Vector2(c * 32 + shift - cam + 16, y), new Vector2(32, 15), 0, TileTint);
            }
        }

        Run.Fill(handle, 0, CurbY, CMUHuggerRun.ScreenWidth, 5, Kerb);
        Run.Fill(handle, 0, CurbY, CMUHuggerRun.ScreenWidth, 1, Green.WithAlpha(0.5f));
        Run.Fill(handle, 0, CurbY + 5, CMUHuggerRun.ScreenWidth, 1, Dim.WithAlpha(0.5f));

        // the seams of the street run off to a point on the horizon
        for (var c = (int) MathF.Floor((cam - 160) / 48); c * 48 - cam < CMUHuggerRun.ScreenWidth + 160; c++)
        {
            var sx = c * 48 - cam;
            Run.Line(handle, new Vector2(sx, CurbY + 6), new Vector2(sx + (sx - CMUHuggerRun.ScreenWidth / 2) * 0.9f, CMUHuggerRun.ScreenHeight),
                Dim.WithAlpha(0.18f));
        }

        Run.Fill(handle, 0, 156, CMUHuggerRun.ScreenWidth, 1, Dim.WithAlpha(0.12f));
        Run.Fill(handle, 0, 196, CMUHuggerRun.ScreenWidth, 1, Dim.WithAlpha(0.12f));

        // a dashed centre line, hazard paint on the kerb, cracks and grates
        for (var c = (int) MathF.Floor(cam / 40); c * 40 - cam < CMUHuggerRun.ScreenWidth; c++)
        {
            var sx = c * 40 - cam;
            Run.Fill(handle, sx, 175, 20, 1.5f, CMUHuggerRun.Amber.WithAlpha(0.28f));
            var hash = CMUHuggerRun.Hash(c + 333);
            if (hash % 3 == 0)
            {
                for (var k = 0; k < 5; k++)
                {
                    var hx = sx + k * 8;
                    Run.Quad(handle, new Vector2(hx, CurbY + 1), new Vector2(hx + 4, CurbY + 1), new Vector2(hx + 1, CurbY + 5), new Vector2(hx - 3, CurbY + 5),
                        CMUHuggerRun.Amber.WithAlpha(0.35f));
                }
            }

            if (hash % 7 == 2)
            {
                var cy = 150 + CMUHuggerRun.Hash01(c, 9) * 50;
                Run.Line(handle, new Vector2(sx + 5, cy), new Vector2(sx + 14, cy + 3), Color.Black.WithAlpha(0.7f));
                Run.Line(handle, new Vector2(sx + 14, cy + 3), new Vector2(sx + 22, cy + 1), Color.Black.WithAlpha(0.7f));
            }

            if (hash % 11 == 5)
            {
                var gy = 160 + CMUHuggerRun.Hash01(c, 13) * 36;
                Run.Fill(handle, sx + 8, gy, 18, 6, Color.Black.WithAlpha(0.6f));
                for (var k = 0; k < 4; k++)
                {
                    Run.Fill(handle, sx + 9 + k * 4.5f, gy + 1, 1.5f, 4, Dim.WithAlpha(0.4f));
                }
            }
        }

        DrawKerb(handle, cam);

        // floodlight towers along the kerb, a few of them flickering
        for (var c = (int) MathF.Floor((cam - 40) / 192); c * 192 + 40 - cam < CMUHuggerRun.ScreenWidth + 40; c++)
        {
            var sx = c * 192 + 40 - cam;
            var on = CMUHuggerRun.Hash(c + 77) % 4 != 0 || MathF.Sin(Run.Clock * 13 + c) + MathF.Sin(Run.Clock * 7.1f + c * 2) > -0.4f;
            if (on)
            {
                Run.Quad(handle, new Vector2(sx - 6, 64), new Vector2(sx + 6, 64), new Vector2(sx + 40, CurbY + 40), new Vector2(sx - 40, CurbY + 40),
                    Green.WithAlpha(0.035f));
                Run.Ellipse(handle, new Vector2(sx, CurbY + 30), 42, 9, Green.WithAlpha(0.045f));
            }

            Run.DrawSprite(handle, Run.Frame(Floodlight, on ? "illuminator_on" : "illuminator"), new Vector2(sx, CurbY + 3 - 43 * 0.9f),
                new Vector2(32 * 0.9f, 96 * 0.9f), 0, PropTint);
        }

        for (var i = 0; i < _scorchCount; i++)
        {
            var scorch = _scorches[i];
            var at = new Vector2(scorch.X - cam, scorch.Y + 2);
            Run.Ellipse(handle, at, scorch.Size, scorch.Size * 0.3f, Color.Black.WithAlpha(0.5f));
            Run.Ellipse(handle, at, scorch.Size * 0.6f, scorch.Size * 0.18f, Color.Black.WithAlpha(0.4f));
        }
    }

    /// <summary>
    /// What the marines left along the kerb: sandbag walls, barbed wire, ammo crates, barrels and burnt-out trucks,
    /// all the game's own sprites, one pick per stretch of street.
    /// </summary>
    private void DrawKerb(DrawingHandleScreen handle, float cam)
    {
        const float scale = 1.4f;
        const float box = 32 * scale;
        var size = new Vector2(box, box);
        var ground = CurbY + 4;
        for (var c = (int) MathF.Floor((cam - 120) / 150); c * 150 - cam < CMUHuggerRun.ScreenWidth + 60; c++)
        {
            var sx = c * 150 + 60 - cam;
            var hash = CMUHuggerRun.Hash(c + 909);
            switch (hash % 6)
            {
                case 0:
                    // a sandbag wall, two courses high
                    for (var k = 0; k < 3; k++)
                    {
                        Run.DrawSprite(handle, Run.Frame(Barricades, "sandbag"), new Vector2(sx + k * box, ground - 14 * scale), size, 0, PropTint);
                    }

                    for (var k = 0; k < 2; k++)
                    {
                        Run.DrawSprite(handle, Run.Frame(Barricades, "sandbag"), new Vector2(sx + box / 2 + k * box, ground - 14 * scale - 12), size, 0,
                            PropTint);
                    }

                    break;
                case 1:
                    Run.DrawSprite(handle, Run.Frame(Barricades, "sandbag_wire"), new Vector2(sx, ground - 14 * scale), size, 0, PropTint);
                    for (var k = 1; k < 3; k++)
                    {
                        Run.DrawSprite(handle, Run.Frame(Razorwire, "barbed_wire"), new Vector2(sx + k * box, ground - 9 * scale), size, 0, PropTint);
                    }

                    break;
                case 2:
                    // a crate stack with a barrel beside it
                    Run.DrawSprite(handle, Run.Frame(AmmoCrate, "closed"), new Vector2(sx, ground - 10 * scale), size, 0, PropTint);
                    Run.DrawSprite(handle, Run.Frame(BasicCrate, "closed"), new Vector2(sx + 4, ground - 10 * scale - 18), size, 0, PropTint);
                    Run.DrawSprite(handle, Run.Frame(Barrels, "barrel_red"), new Vector2(sx + 34, ground - 14 * scale), size, 0, PropTint);
                    break;
                case 3:
                    // a truck caught in the fighting, still smouldering
                    var truck = new Vector2(sx + 30, ground - 29 * 1.3f);
                    Run.DrawSprite(handle, Run.Frame(Trucks, "truck_damaged", hash % 2 == 0 ? RsiDirection.East : RsiDirection.West), truck,
                        new Vector2(64, 64) * 1.3f, 0, PropTint);
                    for (var k = 0; k < 3; k++)
                    {
                        var fx = truck.X - 14 + k * 14;
                        var top = truck.Y - 18;
                        var lick = 5 + 5 * MathF.Abs(MathF.Sin(Run.Clock * (8 + k) + c + k));
                        Run.Quad(handle, new Vector2(fx - 4, top), new Vector2(fx + 4, top), new Vector2(fx + 1, top - lick), new Vector2(fx - 1, top - lick),
                            CMUHuggerRun.TracerGlow.WithAlpha(0.6f));
                    }

                    break;
                case 4:
                    Run.DrawSprite(handle, Run.Frame(Barrels, "barrel_yellow"), new Vector2(sx, ground - 14 * 1.2f), new Vector2(38, 38), 0, PropTint);
                    Run.DrawSprite(handle, Run.Frame(Barrels, "barrel_blue"), new Vector2(sx + 20, ground - 14 * 1.2f + 2), new Vector2(38, 38), 0,
                        PropTint);
                    break;
            }
        }
    }

    public override void DrawFront(DrawingHandleScreen handle)
    {
        DrawShadows(handle);
        DrawFires(handle);
        DrawDebris(handle, true);

        // everything standing on the street, back to front
        var count = 0;
        _order[count] = -1;
        _depths[count++] = _y;
        if (_apc != 0)
        {
            _order[count] = -2;
            _depths[count++] = ApcY;
        }

        for (var i = 0; i < MaxTroopers; i++)
        {
            var t = _troopers[i];
            if (!t.Active || t.X < _cam - 80 || t.X > _cam + CMUHuggerRun.ScreenWidth + 80)
                continue;

            _order[count] = i;
            _depths[count++] = t.State == Act.Dead ? t.Y - 3 : t.Y;
        }

        for (var i = 1; i < count; i++)
        {
            var index = _order[i];
            var depth = _depths[i];
            var j = i - 1;
            while (j >= 0 && _depths[j] > depth)
            {
                _order[j + 1] = _order[j];
                _depths[j + 1] = _depths[j];
                j--;
            }

            _order[j + 1] = index;
            _depths[j + 1] = depth;
        }

        for (var i = 0; i < count; i++)
        {
            switch (_order[i])
            {
                case -1:
                    DrawRavager(handle);
                    break;
                case -2:
                    DrawApc(handle);
                    break;
                default:
                    DrawTrooper(handle, _troopers[_order[i]]);
                    break;
            }
        }

        DrawDebris(handle, false);
        DrawFire(handle);
        DrawBlasts(handle);
        DrawClaws(handle);
    }

    private void DrawShadows(DrawingHandleScreen handle)
    {
        var shade = Color.Black.WithAlpha(0.45f);
        if (_pose != Pose.Dead)
            Run.Ellipse(handle, new Vector2(_x - _cam, _y + 1), 30, 5, shade);

        foreach (var t in _troopers)
        {
            if (!t.Active)
                continue;

            var lift = 1 - MathF.Min(t.Z, 120) / 160;
            var lying = MathF.Abs(MathF.Sin(t.Rotation));
            Run.Ellipse(handle, new Vector2(t.X - _cam, t.Y + 1), (11 + lying * 16) * lift, 3 * lift, shade);
        }

        if (_apc != 0)
            Run.Ellipse(handle, new Vector2(_apcX - _cam, ApcY + 1), ApcHalf + 16, 6, shade);
    }

    private void DrawFires(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _fireCount; i++)
        {
            var fire = _fires[i];
            var fade = Math.Clamp((fire.Life - fire.Age) / 0.6f, 0, 1) * Math.Clamp(fire.Age / 0.15f, 0, 1);
            var at = new Vector2(fire.X - _cam, fire.Y);
            Run.Ellipse(handle, at, fire.Size * 1.4f, fire.Size * 0.4f, CMUHuggerRun.TracerGlow.WithAlpha(0.14f * fade));
            for (var k = 0; k < 3; k++)
            {
                var fx = at.X + (k - 1) * fire.Size * 0.55f;
                var lick = fire.Size * (0.8f + 0.6f * MathF.Abs(MathF.Sin(Run.Clock * (11 + k * 3) + i * 1.7f + k))) * fade;
                var half = fire.Size * 0.32f;
                Run.Quad(handle, new Vector2(fx - half, at.Y), new Vector2(fx + half, at.Y), new Vector2(fx + 1, at.Y - lick), new Vector2(fx - 1, at.Y - lick),
                    CMUHuggerRun.Alarm.WithAlpha(0.55f * fade));
                Run.Quad(handle, new Vector2(fx - half * 0.6f, at.Y), new Vector2(fx + half * 0.6f, at.Y), new Vector2(fx, at.Y - lick * 0.7f),
                    new Vector2(fx, at.Y - lick * 0.7f), CMUHuggerRun.TracerGlow.WithAlpha(0.75f * fade));
            }
        }
    }

    private void DrawDebris(DrawingHandleScreen handle, bool resting)
    {
        for (var i = 0; i < _debrisCount; i++)
        {
            var d = _debris[i];
            if (d.Resting != resting)
                continue;

            var at = new Vector2(d.X - _cam, d.Y - d.Z - (d.Kind >= 5 ? 1 : 3));
            if (at.X < -40 || at.X > CMUHuggerRun.ScreenWidth + 40)
                continue;

            if (d.Kind >= 5)
            {
                var size = d.Kind == 6 ? d.Size * 2 : d.Size;
                var (sin, cos) = MathF.SinCos(d.Rotation);
                var a = new Vector2(cos, sin) * size;
                var b = new Vector2(-sin, cos) * size * 0.6f;
                Run.Quad(handle, at - a - b, at + a - b, at + a + b, at - a + b, d.Kind == 6 ? Hull : Shard);
                continue;
            }

            Run.DrawSprite(handle, GearTexture(d.Kind), at, new Vector2(d.Size, d.Size), d.Rotation, CMUHuggerRun.Phosphor);
        }
    }

    private Texture GearTexture(byte kind)
    {
        return kind switch
        {
            Smartgunner => Run.Frame(SmartGun, "Icon"),
            Flamer => Run.Frame(M34, "icon"),
            Riot => Run.Frame(Shields, "riot-icon"),
            Sentry => Run.Frame(SentryRsi, "normal_uac_sentry_destroyed"),
            _ => Run.Frame(M41, "icon"),
        };
    }

    private void DrawRavager(DrawingHandleScreen handle)
    {
        var sx = _x - _cam;
        var direction = _facing > 0 ? RsiDirection.East : RsiDirection.West;
        if (_pose == Pose.Dead)
        {
            // staggers, goes down in a heap
            var k = Math.Clamp(_timer / 0.55f, 0, 1);
            if (k < 1)
            {
                var crit = Run.Frame(CMUHuggerRun.Ravager, "crit");
                Run.DrawSprite(handle, crit, new Vector2(sx - _facing * 10 * k, _y - RavagerBox / 2 + 10 * k), new Vector2(RavagerBox, RavagerBox),
                    -_facing * 0.5f * k, CMUHuggerRun.Phosphor);
                return;
            }

            Run.DrawSprite(handle, Run.Frame(CMUHuggerRun.Ravager, "dead"), new Vector2(sx - _facing * 10, _y - RavagerBox * 0.42f),
                new Vector2(RavagerBox, RavagerBox), 0, CMUHuggerRun.Phosphor);
            return;
        }

        var texture = Run.Frame(CMUHuggerRun.Ravager, "alive", direction);
        for (var i = 0; i < _ghostCount; i++)
        {
            var ghost = _ghosts[i];
            var fade = 1 - ghost.Age / 0.25f;
            if (fade <= 0)
                continue;

            var tint = (_frenzy > 0 ? FrenzyTint : CMUHuggerRun.Acid).WithAlpha(0.3f * fade);
            Run.DrawSprite(handle, Run.Frame(CMUHuggerRun.Ravager, "alive", ghost.Facing > 0 ? RsiDirection.East : RsiDirection.West),
                new Vector2(ghost.X - _cam, ghost.Y - RavagerBox / 2), new Vector2(RavagerBox, RavagerBox), ghost.Tilt, tint);
        }

        var size = RavagerSize();
        var bob = _pose == Pose.Walk && _moving ? MathF.Abs(MathF.Sin(_stride)) * 2.2f : _pose == Pose.Charge ? MathF.Abs(MathF.Sin(Run.Clock * 30)) * 2 : 0;
        var jitter = _pose == Pose.Windup ? (Run.Fx() - 0.5f) * 2 : 0;
        var centre = new Vector2(sx + jitter, _y - size.Y / 2 - bob);
        var colour = CMUHuggerRun.Phosphor;
        if (_frenzy > 0)
            colour = Color.InterpolateBetween(colour, FrenzyTint, 0.35f + 0.25f * MathF.Sin(Run.Clock * 18));

        if (_hurt > 0)
            colour = Color.InterpolateBetween(colour, HurtTint, 0.55f);

        if (_frenzy > 0)
            Run.Ellipse(handle, centre, size.X * 0.45f, size.Y * 0.5f, CMUHuggerRun.Alarm.WithAlpha(0.08f + 0.05f * MathF.Sin(Run.Clock * 18)));

        Run.DrawSprite(handle, texture, centre, size, Tilt(), colour);
    }

    private void DrawTrooper(DrawingHandleScreen handle, Trooper t)
    {
        var sx = t.X - _cam;
        var direction = t.Facing > 0 ? RsiDirection.East : RsiDirection.West;
        if (t.Kind == Sentry)
        {
            DrawSentry(handle, t, sx, direction);
            return;
        }

        var rotation = t.Rotation;
        var bob = 0f;
        switch (t.State)
        {
            case Act.Move:
                bob = MathF.Abs(MathF.Sin(t.Step)) * 2;
                rotation = t.Kind == Riot ? t.Facing * 0.05f : 0;
                break;
            case Act.Aim:
                rotation = t.Kind == Riot ? -t.Facing * 0.1f * MathF.Min(1, t.Timer / 0.3f) : -t.Facing * 0.03f;
                break;
            case Act.Fire:
                rotation = t.Kind == Riot ? t.Facing * 0.2f : -t.Facing * 0.05f * MathF.Abs(MathF.Sin(t.Timer * 60));
                break;
            case Act.Stagger:
                rotation = -t.Facing * 0.35f * (1 - t.Timer / StaggerTime);
                break;
            case Act.Dead:
                if (t.Timer < 1.5f)
                    rotation += MathF.Sin(t.Timer * 50) * 0.03f * (1 - t.Timer / 1.5f);
                break;
        }

        var lift = HumanScale * (16 - 11 * MathF.Abs(MathF.Sin(rotation)));
        var centre = new Vector2(sx, t.Y - t.Z - lift - bob);
        var box = new Vector2(HumanBox, HumanBox);
        Run.DrawPerson(handle, t.Person, centre, direction, HumanScale, rotation, true);
        if (!t.Armed)
            return;

        switch (t.Kind)
        {
            case Smartgunner:
                Run.DrawSprite(handle, Run.Frame(SmartPack, Backpack, direction), centre, box, rotation, CMUHuggerRun.Phosphor);
                Run.DrawSprite(handle, Run.Frame(SmartGun, Wielded, direction), centre, box, rotation, CMUHuggerRun.Phosphor);
                break;
            case Flamer:
                Run.DrawSprite(handle, Run.Frame(FuelPack, Backpack, direction), centre, box, rotation, CMUHuggerRun.Phosphor);
                Run.DrawSprite(handle, Run.Frame(M34, Wielded, direction), centre, box, rotation, CMUHuggerRun.Phosphor);
                if (t.State is Act.Aim or Act.Fire && Run.Clock % 0.1f < 0.06f)
                {
                    var muzzle = Muzzle(t);
                    Run.Circle(handle, new Vector2(muzzle.X - _cam, muzzle.Y), 1.8f, CMUHuggerRun.TracerGlow);
                }

                break;
            case Riot:
                // the shield, held square in front
                Run.DrawSprite(handle, Run.Frame(Shields, "riot-icon"), centre + new Vector2(t.Facing * 9, 2), box * new Vector2(0.75f, 1),
                    rotation, CMUHuggerRun.Phosphor);
                break;
            default:
                Run.DrawSprite(handle, Run.Frame(M41, Wielded, direction), centre, box, rotation, CMUHuggerRun.Phosphor);
                break;
        }
    }

    private void DrawSentry(DrawingHandleScreen handle, Trooper t, float sx, RsiDirection direction)
    {
        var box = new Vector2(HumanBox, HumanBox);
        if (t.State == Act.Dead)
        {
            Run.DrawSprite(handle, Run.Frame(SentryRsi, "defense_base"), new Vector2(sx, t.Y - HumanScale * 2), box, 0, Color.FromHex("#6A7A6A"));
            Run.DrawSprite(handle, Run.Frame(SentryRsi, "normal_uac_sentry_destroyed"), new Vector2(sx, t.Y - HumanScale * 6), box, 0, CMUHuggerRun.Phosphor);
            return;
        }

        var tint = t.State == Act.Stagger ? HurtTint : CMUHuggerRun.Phosphor;
        Run.DrawSprite(handle, Run.Frame(SentryRsi, "normal_uac_sentry_on", direction), new Vector2(sx, t.Y - t.Z - HumanScale * 10), box, 0, tint);
    }

    private void DrawApc(DrawingHandleScreen handle)
    {
        var wreck = _apc == 3;
        var centre = ApcCentre() - new Vector2(_cam, 0);
        var box = new Vector2(128, 128) * ApcScale;

        // headlights first, so the hull sits over their beams
        if (!wreck)
        {
            var nose = centre + new Vector2(-60, 22) * ApcScale;
            Run.Quad(handle, nose, nose + new Vector2(0, 8), nose + new Vector2(-110, 50), nose + new Vector2(-90, -24), CMUHuggerRun.Bright.WithAlpha(0.04f));
        }

        var tint = _apcHurt > 0 ? Color.InterpolateBetween(CMUHuggerRun.Phosphor, HurtTint, 0.6f) : CMUHuggerRun.Phosphor;
        Run.DrawSprite(handle, Run.Frame(ApcRsi, wreck ? "apc_destroyed" : "apc", RsiDirection.West), centre, box, 0, tint);

        // the side door opens on darkness to let the marines out
        var open = _apcDoor > 0 ? MathF.Sin(MathF.Min(1, _apcDoor / 0.4f) * MathF.PI / 2) * (_apcDoor > 0.7f ? (1 - _apcDoor) / 0.3f : 1) : 0;
        if (open > 0)
            Run.Fill(handle, centre.X - 8 * ApcScale, centre.Y + 14 * ApcScale, 14 * ApcScale, 26 * ApcScale, Color.Black.WithAlpha(0.85f * open));

        var tip = BarrelTip() - new Vector2(_cam, 0);
        if (_apcAim >= 0 && !wreck)
            Run.Circle(handle, tip, 2 + _apcAim * 3, CMUHuggerRun.Laser.WithAlpha(0.3f + 0.3f * MathF.Sin(Run.Clock * 40)));

        if (!wreck && _apcHp >= ApcHealth / 4)
            return;

        // burning
        var roof = centre.Y - 36 * ApcScale;
        for (var k = 0; k < 6; k++)
        {
            var fx = centre.X - 70 + k * 26;
            var lick = (wreck ? 12 : 7) + 7 * MathF.Abs(MathF.Sin(Run.Clock * (7 + k) + k * 2));
            Run.Quad(handle, new Vector2(fx - 6, roof), new Vector2(fx + 6, roof), new Vector2(fx + 1, roof - lick), new Vector2(fx - 1, roof - lick),
                CMUHuggerRun.TracerGlow.WithAlpha(0.7f));
            Run.Quad(handle, new Vector2(fx - 3, roof), new Vector2(fx + 3, roof), new Vector2(fx, roof - lick * 0.6f), new Vector2(fx, roof - lick * 0.6f),
                CMUHuggerRun.TracerCore.WithAlpha(0.8f));
        }
    }

    /// <summary>Lane warnings and laser sights, tracers, the APC's shells, muzzle flashes and the flamers' jets.</summary>
    private void DrawFire(DrawingHandleScreen handle)
    {
        foreach (var t in _troopers)
        {
            if (!t.Active || t.State != Act.Aim || t.Kind is Riot)
                continue;

            var muzzle = Muzzle(t);
            var sx = muzzle.X - _cam;
            var aim = MathF.Min(1, t.Timer / KindAim[t.Kind]);
            var flicker = 0.75f + 0.25f * MathF.Sin(Run.Clock * 50 + t.X);
            if (t.Kind == Flamer)
            {
                Run.Fill(handle, t.Facing > 0 ? sx : sx - FlameReach, t.Y - 1, FlameReach, 2, CMUHuggerRun.TracerGlow.WithAlpha(0.15f * aim));
                continue;
            }

            // the lane the burst will come down, painted along the ground, and the sight's beam at gun height
            var reach = 330 * aim;
            var from = t.Facing > 0 ? sx : sx - reach;
            Run.Fill(handle, from, t.Y - 0.5f, reach, 1, CMUHuggerRun.Laser.WithAlpha(0.22f * flicker));
            Run.Fill(handle, from, muzzle.Y - 0.5f, reach, 1, CMUHuggerRun.Laser.WithAlpha((0.25f + 0.25f * aim) * flicker));
            Run.Circle(handle, new Vector2(sx, muzzle.Y), 1.5f + aim, CMUHuggerRun.Laser.WithAlpha(0.6f));
        }

        for (var i = 0; i < _bulletCount; i++)
        {
            var b = _bullets[i];
            var head = new Vector2(b.X - _cam, b.Y - b.Z);
            var tail = head - new Vector2(MathF.Sign(b.Vx) * (b.Kind == Smartgunner ? 10 : 14), 0);
            Run.Beam(handle, tail, head, 4, CMUHuggerRun.TracerGlow.WithAlpha(0.3f));
            Run.Beam(handle, tail, head, 1.6f, CMUHuggerRun.TracerCore);
        }

        for (var i = 0; i < _shellCount; i++)
        {
            var shell = _shells[i];
            var k = shell.Age / ShellTime;
            var to = new Vector2(shell.To.X - _cam, shell.To.Y);
            var ring = 6 + 14 * (1 - k);
            Run.Ellipse(handle, to, ring, ring * 0.3f, CMUHuggerRun.Laser.WithAlpha(0.18f + 0.2f * k));
            Run.Ellipse(handle, to, 3, 1, CMUHuggerRun.Laser.WithAlpha(0.7f));
            var at = ShellAt(shell) - new Vector2(_cam, 0);
            Run.Circle(handle, at, 4, CMUHuggerRun.TracerGlow.WithAlpha(0.4f));
            Run.Circle(handle, at, 2, CMUHuggerRun.TracerCore);
        }

        for (var i = 0; i < _flareCount; i++)
        {
            var flare = _flares[i];
            var k = 1 - flare.Age / 0.07f;
            var at = flare.At - new Vector2(_cam, 0);
            Run.Circle(handle, at, 6 * k + 2, CMUHuggerRun.TracerGlow.WithAlpha(0.5f * k));
            Run.Circle(handle, at, 2.5f * k + 1, CMUHuggerRun.TracerCore.WithAlpha(k));
        }

        foreach (var t in _troopers)
        {
            if (!t.Active || t.Kind != Flamer || t.State != Act.Fire)
                continue;

            var muzzle = Muzzle(t) - new Vector2(_cam, 0);
            var grow = Math.Clamp(t.Timer / 0.25f, 0, 1);
            var flicker = 0.7f + 0.3f * MathF.Sin(Run.Clock * 47 + t.X);
            var end = muzzle + new Vector2(t.Facing * FlameReach * grow, 4);
            Run.Beam(handle, muzzle, end, 9 * flicker, CMUHuggerRun.TracerGlow.WithAlpha(0.22f));
            Run.Beam(handle, muzzle, muzzle + (end - muzzle) * 0.6f, 4, CMUHuggerRun.TracerCore.WithAlpha(0.5f));
        }
    }

    private void DrawBlasts(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _blastCount; i++)
        {
            var blast = _blasts[i];
            var k = blast.Age / 0.45f;
            var at = new Vector2(blast.X - _cam, blast.Y - 12);
            Run.Circle(handle, at, blast.Size * (0.3f + 0.8f * k), CMUHuggerRun.TracerGlow.WithAlpha(0.45f * (1 - k)));
            if (k < 0.4f)
                Run.Circle(handle, at, blast.Size * (0.2f + k), Color.White.WithAlpha(0.8f * (1 - k / 0.4f)));

            Run.Ellipse(handle, new Vector2(at.X, blast.Y), blast.Size * (0.6f + k), blast.Size * 0.2f, CMUHuggerRun.TracerGlow.WithAlpha(0.3f * (1 - k)));
        }
    }

    /// <summary>The claw streaks: three bright arcs swept through the slash, the sweep's one wide and low.</summary>
    private void DrawClaws(DrawingHandleScreen handle)
    {
        if (_pose != Pose.Slash)
            return;

        var strike = StrikeAt * (_frenzy > 0 ? 0.7f : 1);
        var k = (_timer - strike + 0.02f) / 0.16f;
        if (k < 0 || k > 1)
            return;

        var sx = _x - _cam;
        var sweep = _slash == 2;
        var centre = new Vector2(sx + _facing * (sweep ? 10 : 18), _y - (sweep ? 26 : 60));
        var radius = sweep ? 62 : 42;
        float from, to;
        switch (_slash)
        {
            case 0:
                from = -1.3f;
                to = 0.9f;
                break;
            case 1:
                from = 1.0f;
                to = -1.1f;
                break;
            default:
                from = -0.5f;
                to = 0.55f;
                break;
        }

        var colour = _frenzy > 0 ? FrenzyTint : CMUHuggerRun.Bright;
        var reach = MathF.Min(1, k * 2.2f);
        var alpha = 1 - k;
        for (var line = 0; line < 3; line++)
        {
            var r = radius + (line - 1) * 6;
            var previous = Vector2.Zero;
            for (var s = 0; s <= 8; s++)
            {
                var angle = from + (to - from) * reach * s / 8;
                var point = centre + new Vector2(_facing * MathF.Cos(angle) * r, MathF.Sin(angle) * r * (sweep ? 0.35f : 1));
                if (s > 0)
                {
                    var tip = s / 8f;
                    Run.Beam(handle, previous, point, 6 * tip, colour.WithAlpha(0.45f * alpha));
                    Run.Beam(handle, previous, point, 2.4f * tip, Color.White.WithAlpha(alpha * (0.4f + 0.6f * tip)));
                }

                previous = point;
            }
        }
    }

    public override void DrawOverlay(DrawingHandleScreen handle)
    {
        DrawEdges(handle);
        if (_finale >= 0)
        {
            DrawTally(handle);
            return;
        }

        if (Run.Over)
            return;

        DrawBars(handle);
        DrawCombo(handle);
        if (_banner > 0)
        {
            var alpha = Math.Clamp(_banner / 0.4f, 0, 1) * Math.Clamp((1.6f - _banner) / 0.2f, 0, 1);
            Run.CentreText(handle, Run.BigFont, _waveText, CMUHuggerRun.ScreenWidth / 2, 58, CMUHuggerRun.Amber.WithAlpha(alpha));
            if (_wave == Waves.Length - 1)
                Run.CentreText(handle, Run.SmallFont, _heavy ?? string.Empty, CMUHuggerRun.ScreenWidth / 2, 82, CMUHuggerRun.Bright.WithAlpha(alpha));
        }

        if (_cheer > 0 && _cheerText != null)
        {
            var pop = 1 + MathF.Max(0, _cheer - 0.9f) * 2;
            Run.CentreText(handle, Run.BigFont, _cheerText, CMUHuggerRun.ScreenWidth / 2, 40 - (pop - 1) * 6,
                CMUHuggerRun.Amber.WithAlpha(Math.Clamp(_cheer / 0.3f, 0, 1)));
        }

        if (_waveState == WaveState.Advance && Run.Clock % 0.8f < 0.55f)
        {
            Run.Text(handle, Run.BigFont, _go ?? string.Empty, CMUHuggerRun.ScreenWidth - 66, 92, CMUHuggerRun.Bright);
            var tip = new Vector2(CMUHuggerRun.ScreenWidth - 14, 101);
            Run.Quad(handle, tip, tip + new Vector2(-12, -9), tip + new Vector2(-12, 9), tip, CMUHuggerRun.Bright);
        }

        if (_time < 7 && _wave == 0)
        {
            var alpha = Math.Clamp((7 - _time) / 0.5f, 0, 1);
            Run.CentreText(handle, Run.SmallFont, _keys ?? string.Empty, CMUHuggerRun.ScreenWidth / 2, 212, CMUHuggerRun.Bright.WithAlpha(alpha * 0.9f));
        }
    }

    /// <summary>The frenzy burns the screen's edges red; low health pulses them.</summary>
    private void DrawEdges(DrawingHandleScreen handle)
    {
        float strength;
        if (_frenzy > 0)
            strength = 0.16f + 0.08f * MathF.Sin(Run.Clock * 18);
        else if (_hp < 30 && Hostile)
            strength = (0.1f + 0.08f * MathF.Sin(Run.Clock * 7)) * (1 - _hp / 30);
        else
            return;

        var top = CMUHuggerRun.HudHeight;
        for (var k = 0; k < 4; k++)
        {
            var colour = CMUHuggerRun.Alarm.WithAlpha(strength * (1 - k * 0.25f));
            var inset = k * 4;
            Run.Fill(handle, 0, top + inset, CMUHuggerRun.ScreenWidth, 4, colour);
            Run.Fill(handle, 0, CMUHuggerRun.ScreenHeight - inset - 4, CMUHuggerRun.ScreenWidth, 4, colour);
            Run.Fill(handle, inset, top, 4, CMUHuggerRun.ScreenHeight - top, colour);
            Run.Fill(handle, CMUHuggerRun.ScreenWidth - inset - 4, top, 4, CMUHuggerRun.ScreenHeight - top, colour);
        }
    }

    private void DrawBars(DrawingHandleScreen handle)
    {
        // health, with the charge meter under it
        var health = Math.Clamp(_hp / MaxHealth, 0, 1);
        var low = _hp < 30;
        Run.Fill(handle, 6, 20, BarWidth, 6, Color.Black.WithAlpha(0.7f));
        Run.Fill(handle, 6, 20, BarWidth * health, 6, low && Run.Clock % 0.4f < 0.2f ? CMUHuggerRun.Alarm : CMUHuggerRun.Acid);
        Run.Outline(handle, 6, 20, BarWidth, 6, Dim);
        var ready = _chargeMeter >= 100 || _frenzy > 0;
        Run.Fill(handle, 6, 28, BarWidth * _chargeMeter / 100, 2, ready ? CMUHuggerRun.Bright : Dim);
        Run.Text(handle, Run.SignFont, _hpLabel ?? string.Empty, BarWidth + 10, 20, Dim);

        // rage, burning when full
        var rage = Math.Clamp(_rage / 100, 0, 1);
        var hot = _frenzy > 0 || rage > 0.8f;
        var left = CMUHuggerRun.ScreenWidth - 6 - BarWidth;
        Run.Fill(handle, left, 20, BarWidth, 6, Color.Black.WithAlpha(0.7f));
        Run.Fill(handle, left, 20, BarWidth * rage, 6,
            hot ? Color.InterpolateBetween(CMUHuggerRun.Alarm, CMUHuggerRun.TracerGlow, 0.5f + 0.5f * MathF.Sin(Run.Clock * 16)) : CMUHuggerRun.Alarm);
        Run.Outline(handle, left, 20, BarWidth, 6, Dim);

        // the frenzy's name is longer, so it takes the wave count's place while it burns
        if (_frenzy > 0)
        {
            RightText(handle, Run.SignFont, _frenzyText ?? string.Empty, left - 4, 20, CMUHuggerRun.Alarm);
            return;
        }

        RightText(handle, Run.SignFont, _rageLabel ?? string.Empty, left - 4, 20, Dim);
        Run.CentreText(handle, Run.SignFont, _waveCount, CMUHuggerRun.ScreenWidth / 2, 20, Dim);
    }

    private void DrawCombo(DrawingHandleScreen handle)
    {
        if (_combo < 2)
            return;

        if (_combo != _comboShown)
        {
            _comboShown = _combo;
            _comboText = Multiplier > 1
                ? Loc.GetString("cmu-game-hugger-ravager-combo", ("count", _combo), ("mult", Multiplier))
                : Loc.GetString("cmu-game-hugger-ravager-kills", ("count", _combo));
        }

        var colour = Multiplier >= 3 ? CMUHuggerRun.Amber : CMUHuggerRun.Bright;
        RightText(handle, Run.SmallFont, _comboText, CMUHuggerRun.ScreenWidth - 6, 30, colour);
        var left = _frenzy > 0 ? 1 : Math.Clamp(_comboTime / ComboWindow, 0, 1);
        Run.Fill(handle, CMUHuggerRun.ScreenWidth - 6 - 60 * left, 41, 60 * left, 1.5f, colour.WithAlpha(0.8f));
    }

    private void DrawTally(DrawingHandleScreen handle)
    {
        if (!Run.Over)
        {
            var alpha = Math.Clamp(_finale / 0.5f, 0, 1);
            Run.CentreText(handle, Run.BigFont, _purged ?? string.Empty, CMUHuggerRun.ScreenWidth / 2, 66, CMUHuggerRun.Acid.WithAlpha(alpha));
        }

        if (_tallyShown == 0 || _tallyLabels == null)
            return;

        Run.Fill(handle, 52, 110, 216, 6 + TallyRows * 13, Color.Black.WithAlpha(0.65f));
        Run.Outline(handle, 52, 110, 216, 6 + TallyRows * 13, Dim);
        for (var row = 0; row < _tallyShown; row++)
        {
            var y = 114 + row * 13;
            var total = row == TallyRows - 1;
            var colour = total ? CMUHuggerRun.Acid : row is 3 or 4 ? CMUHuggerRun.Amber : CMUHuggerRun.Bright;
            if (total)
                Run.Fill(handle, 58, y - 2, 204, 1, Dim);

            Run.Text(handle, Run.SmallFont, _tallyLabels[row] ?? string.Empty, 60, y, colour);

            // each figure counts up as its row comes in
            var age = _finale - TallyStart - row * TallyRow;
            var shown = (int) (_tally[row] * Math.Clamp(age / 0.4f, 0, 1));
            if (shown != _tallyCached[row] || _tallyValues[row] == null)
            {
                _tallyCached[row] = shown;
                _tallyValues[row] = row is 3 or 4
                    ? Loc.GetString("cmu-game-hugger-ravager-points", ("points", shown.ToString()))
                    : shown.ToString();
            }

            RightText(handle, Run.SmallFont, _tallyValues[row] ?? string.Empty, 260, y, colour);
        }
    }

    private void RightText(DrawingHandleScreen handle, Font font, string text, float right, float y, Color colour)
    {
        // measured at the screen's own scale (which grows when the game is opened full-window), as the framework does
        var scale = Run.UIScale;
        var width = handle.GetDimensions(font, text, scale).X / scale;
        Run.Text(handle, font, text, right - width, y, colour);
    }

    #endregion
}
