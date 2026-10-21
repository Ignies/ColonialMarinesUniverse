using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>What a human in the corridor is doing.</summary>
public enum CMUHuggerHumanState : byte
{
    Walk,
    Stand,
    Alert,
    Flee,
    Aim,
    WindUp,
    Stab,
    Recover,
    Kick,
    Latched,
    Shot,
    Down,
    Brace,
    Pump,
}

/// <summary>A colonist or marine on the corridor floor.</summary>
public sealed class CMUHuggerHuman
{
    public readonly CMUHuggerPerson Person = new();
    public bool Active;
    public CMUHuggerHumanState State;

    /// <summary>Feet position along the floor, in world units.</summary>
    public float X;

    /// <summary>Facing east: running away from the player.</summary>
    public bool East;

    public bool Flees;
    public bool Whistled;
    public bool Shouted;
    public bool Infected;

    /// <summary>A shotgunner's next blast goes high (duck it) rather than low (jump it).</summary>
    public bool High;

    /// <summary>The demo bot's random draw for this human, and whether it has dealt with them.</summary>
    public float BotSeed;

    public bool BotDone;
    public float Walk;
    public float Step;
    public float Timer;
    public float Cooldown;
    public int Shots;

    /// <summary>Which way the body goes down: 1 back to the right, -1 to the left.</summary>
    public float Fall;

    public bool Marine => Person.Outfit != CMUHuggerOutfit.Colonist;
    public bool Rifleman => Person.Outfit == CMUHuggerOutfit.Rifleman;
    public bool Knifeman => Person.Outfit == CMUHuggerOutfit.Knifeman;
}

/// <summary>What a locker trap in the corridor holds.</summary>
public enum CMUHuggerLockerContent : byte
{
    Empty,
    Marine,
    Colonist,
}

/// <summary>
/// The side-scrolling corridor the facehugger and larva stages run along: green phosphor colony scenery with
/// parallax, a player creature at a fixed x that jumps (held for higher) and ducks, tracers fired down three lanes
/// from off screen with laser-sight warnings, and humans (colonists, riflemen who aim, knifemen who stab) who walk
/// in from the right. Subclasses pick the creature, what spawns, and what touching a human means.
/// </summary>
public abstract class CMUHuggerCorridorStage : CMUHuggerStage
{
    #region Layout and tuning

    public const float CeilingY = 40;
    public const float FloorY = 186;
    public const float PlayerX = 64;

    // the wall scrolls with the deck, so hazards on the deck can be lined up with bare wall; the windows give depth
    protected const float WallDepth = 1;
    protected const float OutsideDepth = 0.12f;
    protected const float HillDepth = 0.22f;
    protected const float PanelWidth = 64;

    /// <summary>Humans are 32 px sprites at 3.3: about two thirds of the corridor, feet on the floor.</summary>
    public const float HumanScale = 3.3f;

    // Human hit boxes, in units above the floor and either side of the feet, from the sprite at HumanScale.
    protected const float HeadTop = 96;
    protected const float HeadBottom = 69;
    protected const float HeadHalfWidth = 14;
    protected const float BodyTop = 71;
    protected const float BodyHalfWidth = 11;

    protected const float Warning = 0.38f;
    protected const float BulletSpeed = 330;
    protected const float BulletSpeedPerRun = 0.5f;
    protected const float MarineBulletSpeed = 360;
    protected const float BurstGap = 0.085f;
    protected const float Tracer = 16;

    protected const float AlertRange = 130;
    protected const float MarineAim = 0.45f;
    protected const float MarineReach = 80;
    protected const float MarineBackpedal = 40;
    protected const float KickTime = 0.32f;
    protected const float Struggle = 0.35f;
    protected const float FallTime = 0.5f;

    // A knifeman winds up when the player comes in range, then stabs low in front of him for a moment.
    protected const float StabRange = 100;
    protected const float WindUpTime = 0.32f;
    protected const float StabTime = 0.22f;
    protected const float RecoverTime = 0.7f;
    protected const float StabReachNear = 8;
    protected const float StabReachFar = 64;
    protected const float StabHeight = 34;

    /// <summary>How long the corridor takes to skid to a stop when the run halts, so the scene stays on screen.</summary>
    protected const float StopTime = 0.15f;

    private const int MaxBullets = 32;
    private const int MaxShots = 12;
    private const int MaxFlashes = 8;
    private const int MaxHumans = 4;

    // The player creature, all in units: half its hit box width, its height standing and ducked, and its jump.
    protected abstract float HalfWidth { get; }
    protected abstract float Height { get; }
    protected abstract float DuckHeight { get; }
    protected abstract float JumpVelocity { get; }
    protected virtual float Gravity => 2400;
    protected virtual float HoldGravity => 700;
    protected virtual float MaxHold => 0.33f;
    protected virtual float FastFallGravity => 5000;
    protected virtual float JumpBuffer => 0.1f;

    protected virtual float StartSpeed => 150;
    protected virtual float MaxSpeed => 400;
    protected virtual float Acceleration => 2.6f;

    // Fire from off screen: low is jumped, mid is ducked or jumped high, high passes over a creature on the floor.
    protected abstract float LaneLow { get; }
    protected abstract float LaneMid { get; }
    protected abstract float LaneHigh { get; }

    #endregion

    #region Art and sound

    protected const string RifleShot = "/Audio/Weapons/Guns/Gunshots/m41.ogg";
    protected const string Ricochet = "/Audio/_RMC14/Bullets/bullet_impact1.ogg";
    protected const string Squish = "/Audio/Effects/Footsteps/meatslap.ogg";
    protected const string DistantRoar = "/Audio/_RMC14/Xeno/alien_distantroar_3.ogg";
    protected const string BodyFall = "/Audio/CMU14/PersonalComputers/Games/fall.ogg";
    protected const string KnifeSwing = "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg";

    protected static readonly string[] Flesh =
    {
        "/Audio/_RMC14/Bullets/impact_flesh_1.ogg", "/Audio/_RMC14/Bullets/impact_flesh_2.ogg",
        "/Audio/_RMC14/Bullets/impact_flesh_3.ogg", "/Audio/_RMC14/Bullets/impact_flesh_4.ogg",
    };

    protected static readonly string[] Sizzles =
    {
        "/Audio/_RMC14/Xeno/acid_sizzle1.ogg", "/Audio/_RMC14/Xeno/acid_sizzle2.ogg",
        "/Audio/_RMC14/Xeno/acid_sizzle3.ogg", "/Audio/_RMC14/Xeno/acid_sizzle4.ogg",
    };

    protected static readonly string[] Hisses =
    {
        "/Audio/_RMC14/Xeno/alien_hiss1.ogg", "/Audio/_RMC14/Xeno/alien_hiss2.ogg", "/Audio/_RMC14/Xeno/alien_hiss3.ogg",
    };

    protected static readonly string[] Swooshes =
    {
        "/Audio/CMU14/PersonalComputers/Games/swoosh_1.ogg", "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg",
        "/Audio/CMU14/PersonalComputers/Games/swoosh_3.ogg",
    };

    protected static readonly string[] Steps =
    {
        "/Audio/Effects/Footsteps/plating1.ogg", "/Audio/Effects/Footsteps/plating2.ogg", "/Audio/Effects/Footsteps/plating3.ogg",
        "/Audio/Effects/Footsteps/plating4.ogg", "/Audio/Effects/Footsteps/plating5.ogg",
    };

    protected static readonly string[] MaleGasps = { "/Audio/Effects/Gasp/gasp_male1.ogg", "/Audio/Effects/Gasp/gasp_male2.ogg" };

    protected static readonly string[] FemaleGasps =
    {
        "/Audio/Effects/Gasp/gasp_female1.ogg", "/Audio/Effects/Gasp/gasp_female2.ogg", "/Audio/Effects/Gasp/gasp_female3.ogg",
    };

    protected static readonly string[] Whistles =
    {
        "/Audio/Voice/Human/whistle_1.ogg", "/Audio/Voice/Human/whistle_2.ogg", "/Audio/Voice/Human/whistle_3.ogg",
        "/Audio/Voice/Human/whistle_4.ogg",
    };

    // The corridor is drawn as one side-view composition rather than tiled sprites: from the top, a ceiling girder
    // with strip lights, a service band of pipes, the main wall of bevelled riveted plates, a handrail, a ribbed lower
    // wall, the skirting and the deck, with foreground railings at the bottom edge. Its palette is the six greys of
    // the game's metal wall sprite (Walls/metal.rsi), pulled into the phosphor. Doors, lockers, consoles, crates,
    // signs and vents are the game's own sprites, placed sparingly at sensible heights.
    public const string Structures = "/Textures/_RMC14/Structures/";
    public const string Scrubber = Structures + "Piping/Atmospherics/vent_scrubber.rsi";
    private const string FloorTile = "/Textures/_RMC14/Tiles/almayer/floor.png";
    private const string CatwalkTile = "/Textures/_RMC14/Tiles/almayer/catwalk_tile.png";
    private const string Posters = Structures + "Wallmounts/posters.rsi";
    private const string Signs = Structures + "Wallmounts/semiotics.rsi";
    private const string Display = Structures + "Machines/status_display.rsi";
    private const string Computer = Structures + "Machines/computer.rsi";

    /// <summary>Doors are 32 px sprites at 3.4: a little taller than a person.</summary>
    private const float DoorSize = 109;

    private static readonly string[] Doors =
    {
        Structures + "Doors/Airlocks/maint_door.rsi", Structures + "Doors/Airlocks/personal_door.rsi", Structures + "Doors/Airlocks/secure_door.rsi",
        Structures + "Doors/Airlocks/engi_door.rsi", Structures + "Doors/Airlocks/medi_door.rsi", Structures + "Doors/Airlocks/sec_door.rsi",
    };

    private static readonly string[] Closets =
    {
        Structures + "Storage/Closets/green.rsi", Structures + "Storage/Closets/red.rsi", Structures + "Storage/Closets/standard.rsi",
        Structures + "Storage/Closets/blue.rsi", Structures + "Storage/Closets/yellow.rsi",
    };

    private static readonly string[] Crates =
    {
        Structures + "Storage/Crates/ammo.rsi", Structures + "Storage/Crates/basic.rsi", Structures + "Storage/Crates/alpha.rsi",
    };

    private static readonly string[] PosterStates =
    {
        "safety_guidance", "assault_rifle", "proper_lifting", "safety_goggles_1", "the_future", "remember_io", "walkman", "eat",
    };

    private static readonly string[] SignStates = { "biohazard", "airlock", "hazard", "highvoltage", "firehaz", "escapepod", "galley", "cryo" };

    /// <summary>The metal wall sprite's greys, pulled into the phosphor: outline, dark, mid, main, light, highlight, and a rim for trims.</summary>
    public static readonly Color MetalOutline = Color.FromHex("#0E160F");
    public static readonly Color MetalDark = Color.FromHex("#152018");
    public static readonly Color MetalMid = Color.FromHex("#1B2A1F");
    public static readonly Color MetalMain = Color.FromHex("#223427");
    private static readonly Color MetalShade = Color.FromHex("#1F3024");
    public static readonly Color MetalLight = Color.FromHex("#283C2C");
    public static readonly Color MetalHigh = Color.FromHex("#33503A");
    public static readonly Color MetalRim = Color.FromHex("#4C7A56");
    private static readonly Color TubeLit = Color.FromHex("#DFFFE6");

    /// <summary>Furniture is multiplied by this: dimmer than the characters, so they stay in front.</summary>
    public static readonly Color PropTint = Color.FromHex("#8EAE94");

    public static readonly Color FloorFar = Color.FromHex("#4E6654");
    public static readonly Color FloorNear = Color.FromHex("#647E6A");

    // the wall's bands, top to bottom
    private const float ServiceTop = 40;
    private const float WallTop = 62;
    private const float PlateTop = 66;
    private const float RailY = 148;
    private const float LowerTop = 156;
    private const float SkirtTop = 172;
    private const float ForegroundDepth = 1.35f;

    // locker traps: the tell starts this many seconds before the player gets there and lasts this long before the door goes
    private const int MaxLockers = 2;
    private const float LockerTellEta = 1.6f;
    private const float LockerTell = 0.55f;

    // a shotgunner braces, then blasts a spread of short-range pellets into one band: low is jumped, high is ducked
    private const float ShotgunBrace = 0.4f;
    private const float ShotgunPumpTime = 0.65f;
    private const float ShotgunRange = 230;
    private const float ShotgunClosest = 70;
    private const int ShotgunPellets = 6;
    private const float ShotgunSpeed = 420;
    private static readonly Vector2 ShotgunLow = new(2, 14);
    private static readonly Vector2 ShotgunHigh = new(17, 52);

    private const string Shotgun = "/Audio/_RMC14/Weapons/Guns/Gunshots/gun_shotgun.ogg";
    private const string ShotgunPump = "/Audio/_RMC14/Weapons/Guns/Pump/shotgunpump1.ogg";
    private const string LockerOpen = "/Audio/Effects/closetopen.ogg";

    private static readonly string[] Rattles =
    {
        "/Audio/Effects/fence_rattle1.ogg", "/Audio/Effects/fence_rattle2.ogg", "/Audio/Effects/fence_rattle3.ogg",
    };

    protected static readonly Color Sky = Color.FromHex("#010502");
    private static readonly Color Hills = Color.FromHex("#0A2A12");
    protected static readonly Color Green = CMUHuggerRun.PhosphorGreen;
    protected static readonly Color Dim = CMUHuggerRun.PhosphorDim;

    private string[]? _signs;

    #endregion

    private struct Bullet
    {
        public Vector2 At;
        public Vector2 Velocity;

        /// <summary>Seconds left in flight: shotgun pellets are short range, tracers fly until they leave.</summary>
        public float Life;

        /// <summary>Seconds since it was fired, and a random draw, both for the demo bot's reactions.</summary>
        public float Age;

        public float Seed;
    }

    private enum LockerState : byte
    {
        Shut,
        Tell,
        Open,
    }

    private struct Locker
    {
        public float X;
        public CMUHuggerLockerContent Content;
        public int Closet;
        public LockerState State;
        public float Timer;
    }

    private struct QueuedShot
    {
        public float Delay;
        public float Lane;
    }

    private struct MuzzleFlash
    {
        public Vector2 At;
        public float Age;
    }

    private readonly Bullet[] _bullets = new Bullet[MaxBullets];
    private int _bulletCount;
    private readonly QueuedShot[] _shots = new QueuedShot[MaxShots];
    private int _shotCount;
    private readonly MuzzleFlash[] _flashes = new MuzzleFlash[MaxFlashes];
    private int _flashCount;
    private readonly Locker[] _lockers = new Locker[MaxLockers];
    private int _lockerCount;
    protected readonly CMUHuggerHuman[] Humans = new CMUHuggerHuman[MaxHumans];

    /// <summary>World units the floor has moved, and how fast it is moving now.</summary>
    protected float Scroll;

    protected float Speed;

    /// <summary>Seconds into the stage.</summary>
    protected float Time;

    /// <summary>The run is stopped for a scene or a death: the corridor skids to a halt and nothing spawns or hits.</summary>
    protected bool Halted;

    private float _stopRate;

    // the player creature: feet height over the floor and climb in units per second
    protected float Y;
    protected float Vy;
    protected bool Grounded = true;
    protected float Squash;
    protected float Anim;
    private float _hold;
    private float _buffer;
    private float _dust;

    // its body once dead: X along the floor, Y height above it
    protected bool Corpse;
    protected bool CorpseDown;
    protected Vector2 CorpseAt;
    protected Vector2 CorpseVelocity;
    protected float CorpseSpin;
    protected float CorpseRotation;
    private float _sizzle;

    private float _ambient;

    protected CMUHuggerCorridorStage()
    {
        for (var i = 0; i < MaxHumans; i++)
        {
            Humans[i] = new CMUHuggerHuman();
        }
    }

    /// <summary>Whether the player is ducking: Down held while on the floor.</summary>
    protected bool Ducking => Run.Held(CMUArcadeKey.Down) && Grounded;

    /// <summary>The player is in control and things can still hit it.</summary>
    protected bool Playing => !Run.Over && !Run.Attract && !Halted;

    public override void Enter()
    {
        Scroll = 0;
        Speed = StartSpeed;
        Time = 0;
        Halted = false;
        Y = 0;
        Vy = 0;
        Grounded = true;
        Squash = 0;
        _hold = 0;
        _buffer = 0;
        Corpse = false;
        _sizzle = 0;
        _bulletCount = 0;
        _shotCount = 0;
        _flashCount = 0;
        _lockerCount = 0;
        _ambient = 5;
        foreach (var human in Humans)
        {
            human.Active = false;
        }
    }

    public override void OnKey(CMUArcadeInput input)
    {
        if (input.Down && input.Key is CMUArcadeKey.Up or CMUArcadeKey.Action)
            _buffer = JumpBuffer;
    }

    /// <summary>Stops the run where it is: the corridor skids to a halt and nothing more spawns or hits.</summary>
    protected void Halt()
    {
        Halted = true;
        _stopRate = MathF.Max(1, Speed / StopTime);
        _shotCount = 0;
    }

    public override void Update(float dt)
    {
        if (Run.Attract)
            Speed = 28;
        else if (Halted || Run.Over)
            Speed = MathF.Max(0, Speed - _stopRate * dt);
        else
            Speed = MathF.Min(MaxSpeed, Speed + Acceleration * dt);

        var moved = Speed * dt;
        Scroll += moved;
        Run.Camera = new Vector2(Scroll, 0);
        var scurry = Playing ? MathF.Max(1, Speed / StartSpeed) * (Ducking ? 1.6f : 1) : 1;
        Anim += dt * scurry;
        if (Playing)
        {
            Time += dt;
            Run.AddDistance(moved);
            MovePlayer(dt);
            Ambience(dt);
            SpawnThreats(dt);
            FireQueued(dt);
        }

        MoveBullets(dt);
        MoveHumans(dt);
        MoveLockers(dt);
        MoveCorpse(dt);
        UpdateStage(dt);
        if (Playing)
            Collide();
    }

    /// <summary>Spawns this stage's bullets, humans and obstacles. Called every step while playing.</summary>
    protected abstract void SpawnThreats(float dt);

    /// <summary>Everything else the stage does each step, playing or not (scenes, obstacles).</summary>
    protected virtual void UpdateStage(float dt)
    {
    }

    /// <summary>The player's box touched a human: <paramref name="head"/> when it met the head from above the shoulders.</summary>
    protected abstract void OnTouch(CMUHuggerHuman human, bool head);

    /// <summary>A knifeman's stab caught the player.</summary>
    protected virtual void OnStabbed(CMUHuggerHuman human)
    {
    }

    /// <summary>A tracer hit the player.</summary>
    protected abstract void OnShot(Vector2 hit, Vector2 dir);

    /// <summary>Extra collisions the stage checks each step while playing (airlocks, vents).</summary>
    protected virtual void CollideStage(float left, float right, float top, float bottom)
    {
    }

    #region The player

    private void MovePlayer(float dt)
    {
        if (_buffer > 0)
            _buffer -= dt;

        var down = Run.Held(CMUArcadeKey.Down);
        if (Grounded)
        {
            if (_buffer > 0 && !down)
                Jump();
        }
        else
        {
            // holding the jump floats it higher for a moment; ducking in the air drops it like a stone
            var gravity = Gravity;
            var held = Run.Held(CMUArcadeKey.Up) || Run.Held(CMUArcadeKey.Action);
            if (down)
            {
                gravity = FastFallGravity;
            }
            else if (held && Vy > 0 && _hold < MaxHold)
            {
                gravity = HoldGravity;
                _hold += dt;
            }

            Vy -= gravity * dt;
            Y += Vy * dt;
            if (Y <= 0)
                Land();
        }

        Squash = MoveTowards(Squash, 0, dt * 7);
        if (!Grounded)
            return;

        _dust -= dt;
        if (_dust > 0)
            return;

        _dust = 0.06f + Run.Fx() * 0.06f;
        Run.Emit(World(PlayerX - HalfWidth, FloorY - 1), 1, CMUHuggerRun.Dust.WithAlpha(0.45f), 30, 0.35f, 1.6f, MathF.PI + 0.4f, 0.8f,
            -20, kind: CMUHuggerRun.Smoke);
    }

    private void Jump()
    {
        Grounded = false;
        Vy = JumpVelocity;
        _hold = 0;
        _buffer = 0;
        Squash = 1;
        Run.PlayAny(Swooshes, -6, 1.1f);
        Run.Emit(World(PlayerX - 4, FloorY - 1), 5, CMUHuggerRun.Dust.WithAlpha(0.6f), 60, 0.4f, 2, MathF.PI, 1.2f, -10,
            kind: CMUHuggerRun.Smoke);
    }

    private void Land()
    {
        Y = 0;
        Vy = 0;
        Grounded = true;
        Squash = -1;
        Run.PlayAny(Steps, -12, 1.6f);
        Run.Emit(World(PlayerX, FloorY - 1), 6, CMUHuggerRun.Dust.WithAlpha(0.6f), 50, 0.45f, 2, -MathF.PI / 2, MathF.PI, -10,
            kind: CMUHuggerRun.Smoke);
        if (_buffer > 0 && !Run.Held(CMUArcadeKey.Down))
            Jump();
    }

    /// <summary>The player's hit box on screen this step.</summary>
    protected void PlayerBox(out float left, out float right, out float top, out float bottom)
    {
        bottom = FloorY - Y;
        top = bottom - (Ducking ? DuckHeight : Height);
        left = PlayerX - HalfWidth;
        right = PlayerX + HalfWidth;
    }

    /// <summary>
    /// Width and height to draw the player sprite this frame: squashed on landing, stretched on take-off, flattened
    /// while ducking.
    /// </summary>
    protected Vector2 PlayerSize(float size, float duck = 0.55f)
    {
        var scale = new Vector2(size, size);
        if (Squash > 0)
            scale *= new Vector2(1 - 0.14f * Squash, 1 + 0.22f * Squash);
        else if (Squash < 0)
            scale *= new Vector2(1 - 0.2f * Squash, 1 + 0.26f * Squash);

        if (Ducking)
            scale *= new Vector2(1.14f, duck);

        return scale;
    }

    /// <summary>The nose-up/nose-down tilt of a jump, in radians: the creature keeps facing the way it runs.</summary>
    protected float JumpTilt()
    {
        if (Grounded)
            return 0;

        return Math.Clamp(-Vy / 1400, -0.3f, 0.25f);
    }

    #endregion

    #region Fire

    private void Ambience(float dt)
    {
        _ambient -= dt;
        if (_ambient > 0)
            return;

        _ambient = 7 + Run.Fx() * 8;
        if (Run.Fx() < 0.3f)
        {
            Run.Play(DistantRoar, -14, 0.9f + Run.Fx() * 0.2f);
            return;
        }

        var shots = 2 + Run.Rng.Next(4);
        for (var i = 0; i < shots; i++)
        {
            Run.Pend(i * 0.09f, RifleShot, -18, 0.78f);
        }
    }

    /// <summary>Queues a pattern of off-screen fire that gets nastier with <see cref="Time"/>: singles, bursts, a low-then-mid pair.</summary>
    protected void FirePattern()
    {
        var hard = Math.Clamp(Time / 90f, 0, 1);
        var roll = Run.Rng.NextDouble();
        if (Time > 15 && roll < 0.25 + hard * 0.2)
        {
            Queue(PickLane(), 3);
        }
        else if (Time > 40 && roll < 0.55)
        {
            Queue(LaneLow, 1);
            Queue(LaneMid, 1, 0.3f);
        }
        else
        {
            Queue(PickLane(), 1);
        }
    }

    /// <summary>Seconds until the next pattern, shrinking with time; <paramref name="rate"/> above 1 fires more often.</summary>
    protected float FireGap(float rate = 1)
    {
        var hard = Math.Clamp(Time / 90f, 0, 1);
        return (1.55f - 0.7f * hard) * (0.8f + Run.Fx() * 0.6f) / rate;
    }

    private float PickLane()
    {
        if (Time < 10)
            return LaneLow;

        var roll = Run.Rng.NextDouble();
        if (roll < 0.5)
            return LaneLow;

        return roll < 0.8 || Time < 20 ? LaneMid : LaneHigh;
    }

    /// <summary>Queues off-screen shots down a lane, each shown by its laser sight for <see cref="Warning"/> seconds first.</summary>
    protected void Queue(float lane, int count, float delay = 0)
    {
        for (var i = 0; i < count && _shotCount < MaxShots; i++)
        {
            _shots[_shotCount++] = new QueuedShot { Delay = Warning + delay + i * BurstGap, Lane = lane };
        }
    }

    protected bool ShotsQueued => _shotCount > 0;

    private void FireQueued(float dt)
    {
        for (var i = _shotCount - 1; i >= 0; i--)
        {
            _shots[i].Delay -= dt;
            if (_shots[i].Delay > 0)
                continue;

            var lane = _shots[i].Lane;
            _shots[i] = _shots[--_shotCount];
            AddBullet(new Vector2(CMUHuggerRun.ScreenWidth + 4, lane), new Vector2(-(BulletSpeed + Speed * BulletSpeedPerRun), 0));
            AddFlash(new Vector2(CMUHuggerRun.ScreenWidth - 1, lane));
            Run.Play(RifleShot, -4, 0.95f + Run.Fx() * 0.1f);
        }
    }

    private void AddBullet(Vector2 at, Vector2 velocity, float life = float.PositiveInfinity)
    {
        if (_bulletCount < MaxBullets)
            _bullets[_bulletCount++] = new Bullet { At = at, Velocity = velocity, Life = life, Seed = Run.Fx() };
    }

    private void AddFlash(Vector2 at)
    {
        if (_flashCount < MaxFlashes)
            _flashes[_flashCount++] = new MuzzleFlash { At = at };
    }

    /// <summary>Whether any tracer is still coming at the player.</summary>
    protected bool BulletsAhead()
    {
        for (var i = 0; i < _bulletCount; i++)
        {
            if (_bullets[i].At.X > PlayerX)
                return true;
        }

        return false;
    }

    private void MoveBullets(float dt)
    {
        for (var i = _bulletCount - 1; i >= 0; i--)
        {
            ref var bullet = ref _bullets[i];
            bullet.At += bullet.Velocity * dt;
            bullet.Life -= dt;
            bullet.Age += dt;
            if (bullet.Life <= 0)
            {
                // a pellet at the end of its range
                Run.Emit(World(bullet.At.X, bullet.At.Y), 2, CMUHuggerRun.Dust.WithAlpha(0.4f), 10, 0.3f, 1.5f, 0, MathF.Tau, -10, kind: CMUHuggerRun.Smoke);
            }
            else if (bullet.At.Y >= FloorY + 2)
            {
                // an aimed shot that missed sparks off the deck plating
                var world = World(bullet.At.X, FloorY + 2);
                Run.Emit(world, 7, CMUHuggerRun.Amber, 160, 0.3f, 1, -MathF.PI / 2 + MathF.Sign(bullet.Velocity.X) * 0.6f, 1.2f, 500,
                    floor: FloorY + 2, kind: CMUHuggerRun.Spark);
                Run.Emit(world, 2, CMUHuggerRun.Dust.WithAlpha(0.5f), 20, 0.5f, 2, -MathF.PI / 2, 1, -15, kind: CMUHuggerRun.Smoke);
                Run.Play(Ricochet, -10, 1.1f + Run.Fx() * 0.2f);
            }
            else if (bullet.At.X > -Tracer - 10 && bullet.At.Y > CMUHuggerRun.HudHeight)
            {
                continue;
            }

            _bullets[i] = _bullets[--_bulletCount];
        }

        for (var i = _flashCount - 1; i >= 0; i--)
        {
            _flashes[i].Age += dt;
            if (_flashes[i].Age >= 0.07f)
                _flashes[i] = _flashes[--_flashCount];
        }
    }

    #endregion

    #region Locker traps

    /// <summary>
    /// A wall locker on the deck ahead that may hold somebody: as the player nears it rattles, the door shifts and,
    /// if someone is inside, eyes glint in its slats; then it bursts open. The odds of a marine or a colonist inside
    /// are given; the rest are empty fake-outs.
    /// </summary>
    protected void SpawnLocker(float marine, float colonist)
    {
        if (_lockerCount >= MaxLockers)
            return;

        var roll = Run.Fx();
        var content = roll < marine ? CMUHuggerLockerContent.Marine : roll < marine + colonist ? CMUHuggerLockerContent.Colonist : CMUHuggerLockerContent.Empty;
        _lockers[_lockerCount++] = new Locker
        {
            X = BareSlot(Scroll + CMUHuggerRun.ScreenWidth + 50),
            Content = content,
            Closet = Run.Rng.Next(Closets.Length),
            State = LockerState.Shut,
        };
    }

    /// <summary>Whether a locker that has not burst yet is ahead of the player.</summary>
    protected bool LockerAhead()
    {
        for (var i = 0; i < _lockerCount; i++)
        {
            if (_lockers[i].State != LockerState.Open && _lockers[i].X - Scroll > PlayerX - 30)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The world x of the middle of the first bare stretch of wall at or after <paramref name="x"/>: a panel with no
    /// furniture on it or on the two either side, so hazards never land near a door, a locker or a sign.
    /// </summary>
    protected static float BareSlot(float x)
    {
        var panel = (int) MathF.Floor(x / PanelWidth);
        while (Furniture(panel - 2) >= 0 || Furniture(panel - 1) >= 0 || Furniture(panel) >= 0 || Furniture(panel + 1) >= 0 || Furniture(panel + 2) >= 0)
        {
            panel++;
        }

        return panel * PanelWidth + PanelWidth / 2;
    }

    private void MoveLockers(float dt)
    {
        for (var i = _lockerCount - 1; i >= 0; i--)
        {
            ref var locker = ref _lockers[i];
            locker.Timer += dt;
            var x = locker.X - Scroll;
            if (x < -90)
            {
                _lockers[i] = _lockers[--_lockerCount];
                continue;
            }

            if (locker.State == LockerState.Shut && Playing && (x - PlayerX) / MathF.Max(1, Speed) < LockerTellEta)
            {
                locker.State = LockerState.Tell;
                locker.Timer = 0;
                Run.PlayAny(Rattles, locker.Content == CMUHuggerLockerContent.Empty ? -8 : -3);
            }
            else if (locker.State == LockerState.Tell && locker.Timer >= LockerTell)
            {
                OpenLocker(ref locker, x);
            }
        }
    }

    /// <summary>The locker door bangs open on whatever was inside.</summary>
    private void OpenLocker(ref Locker locker, float x)
    {
        locker.State = LockerState.Open;
        locker.Timer = 0;
        var empty = locker.Content == CMUHuggerLockerContent.Empty;
        Run.Play(LockerOpen, empty ? -6 : 2, empty ? 0.8f : 1);
        if (empty)
            return;

        Run.Shake(1.5f, 0.2f);
        Run.Emit(World(x + 14, FloorY - 50), 8, CMUHuggerRun.Dust.WithAlpha(0.5f), 50, 0.6f, 3, 0, 1.6f, -10, kind: CMUHuggerRun.Smoke);
        var marine = locker.Content == CMUHuggerLockerContent.Marine;
        if (SpawnHuman(marine ? CMUHuggerOutfit.Shotgunner : CMUHuggerOutfit.Colonist, true) is not { } human)
            return;

        human.X = locker.X + 6;
        human.Timer = 0;
        human.Shouted = true;
        human.Whistled = true;
        if (marine)
        {
            // out he steps, shotgun up
            human.State = CMUHuggerHumanState.Brace;
            human.High = Run.Fx() < 0.5f;
            Run.Pop("!", World(x, FloorY - 116), CMUHuggerRun.Alarm, 0.6f);
            return;
        }

        human.State = CMUHuggerHumanState.Flee;
        human.East = true;
        human.Flees = true;
        Run.Scream(human.Person.Female);
    }

    /// <summary>A shotgunner braces, shows where the blast will go, then fires a spread of pellets and pumps another round.</summary>
    private void Brace(CMUHuggerHuman human, float x)
    {
        // too close to level the gun at something underfoot
        if (!Playing || x < PlayerX + ShotgunClosest)
        {
            human.State = CMUHuggerHumanState.Stand;
            human.Timer = 0;
            return;
        }

        if (human.Timer < ShotgunBrace || x - PlayerX > ShotgunRange)
            return;

        var muzzle = new Vector2(x - 46, FloorY - 51);
        var band = human.High ? ShotgunHigh : ShotgunLow;
        for (var k = 0; k < ShotgunPellets; k++)
        {
            // aimed through the back of the player, so a band clears (or fills) the whole of its box
            var target = new Vector2(PlayerX - HalfWidth - 4, FloorY - (band.X + (band.Y - band.X) * (k + Run.Fx() * 0.6f) / ShotgunPellets));
            var dir = Vector2.Normalize(target - muzzle);
            AddBullet(muzzle, dir * ShotgunSpeed * (0.92f + Run.Fx() * 0.16f), (muzzle.X - PlayerX + 40) / ShotgunSpeed);
        }

        AddFlash(muzzle);
        AddFlash(muzzle - new Vector2(8, 0));
        Run.Emit(World(muzzle.X, muzzle.Y), 10, CMUHuggerRun.TracerGlow, 200, 0.2f, 1, MathF.PI, 0.7f, kind: CMUHuggerRun.Spark);
        Run.Emit(World(muzzle.X, muzzle.Y), 5, CMUHuggerRun.Dust.WithAlpha(0.5f), 30, 0.7f, 4, MathF.PI, 1, -15, kind: CMUHuggerRun.Smoke);
        Run.Play(Shotgun, 0, 0.95f + Run.Fx() * 0.1f);
        Run.Shake(2, 0.15f);
        Run.Pend(0.3f, ShotgunPump, -2);
        human.State = CMUHuggerHumanState.Pump;
        human.Timer = 0;
    }

    private void DrawLockers(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _lockerCount; i++)
        {
            var locker = _lockers[i];
            var x = locker.X - Scroll;
            if (x < -80 || x > CMUHuggerRun.ScreenWidth + 80)
                continue;

            // a rattle that builds before the door goes
            var shake = locker.State == LockerState.Tell ? MathF.Sin(locker.Timer * 60) * (0.6f + 1.4f * locker.Timer / LockerTell) : 0;
            var closet = Closets[locker.Closet];
            var size = new Vector2(96, 96);
            const float y = FloorY - 14 * 3;
            Run.DrawSprite(handle, Run.Frame(closet, "base"), new Vector2(x + shake, y), size, 0, PropTint);
            if (locker.State == LockerState.Open)
            {
                Run.DrawSprite(handle, Run.Frame(closet, "open"), new Vector2(x, y), size, 0, PropTint);
                continue;
            }

            Run.DrawSprite(handle, Run.Frame(closet, "closed"), new Vector2(x + shake * 1.6f, y), size, 0, PropTint);

            // eyes in the slats when somebody is in there
            if (locker.State != LockerState.Tell || locker.Content == CMUHuggerLockerContent.Empty || Run.Clock % 0.5f >= 0.35f)
                continue;

            Run.Fill(handle, x - 5 + shake * 1.6f, FloorY - 58, 2, 1.5f, Color.White.WithAlpha(0.9f));
            Run.Fill(handle, x + 2 + shake * 1.6f, FloorY - 58, 2, 1.5f, Color.White.WithAlpha(0.9f));
        }
    }

    /// <summary>The red zone a braced shotgunner is about to fill, at the player, so it can be jumped or ducked.</summary>
    private void DrawBlastWarnings(DrawingHandleScreen handle)
    {
        foreach (var human in Humans)
        {
            if (!human.Active || human.State != CMUHuggerHumanState.Brace || !Playing)
                continue;

            var x = human.X - Scroll;
            if (x - PlayerX > ShotgunRange)
                continue;

            var band = human.High ? ShotgunHigh : ShotgunLow;
            var k = MathF.Min(1, human.Timer / ShotgunBrace);
            var flicker = 0.7f + 0.3f * MathF.Sin(Run.Clock * 40);
            Run.Fill(handle, PlayerX - 22, FloorY - band.Y, 44, band.Y - band.X, CMUHuggerRun.Laser.WithAlpha((0.1f + 0.22f * k) * flicker));
            Run.Outline(handle, PlayerX - 22, FloorY - band.Y, 44, band.Y - band.X, CMUHuggerRun.Laser.WithAlpha(0.5f * k * flicker));
            Run.Line(handle, new Vector2(x - 46, FloorY - 51), new Vector2(PlayerX + 22, FloorY - (band.X + band.Y) / 2),
                CMUHuggerRun.Laser.WithAlpha(0.3f * k * flicker));
        }
    }

    #endregion

    #region Demo bot

    // The demo's player: it reads each threat a human beat after it shows (a reaction time drawn per threat), jumps
    // low fire, ducks mid fire and high shotgun blasts, and leaves the rest to the stage. Now and then it fumbles a
    // threat by reacting late, so runs end.
    private const float BotReactMin = 0.14f;
    private const float BotReactSpread = 0.16f;
    private const float BotFumble = 0.05f;
    private const float BotFumbleLate = 0.2f;
    private const float BotLead = 0.17f;
    private const float BotTap = 0.07f;
    private const float BotLongHold = 0.24f;

    private float _botHold;

    public override void DemoThink(float dt)
    {
        if (!Playing)
        {
            Run.Bot(CMUArcadeKey.Up, false);
            Run.Bot(CMUArcadeKey.Down, false);
            _botHold = 0;
            return;
        }

        var jump = false;
        var hold = BotTap;
        var duck = false;
        BotBullets(ref jump, ref hold, ref duck);
        BotBrace(ref jump, ref hold, ref duck);
        BotStage(ref jump, ref hold, ref duck);
        if (_botHold > 0)
        {
            _botHold -= dt;
            if (_botHold <= 0)
                Run.Bot(CMUArcadeKey.Up, false);
        }

        if (jump && Grounded && _botHold <= 0)
        {
            Run.Bot(CMUArcadeKey.Down, false);
            Run.Bot(CMUArcadeKey.Up, true);
            _botHold = hold;
            return;
        }

        // ducking in the air is a fast fall, into whatever it was jumping
        Run.Bot(CMUArcadeKey.Down, duck && Grounded);
    }

    /// <summary>The stage's own reads for its bot: faces to leap at, knives, airlocks, vents.</summary>
    protected virtual void BotStage(ref bool jump, ref float hold, ref bool duck)
    {
    }

    /// <summary>A threat's reaction time, from its own random draw: a human beat, and now and then a fumble.</summary>
    protected static float BotReaction(float seed)
    {
        return BotReactMin + seed * BotReactSpread + (seed < BotFumble ? BotFumbleLate : 0);
    }

    private void BotBullets(ref bool jump, ref float hold, ref bool duck)
    {
        for (var i = 0; i < _bulletCount; i++)
        {
            var bullet = _bullets[i];
            if (bullet.Velocity.X >= 0 || bullet.Age < BotReaction(bullet.Seed))
                continue;

            var eta = (bullet.At.X - PlayerX) / -bullet.Velocity.X;
            if (eta < -0.12f || eta > 1)
                continue;

            // how high it will be when it reaches the player
            var height = FloorY - (bullet.At.Y + bullet.Velocity.Y * MathF.Max(0, eta));
            if (height > Height + 3)
                continue;

            if (height > DuckHeight + 2)
            {
                if (eta < 0.32f)
                    duck = true;

                continue;
            }

            var lead = BotLead + (bullet.Seed - 0.5f) * 0.08f;
            if (eta <= 0 || eta > lead)
                continue;

            jump = true;
            if (LowFireAfter(eta))
                hold = MathF.Max(hold, BotLongHold);
        }
    }

    /// <summary>Whether more low fire follows close behind, so the jump wants holding (a burst).</summary>
    private bool LowFireAfter(float eta)
    {
        for (var i = 0; i < _bulletCount; i++)
        {
            var bullet = _bullets[i];
            if (bullet.Velocity.X >= 0)
                continue;

            var next = (bullet.At.X - PlayerX) / -bullet.Velocity.X;
            if (next > eta + 0.02f && next < eta + 0.45f && FloorY - bullet.At.Y <= DuckHeight + 2)
                return true;
        }

        return false;
    }

    /// <summary>
    /// A braced shotgunner: get down under a high blast and stay down while the pellets go over, or time a held jump
    /// over a low one from when it will reach the player. The red zone is read a beat after he braces, later on a fumble.
    /// </summary>
    private void BotBrace(ref bool jump, ref float hold, ref bool duck)
    {
        foreach (var human in Humans)
        {
            var bracing = human.State == CMUHuggerHumanState.Brace;
            if (!human.Active || !(bracing || human.State == CMUHuggerHumanState.Pump && human.Timer < 0.3f))
                continue;

            var x = human.X - Scroll;
            if (x - PlayerX > ShotgunRange)
                continue;

            if (bracing && human.Timer < 0.1f + human.BotSeed * 0.15f + (human.BotSeed < BotFumble ? BotFumbleLate : 0))
                continue;

            if (human.High)
            {
                duck = true;
                continue;
            }

            var arrival = (bracing ? ShotgunBrace - human.Timer : -human.Timer) + (x - 46 - PlayerX) / ShotgunSpeed;
            if (arrival < -0.05f || arrival > BotLead + (human.BotSeed - 0.5f) * 0.06f)
                continue;

            jump = true;
            hold = MathF.Max(hold, BotLongHold);
        }
    }

    #endregion

    #region Humans

    /// <summary>A human walks (or stands) in from the right. Null when all are in use.</summary>
    protected CMUHuggerHuman? SpawnHuman(CMUHuggerOutfit outfit, bool standing)
    {
        CMUHuggerHuman? human = null;
        foreach (var free in Humans)
        {
            if (free.Active)
                continue;

            human = free;
            break;
        }

        if (human == null)
            return null;

        Run.Dress(human.Person, outfit);
        human.Active = true;
        human.East = false;
        human.Flees = false;
        human.Infected = false;
        human.Whistled = human.Marine;
        human.Shouted = false;
        human.Timer = 0;
        human.Step = Run.Fx() * 3;
        human.Fall = 1;
        human.Cooldown = 0;
        human.Shots = 0;
        human.BotSeed = Run.Fx();
        human.BotDone = false;
        human.X = Scroll + CMUHuggerRun.ScreenWidth + 60;
        human.State = standing || human.Rifleman ? CMUHuggerHumanState.Stand : CMUHuggerHumanState.Walk;
        human.Walk = human.State == CMUHuggerHumanState.Walk ? 10 + Run.Fx() * 24 : 0;
        return human;
    }

    /// <summary>Whether a human who can still act is ahead of the player.</summary>
    protected bool HumanAhead()
    {
        foreach (var human in Humans)
        {
            if (human.Active && human.X - Scroll > PlayerX - 24 && Hittable(human))
                return true;
        }

        return false;
    }

    protected static bool Hittable(CMUHuggerHuman human)
    {
        return human.Active && human.State is not (CMUHuggerHumanState.Latched or CMUHuggerHumanState.Shot or CMUHuggerHumanState.Down);
    }

    private void MoveHumans(float dt)
    {
        foreach (var human in Humans)
        {
            if (!human.Active)
                continue;

            human.Timer += dt;
            var x = human.X - Scroll;
            switch (human.State)
            {
                case CMUHuggerHumanState.Walk:
                    human.X -= human.Walk * dt;
                    human.Step += dt * (4 + human.Walk * 0.18f);
                    if (!human.Whistled && x < CMUHuggerRun.ScreenWidth - 10)
                    {
                        human.Whistled = true;
                        if (Run.Fx() < 0.35f)
                            Run.PlayAny(Whistles, -12);
                    }

                    Notice(human, x);
                    break;
                case CMUHuggerHumanState.Stand:
                    if (human.Rifleman)
                        Watch(human, x, dt);
                    else
                        Notice(human, x);
                    break;
                case CMUHuggerHumanState.Alert:
                    if (human.Flees && human.Timer > 0.3f)
                    {
                        human.State = CMUHuggerHumanState.Flee;
                        human.East = true;
                        human.Timer = 0;
                    }

                    break;
                case CMUHuggerHumanState.Flee:
                    human.X += 48 * dt;
                    human.Step += dt * 14;
                    break;
                case CMUHuggerHumanState.Aim:
                    Backpedal(human, dt);
                    Aim(human, x);
                    break;
                case CMUHuggerHumanState.Brace:
                    Brace(human, x);
                    break;
                case CMUHuggerHumanState.Pump:
                    if (human.Timer < ShotgunPumpTime)
                        break;

                    human.State = CMUHuggerHumanState.Brace;
                    human.Timer = 0;
                    human.High = Run.Fx() < 0.5f;
                    break;
                case CMUHuggerHumanState.WindUp:
                    if (human.Timer < WindUpTime)
                        break;

                    human.State = CMUHuggerHumanState.Stab;
                    human.Timer = 0;
                    Run.Play(KnifeSwing, -2, 0.9f);
                    break;
                case CMUHuggerHumanState.Stab:
                    if (human.Timer < StabTime)
                        break;

                    human.State = CMUHuggerHumanState.Recover;
                    human.Timer = 0;
                    break;
                case CMUHuggerHumanState.Recover:
                case CMUHuggerHumanState.Kick:
                    if (human.Timer < (human.State == CMUHuggerHumanState.Kick ? KickTime * 1.6f : RecoverTime))
                        break;

                    human.State = CMUHuggerHumanState.Stand;
                    human.Timer = 0;
                    break;
                case CMUHuggerHumanState.Latched:
                    if (human.Timer >= Struggle + FallTime)
                        Fallen(human);
                    break;
                case CMUHuggerHumanState.Shot:
                    if (human.Timer >= FallTime * 0.8f)
                        Fallen(human);
                    break;
            }

            if (x < -90 || human.State == CMUHuggerHumanState.Flee && x > CMUHuggerRun.ScreenWidth + 90)
                human.Active = false;
        }
    }

    /// <summary>A colonist who sees the player freezes with a gasp and may run; a knifeman closes in and winds up a stab.</summary>
    private void Notice(CMUHuggerHuman human, float x)
    {
        if (!Playing || x < PlayerX)
            return;

        if (human.Knifeman)
        {
            if (x - PlayerX > StabRange || human.Shouted)
                return;

            human.Shouted = true;
            human.State = CMUHuggerHumanState.WindUp;
            human.Timer = 0;
            Run.Pop("!", World(x, FloorY - 116), CMUHuggerRun.Alarm, 0.6f);
            return;
        }

        if (x - PlayerX > AlertRange)
            return;

        human.State = CMUHuggerHumanState.Alert;
        human.Timer = 0;
        human.Flees = !human.Marine && Run.Fx() < 0.45f;
        Run.Pop("!", World(x, FloorY - 116), CMUHuggerRun.Amber, 0.7f);
        Run.PlayAny(human.Person.Female ? FemaleGasps : MaleGasps, -6);
    }

    /// <summary>A rifleman backs off and takes aim every second or so, until the player is too close to shoot.</summary>
    private void Watch(CMUHuggerHuman human, float x, float dt)
    {
        if (!Playing)
            return;

        if (x < PlayerX + MarineReach)
        {
            Notice(human, x);
            return;
        }

        Backpedal(human, dt);
        human.Cooldown -= dt;
        if (human.Cooldown > 0 || x > CMUHuggerRun.ScreenWidth + 8)
            return;

        human.State = CMUHuggerHumanState.Aim;
        human.Timer = 0;
        human.Shots = 3;
        if (human.Shouted)
            return;

        human.Shouted = true;
        Run.Pop(Loc.GetString("cmu-game-hugger-contact"), World(x, FloorY - 118), CMUHuggerRun.Amber);
    }

    private void Aim(CMUHuggerHuman human, float x)
    {
        if (!Playing || x < PlayerX + MarineReach)
        {
            human.State = CMUHuggerHumanState.Stand;
            human.Timer = 0;
            return;
        }

        // the laser sight settles for a moment, then a three round burst at wherever the player is
        if (human.Timer < MarineAim + (3 - human.Shots) * 0.09f)
            return;

        var muzzle = Barrel(x);
        var dir = Vector2.Normalize(Target() - muzzle);
        AddBullet(muzzle, dir * MarineBulletSpeed);
        AddFlash(muzzle);
        Run.Play(RifleShot, 0, 0.95f + Run.Fx() * 0.1f);
        Run.Emit(World(muzzle.X + 34, muzzle.Y + 4), 1, CMUHuggerRun.Amber, 90, 1.2f, 1.8f, -MathF.PI / 2 + 0.5f, 0.6f, 600, floor: FloorY + 2);
        human.Shots--;
        if (human.Shots > 0)
            return;

        human.State = CMUHuggerHumanState.Stand;
        human.Timer = 0;
        human.Cooldown = 0.9f + Run.Fx() * 0.7f;
    }

    private static void Backpedal(CMUHuggerHuman human, float dt)
    {
        human.X += MarineBackpedal * dt;
        human.Step += dt * 6;
    }

    /// <summary>The M41A's muzzle on a rifleman facing west, from the sprite at <see cref="HumanScale"/>.</summary>
    protected static Vector2 Barrel(float x)
    {
        return new Vector2(x - 43, FloorY - 51);
    }

    /// <summary>Where marines aim: the middle of the player.</summary>
    protected Vector2 Target()
    {
        return new Vector2(PlayerX, FloorY - Y - (Ducking ? DuckHeight : Height) * 0.5f);
    }

    /// <summary>The body hits the floor.</summary>
    protected virtual void Fallen(CMUHuggerHuman human)
    {
        human.State = CMUHuggerHumanState.Down;
        human.Timer = 0;
        var x = human.X - Scroll;
        Run.Play(BodyFall, -1, 0.95f + Run.Fx() * 0.1f);
        Run.Emit(World(x + human.Fall * 50, FloorY), 12, CMUHuggerRun.Dust.WithAlpha(0.5f), 70, 0.6f, 3.5f, -MathF.PI / 2, MathF.PI, -10,
            kind: CMUHuggerRun.Smoke);
        Run.Shake(2.5f, 0.25f);
        if (!human.Infected)
            Run.Splat(new Vector2(human.X + human.Fall * 36, FloorY + 6), 52, CMUHuggerRun.DarkBlood.WithAlpha(0.9f));
    }

    /// <summary>A round meant for the player hits whoever it passed: they scream and drop.</summary>
    private void Wounded(CMUHuggerHuman human, Vector2 at, Vector2 dir)
    {
        human.State = CMUHuggerHumanState.Shot;
        human.Timer = 0;
        human.Fall = -1;
        var angle = MathF.Atan2(dir.Y, dir.X);
        var hit = World(at.X, at.Y);
        Run.Emit(hit, 24, CMUHuggerRun.Blood, 180, 1.2f, 2.2f, angle, 1.6f, 520, FloorY + 1, 14, stains: true);
        Run.Emit(hit, 6, CMUHuggerRun.DarkBlood, 90, 1, 3, angle, 1.2f, 520, FloorY + 1, 14, stains: true);
        Run.PlayAny(Flesh);
        Run.Scream(human.Person.Female, -2);
    }

    /// <summary>Where a standing human's face is on screen.</summary>
    protected Vector2 Face(CMUHuggerHuman human)
    {
        return new Vector2(human.X - Scroll, FloorY - 82);
    }

    #endregion

    #region Collisions

    private void Collide()
    {
        PlayerBox(out var left, out var right, out var top, out var bottom);
        for (var i = _bulletCount - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            var dir = Vector2.Normalize(bullet.Velocity);
            var tail = bullet.At - dir * Tracer;
            if (Inside(bullet.At, left, right, top, bottom) || Inside((bullet.At + tail) / 2, left, right, top, bottom)
                || Inside(tail, left, right, top, bottom))
            {
                _bullets[i] = _bullets[--_bulletCount];
                OnShot(bullet.At, dir);
                return;
            }

            // a round that got past the player can still find somebody behind it
            if (bullet.At.X >= PlayerX - 14)
                continue;

            foreach (var human in Humans)
            {
                if (!Hittable(human))
                    continue;

                var x = human.X - Scroll;
                if (!Inside(bullet.At, x - BodyHalfWidth, x + BodyHalfWidth, FloorY - HeadTop, FloorY))
                    continue;

                _bullets[i] = _bullets[--_bulletCount];
                Wounded(human, bullet.At, dir);
                break;
            }
        }

        foreach (var human in Humans)
        {
            if (!Hittable(human))
                continue;

            var x = human.X - Scroll;
            if (human.State == CMUHuggerHumanState.Stab && Overlap(left, right, top, bottom, x - StabReachFar, x - StabReachNear, FloorY - StabHeight, FloorY))
            {
                OnStabbed(human);
                if (!Playing)
                    return;
            }

            if (Overlap(left, right, top, bottom, x - HeadHalfWidth, x + HeadHalfWidth, FloorY - HeadTop, FloorY - HeadBottom))
            {
                OnTouch(human, true);
                if (!Playing)
                    return;

                continue;
            }

            if (Overlap(left, right, top, bottom, x - BodyHalfWidth, x + BodyHalfWidth, FloorY - BodyTop, FloorY))
            {
                OnTouch(human, false);
                if (!Playing)
                    return;
            }
        }

        CollideStage(left, right, top, bottom);
    }

    protected static bool Inside(Vector2 point, float left, float right, float top, float bottom)
    {
        // tracers are thin: a hair of slack so a grazing round still counts
        return point.X >= left - 1.5f && point.X <= right + 1.5f && point.Y >= top - 1.5f && point.Y <= bottom + 1.5f;
    }

    protected static bool Overlap(float left, float right, float top, float bottom, float otherLeft, float otherRight, float otherTop, float otherBottom)
    {
        return left < otherRight && right > otherLeft && top < otherBottom && bottom > otherTop;
    }

    #endregion

    #region Deaths

    /// <summary>
    /// The player bursts in acid where <paramref name="hit"/> struck it (screen point) and its body tumbles away
    /// with <paramref name="velocity"/> (world units, up positive), landing in a pool. The run is lost with
    /// <paramref name="text"/>.
    /// </summary>
    protected void KillPlayer(Vector2 hit, Vector2 dir, Vector2 velocity, float spin, string cry, string text, int acid = 40)
    {
        var angle = MathF.Atan2(dir.Y, dir.X);
        var at = World(hit.X, hit.Y);
        Corpse = true;
        CorpseDown = false;
        CorpseAt = new Vector2(PlayerX + Scroll, Y + 8);
        CorpseVelocity = velocity;
        CorpseSpin = spin;
        Run.Emit(at, acid, CMUHuggerRun.Acid, 230, 1.1f, 2.4f, angle, 1.9f, 620, FloorY + 1, 14, stains: true, acid: true, inherit: Speed * 0.2f);
        Run.Emit(at, acid / 3, CMUHuggerRun.DarkAcid, 140, 0.9f, 3.2f, angle, 1.2f, 620, FloorY + 1, 14, stains: true, acid: true,
            inherit: Speed * 0.2f);
        Run.Emit(at, 12, CMUHuggerRun.Acid, 280, 0.25f, 1, angle + MathF.PI, 1.4f, kind: CMUHuggerRun.Spark);
        Run.Emit(at, 6, CMUHuggerRun.Acid.WithAlpha(0.4f), 30, 0.9f, 3, -MathF.PI / 2, 1.5f, -20, kind: CMUHuggerRun.Smoke);
        Run.Play(cry);
        Run.Flash(CMUHuggerRun.Acid.WithAlpha(0.35f), 0.3f);
        Run.Shake(4.5f, 0.4f);
        Run.SlowMotion(0.45f);
        Halt();
        Run.Lose(text, CMUHuggerRun.Acid);
    }

    /// <summary>A tracer kills the player: knocked back the way the round was going.</summary>
    protected void ShotDown(Vector2 hit, Vector2 dir, string cry, string text)
    {
        Run.PlayAny(Flesh);
        KillPlayer(hit, dir, new Vector2(dir.X * 70, 170 + Y * 0.5f), -9, cry, text);
    }

    private void MoveCorpse(float dt)
    {
        if (!Corpse)
            return;

        if (!CorpseDown)
        {
            CorpseVelocity.Y -= Gravity * dt;
            CorpseAt += CorpseVelocity * dt;
            CorpseRotation += CorpseSpin * dt;
            if (CorpseAt.Y > 0)
                return;

            CorpseAt.Y = 0;
            CorpseDown = true;
            CorpseRotation = 0;
            Splash();
            return;
        }

        if (_sizzle <= 0)
            return;

        // the acid eats into the deck for a while
        _sizzle -= dt;
        if (Run.Fx() < dt * 20)
        {
            Run.Emit(new Vector2(CorpseAt.X + (Run.Fx() - 0.5f) * 28, FloorY + 3), 1, CMUHuggerRun.Acid.WithAlpha(0.35f), 14, 1.1f, 2.5f,
                -MathF.PI / 2, 0.6f, -25, kind: CMUHuggerRun.Smoke);
        }
    }

    /// <summary>The dead creature hits the floor in a pool of its own acid.</summary>
    private void Splash()
    {
        var x = CorpseAt.X;
        Run.Splat(new Vector2(x, FloorY + 5), 40, CMUHuggerRun.Acid.WithAlpha(0.9f), acid: true);
        Run.Splat(new Vector2(x - 16, FloorY + 10), 18, CMUHuggerRun.Acid.WithAlpha(0.85f), acid: true);
        Run.Splat(new Vector2(x + 14, FloorY + 2), 14, CMUHuggerRun.Acid.WithAlpha(0.85f), acid: true);
        Run.Emit(new Vector2(x, FloorY), 18, CMUHuggerRun.Acid, 140, 0.9f, 1.8f, -MathF.PI / 2, 2.4f, 600, FloorY + 1, 14, stains: true, acid: true);
        Run.Emit(new Vector2(x, FloorY), 6, CMUHuggerRun.Acid.WithAlpha(0.4f), 20, 1.2f, 3, -MathF.PI / 2, 1, -20, kind: CMUHuggerRun.Smoke);
        Run.Play(Squish, -2);
        Run.PlayAny(Sizzles, -3);
        Run.Shake(2.5f, 0.25f);
        _sizzle = 2.4f;
    }

    #endregion

    #region Drawing

    /// <summary>Screen x to world, for spawning effects where something is on screen.</summary>
    protected Vector2 World(float x, float y)
    {
        return new Vector2(x + Scroll, y);
    }

    protected static float MoveTowards(float value, float target, float step)
    {
        return value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);
    }

    public override void DrawBack(DrawingHandleScreen handle)
    {
        DrawCeiling(handle);
        DrawWall(handle);
        DrawFloor(handle);
        DrawLights(handle);
    }

    public override void DrawFront(DrawingHandleScreen handle)
    {
        DrawShadows(handle);
        DrawLockers(handle);
        foreach (var human in Humans)
        {
            if (human.Active)
                DrawHuman(handle, human);
        }

        DrawBlastWarnings(handle);
        DrawCorpse(handle);
        if (!Corpse)
            DrawPlayer(handle);

        DrawForeground(handle);
        DrawFire(handle);
        DrawStreaks(handle);
    }

    /// <summary>Draws the player creature with its feet at (<see cref="PlayerX"/>, <see cref="FloorY"/> - <see cref="Y"/>).</summary>
    protected abstract void DrawPlayer(DrawingHandleScreen handle);

    /// <summary>The player's body while it tumbles, and once it lies dead.</summary>
    protected abstract Texture CorpseTexture(bool landed);

    /// <summary>The size the corpse is drawn at, and how far its feet sit below the sprite's bottom edge.</summary>
    protected abstract float CorpseSize { get; }

    /// <summary>Drawn on a latched host's face, at the same box as the host's sprite. Null for none.</summary>
    protected virtual Texture? FaceMask(CMUHuggerHuman human, RsiDirection direction)
    {
        return null;
    }

    /// <summary>What stands at a panel, from its hash: most panels are bare, with at least two bare panels between any two pieces.</summary>
    private static int Furniture(int panel)
    {
        var kind = (int) (CMUHuggerRun.Hash(panel) % 100);
        if (kind >= 59 || CMUHuggerRun.Hash(panel - 1) % 100 < 59 || CMUHuggerRun.Hash(panel - 2) % 100 < 59)
            return -1;

        return kind;
    }

    /// <summary>The ceiling girder and the service band of pipes under it, both one continuous strip.</summary>
    private void DrawCeiling(DrawingHandleScreen handle)
    {
        const float w = CMUHuggerRun.ScreenWidth;
        var u = Scroll * WallDepth;
        Run.Fill(handle, 0, CMUHuggerRun.HudHeight, w, ServiceTop - CMUHuggerRun.HudHeight, MetalOutline);

        // the girder: a web with a flange under it, and stiffeners with rivets
        Run.Fill(handle, 0, 19, w, 4, MetalDark);
        Run.Fill(handle, 0, 31, w, 5, MetalMid);
        Run.Fill(handle, 0, 31, w, 1, MetalHigh);
        Run.Fill(handle, 0, 36, w, 1, MetalOutline);
        for (var c = (int) MathF.Floor(u / 48); c * 48 - u < w; c++)
        {
            var x = c * 48 - u;
            Run.Fill(handle, x, 23, 3, 8, MetalDark);
            Run.Fill(handle, x + 1, 33, 1, 1, MetalRim);
        }

        // the service band: a big pipe with flanged joints, a small one, and the brackets holding them
        Run.Fill(handle, 0, ServiceTop - 4, w, WallTop - ServiceTop + 4, MetalOutline);
        Run.Fill(handle, 0, 43, w, 7, MetalMid);
        Run.Fill(handle, 0, 43, w, 1, MetalHigh);
        Run.Fill(handle, 0, 49, w, 1, MetalDark);
        Run.Fill(handle, 0, 53, w, 3, MetalDark);
        Run.Fill(handle, 0, 53, w, 1, MetalLight);
        for (var c = (int) MathF.Floor(u / 80); c * 80 - u < w; c++)
        {
            var x = c * 80 - u + 20;
            Run.Fill(handle, x, 41, 4, 11, MetalLight);
            Run.Fill(handle, x, 41, 4, 1, MetalHigh);
        }

        for (var c = (int) MathF.Floor(u / 48); c * 48 - u < w; c++)
        {
            Run.Fill(handle, c * 48 - u + 30, 40, 2, 22, MetalDark);
        }
    }

    /// <summary>Most lamps burn steady; a few in every corridor stutter.</summary>
    private bool LampOn(int lamp)
    {
        if (CMUHuggerRun.Hash(lamp) % 6 != 0)
            return true;

        return MathF.Sin(Run.Clock * 13 + lamp) + MathF.Sin(Run.Clock * 7.3f + lamp * 2) > -0.6f;
    }

    private void DrawWall(DrawingHandleScreen handle)
    {
        const float w = CMUHuggerRun.ScreenWidth;
        var u = Scroll * WallDepth;
        var first = (int) MathF.Floor(u / PanelWidth) - 1;

        // the bands run the whole width, then the plates, then what stands in front, so a door can spill over its panel
        Run.Fill(handle, 0, WallTop, w, FloorY - WallTop, MetalDark);
        Run.Fill(handle, 0, WallTop, w, 1, MetalHigh);
        Run.Fill(handle, 0, WallTop + 1, w, 2, MetalMain);
        Run.Fill(handle, 0, WallTop + 3, w, 1, MetalOutline);
        for (var p = first; p * PanelWidth - u < w; p++)
        {
            DrawPlate(handle, p, p * PanelWidth - u);
        }

        DrawLowerWall(handle, u);
        for (var p = first; p * PanelWidth - u < w + PanelWidth; p++)
        {
            DrawFurniture(handle, p, p * PanelWidth - u);
        }
    }

    /// <summary>One bevelled wall plate with rivets at its corners, and now and then a groove, a grille, a split or some grime.</summary>
    private void DrawPlate(DrawingHandleScreen handle, int panel, float x)
    {
        const float top = PlateTop;
        const float bottom = RailY - 2;
        Run.Fill(handle, x + 2, top, PanelWidth - 4, bottom - top, CMUHuggerRun.Hash01(panel, 24) < 0.3f ? MetalShade : MetalMain);
        Run.Fill(handle, x + 2, top, PanelWidth - 4, 1, MetalHigh);
        Run.Fill(handle, x + 2, top, 1, bottom - top, MetalLight);
        Run.Fill(handle, x + 2, bottom - 1, PanelWidth - 4, 1, MetalOutline);
        Run.Fill(handle, x + PanelWidth - 3, top, 1, bottom - top, MetalOutline);
        var v = CMUHuggerRun.Hash01(panel, 21);
        if (v < 0.45f)
        {
            Run.Fill(handle, x + 3, 104, PanelWidth - 6, 1, MetalOutline);
            Run.Fill(handle, x + 3, 105, PanelWidth - 6, 1, MetalLight);
        }
        else if (v < 0.65f)
        {
            Run.Fill(handle, x + 14, 116, 36, 22, MetalDark);
            for (var k = 0; k < 5; k++)
            {
                Run.Fill(handle, x + 16, 118 + k * 4, 32, 2, MetalOutline);
            }
        }
        else if (v < 0.82f)
        {
            Run.Fill(handle, x + 31, top + 1, 1, bottom - top - 2, MetalOutline);
            Run.Fill(handle, x + 32, top + 1, 1, bottom - top - 2, MetalLight);
        }
        else
        {
            Run.Fill(handle, x + 3, 118, PanelWidth - 6, bottom - 119, MetalOutline.WithAlpha(0.35f));
            Run.Fill(handle, x + 12 + CMUHuggerRun.Hash01(panel, 22) * 36, 100, 1, 30, MetalOutline.WithAlpha(0.5f));
        }

        Rivet(handle, x + 5, top + 3);
        Rivet(handle, x + PanelWidth - 7, top + 3);
        Rivet(handle, x + 5, bottom - 5);
        Rivet(handle, x + PanelWidth - 7, bottom - 5);
    }

    private void Rivet(DrawingHandleScreen handle, float x, float y)
    {
        Run.Fill(handle, x, y, 1, 1, MetalHigh);
        Run.Fill(handle, x + 1, y + 1, 1, 1, MetalOutline);
    }

    /// <summary>The handrail, the ribbed lower wall and the skirting, continuous along the corridor.</summary>
    private void DrawLowerWall(DrawingHandleScreen handle, float u)
    {
        const float w = CMUHuggerRun.ScreenWidth;
        Run.Fill(handle, 0, RailY, w, LowerTop - RailY, MetalOutline);
        Run.Fill(handle, 0, RailY + 2, w, 2, MetalRim);
        Run.Fill(handle, 0, RailY + 2, w, 1, Green.WithAlpha(0.25f));
        Run.Fill(handle, 0, LowerTop, w, SkirtTop - LowerTop, MetalMid);
        Run.Fill(handle, 0, LowerTop, w, 1, MetalOutline);
        for (var c = (int) MathF.Floor(u / 8); c * 8 - u < w; c++)
        {
            Run.Fill(handle, c * 8 - u, LowerTop + 1, 1, SkirtTop - LowerTop - 1, MetalDark);
        }

        Run.Fill(handle, 0, SkirtTop, w, FloorY - SkirtTop, MetalDark);
        Run.Fill(handle, 0, SkirtTop, w, 1, MetalHigh);
        for (var c = (int) MathF.Floor(u / PanelWidth); c * PanelWidth - u < w; c++)
        {
            var x = c * PanelWidth - u;
            Run.Fill(handle, x + 31, RailY + 4, 2, 4, MetalLight);
            Run.Fill(handle, x + 8, 178, 2, 2, MetalLight);
            Run.Fill(handle, x + 40, 178, 2, 2, MetalLight);
            if (CMUHuggerRun.Hash01(c, 23) >= 0.15f)
                continue;

            for (var k = 0; k < 8; k++)
            {
                var sx = x + k * 8 + 4;
                Run.Quad(handle, new Vector2(sx, SkirtTop + 2), new Vector2(sx + 4, SkirtTop + 2), new Vector2(sx, FloorY - 1),
                    new Vector2(sx - 4, FloorY - 1), CMUHuggerRun.Amber.WithAlpha(0.3f));
            }
        }
    }

    /// <summary>What stands at a panel: doors and lockers on the deck, signs at eye level, vents and displays up high.</summary>
    private void DrawFurniture(DrawingHandleScreen handle, int panel, float x)
    {
        var kind = Furniture(panel);
        if (kind < 0)
            return;

        var hash = CMUHuggerRun.Hash(panel);
        var centre = x + 32;
        var frame = (int) (Run.Clock / 0.12f);
        if (kind < 9)
        {
            // an airlock door set into a recess in the wall, a little taller than a person
            Run.Fill(handle, centre - 58, FloorY - 115, 116, 115, MetalOutline);
            Run.Fill(handle, centre - 58, FloorY - 115, 116, 1, MetalHigh);
            Run.DrawSprite(handle, Run.Frame(Doors[(hash >> 10) % (uint) Doors.Length], "closed"), new Vector2(centre, FloorY - DoorSize / 2),
                new Vector2(DoorSize, DoorSize), 0, PropTint);
            var light = (hash >> 8) % 3 == 0 ? CMUHuggerRun.Alarm : Green;
            Run.Circle(handle, new Vector2(centre + 46, FloorY - 104), 1.6f, light.WithAlpha(Run.Clock % 1.2f < 0.8f ? 1 : 0.3f));
        }
        else if (kind < 18)
        {
            // a viewport onto the colony outside
            Run.Fill(handle, x + 4, 72, 56, 54, MetalOutline);
            DrawOutside(handle, x + 7, 75, 50, 48);
            Run.Fill(handle, x + 31.5f, 75, 1, 48, MetalMid);
            Run.Outline(handle, x + 6, 74, 52, 50, MetalHigh);
            Run.Quad(handle, new Vector2(x + 12, 75), new Vector2(x + 18, 75), new Vector2(x + 10, 92), new Vector2(x + 8, 92), Green.WithAlpha(0.07f));
        }
        else if (kind < 24)
        {
            // a locker stood on the deck: its body, then its door; the body ends 30 px down its sprite
            var closet = Closets[(hash >> 8) % (uint) Closets.Length];
            var at = new Vector2(centre, FloorY - 14 * 3);
            Run.DrawSprite(handle, Run.Frame(closet, "base"), at, new Vector2(96, 96), 0, PropTint);
            Run.DrawSprite(handle, Run.Frame(closet, "closed"), at, new Vector2(96, 96), 0, PropTint);
        }
        else if (kind < 29)
        {
            Run.DrawSprite(handle, Run.Frame(Computer, "computer_generic", RsiDirection.South, frame / 4), new Vector2(centre, FloorY - 38.5f),
                new Vector2(77, 77), 0, PropTint);
        }
        else if (kind < 35)
        {
            // a crate on the deck; its sprite ends 26 px down
            Run.DrawSprite(handle, Run.Frame(Crates[(hash >> 8) % (uint) Crates.Length], "closed"), new Vector2(centre, FloorY - 10 * 2.2f),
                new Vector2(70, 70), 0, PropTint);
        }
        else if (kind < 43)
        {
            if ((hash >> 8) % 2 == 0)
            {
                Run.DrawSprite(handle, Run.Frame(Signs, SignStates[(hash >> 9) % (uint) SignStates.Length]), new Vector2(centre + 4, 96),
                    new Vector2(44, 44), 0, PropTint);
            }
            else
            {
                _signs ??= new[]
                {
                    Loc.GetString("cmu-game-hugger-sign-0"), Loc.GetString("cmu-game-hugger-sign-1"), Loc.GetString("cmu-game-hugger-sign-2"),
                    Loc.GetString("cmu-game-hugger-sign-3"), Loc.GetString("cmu-game-hugger-sign-4"), Loc.GetString("cmu-game-hugger-sign-5"),
                };
                Run.Fill(handle, x + 12, 88, 40, 14, Color.Black);
                Run.Outline(handle, x + 12, 88, 40, 14, Green.WithAlpha(0.8f));
                Run.CentreText(handle, Run.SignFont, _signs[(hash >> 9) % (uint) _signs.Length], centre, 91, Green);
            }
        }
        else if (kind < 49)
        {
            Run.DrawSprite(handle, Run.Frame(Scrubber, "on", RsiDirection.South, frame), new Vector2(centre, 84), new Vector2(44, 44), 0, PropTint);
        }
        else if (kind < 53)
        {
            Run.DrawSprite(handle, Run.Frame(Posters, PosterStates[(hash >> 8) % (uint) PosterStates.Length]), new Vector2(centre, 100),
                new Vector2(48, 48), 0, PropTint);
        }
        else if (kind < 56)
        {
            Run.DrawSprite(handle, Run.Frame(Display, (hash >> 8) % 2 == 0 ? "redalert_framed" : "default", RsiDirection.South, frame),
                new Vector2(centre, 88), new Vector2(52, 52), 0, PropTint);
        }
        else
        {
            // somebody already died here
            Run.DrawSprite(handle, Run.SplatterFrame((int) ((hash >> 9) % 6)), new Vector2(centre, 126), new Vector2(30, 30),
                (hash >> 12) % 628 / 100f, CMUHuggerRun.DarkBlood.WithAlpha(0.65f));
            Run.Fill(handle, centre - 2, 136, 1.5f, 14, CMUHuggerRun.DarkBlood.WithAlpha(0.5f));
            Run.Fill(handle, centre + 4, 134, 1, 9, CMUHuggerRun.DarkBlood.WithAlpha(0.45f));
        }
    }

    /// <summary>The colony outside a window: stars, rain and hills, far enough away to crawl past.</summary>
    private void DrawOutside(DrawingHandleScreen handle, float x, float y, float width, float height)
    {
        Run.Fill(handle, x, y, width, height, Sky);
        const float cell = 11;
        var u = Scroll * OutsideDepth;
        var last = (int) MathF.Floor((x + width + u) / cell);
        for (var c = (int) MathF.Floor((x + u) / cell); c <= last; c++)
        {
            for (var k = 0; k < 2; k++)
            {
                var sx = c * cell + CMUHuggerRun.Hash01(c, k * 2 + 1) * cell - u;
                if (sx < x + 1 || sx > x + width - 2)
                    continue;

                var sy = y + 2 + CMUHuggerRun.Hash01(c, k * 2 + 2) * height * 0.6f;
                var twinkle = 0.4f + 0.6f * MathF.Abs(MathF.Sin(Run.Clock * (1 + CMUHuggerRun.Hash01(c, 9) * 3) + c));
                Run.Fill(handle, sx, sy, 1, 1, Green.WithAlpha((0.2f + 0.6f * CMUHuggerRun.Hash01(c, k + 5)) * twinkle));
            }
        }

        var hills = Scroll * HillDepth;
        for (var cx = x; cx < x + width; cx += 2)
        {
            var ridge = Ridge(cx + hills);
            Run.Fill(handle, cx, y + height - ridge, MathF.Min(2, x + width - cx), ridge, Hills);
        }

        for (var k = 0; k < 5; k++)
        {
            var rx = x + (k * 9.7f + Run.Clock * 14) % width;
            var ry = y + (k * 23 + Run.Clock * 120) % (height - 6);
            Run.Line(handle, new Vector2(rx, ry), new Vector2(MathF.Max(x, rx - 2), ry + 5), Green.WithAlpha(0.18f));
        }
    }

    private static float Ridge(float u)
    {
        var i = (int) MathF.Floor(u / 18);
        var f = u / 18 - i;
        f = f * f * (3 - 2 * f);
        var a = CMUHuggerRun.Hash01(i, 3);
        return 6 + 16 * (a + (CMUHuggerRun.Hash01(i + 1, 3) - a) * f);
    }

    private void DrawFloor(DrawingHandleScreen handle)
    {
        Run.Fill(handle, 0, FloorY, CMUHuggerRun.ScreenWidth, CMUHuggerRun.ScreenHeight - FloorY, MetalOutline);
        var floor = Run.Image(FloorTile);
        var catwalk = Run.Image(CatwalkTile);

        // two rows of deck plates seen at a slant, the near row lighter and offset half a plate
        for (var row = 0; row < 2; row++)
        {
            var offset = row * 20;
            var y = FloorY + 2 + row * 19;
            for (var c = (int) MathF.Floor((Scroll + offset) / 40) - 1; c * 40 - Scroll - offset < CMUHuggerRun.ScreenWidth; c++)
            {
                var x = c * 40 - Scroll - offset;
                var tile = CMUHuggerRun.Hash(c * 2 + row) % 7 == 0 ? catwalk : floor;
                Run.DrawSprite(handle, tile, new Vector2(x + 20, y + 9.5f), new Vector2(40, 19), 0, row == 0 ? FloorFar : FloorNear);
            }
        }

        // the deck's edge against the wall, and the wall's shadow on the deck
        Run.Fill(handle, 0, FloorY, CMUHuggerRun.ScreenWidth, 2, MetalRim);
        Run.Fill(handle, 0, FloorY, CMUHuggerRun.ScreenWidth, 1, Green.WithAlpha(0.5f));
        Run.Fill(handle, 0, FloorY + 2, CMUHuggerRun.ScreenWidth, 4, Color.Black.WithAlpha(0.4f));
    }

    /// <summary>Each strip light: its fitting, a halo on the wall, a cone of light, and a pool of it on the deck.</summary>
    private void DrawLights(DrawingHandleScreen handle)
    {
        var u = Scroll * WallDepth;
        for (var c = (int) MathF.Floor((u - 30) / 96) - 1; c * 96 + 30 - u < CMUHuggerRun.ScreenWidth + 60; c++)
        {
            var x = c * 96 + 30 - u;
            var centre = x + 13;
            var on = LampOn(c);
            Run.Fill(handle, x - 1, 35, 28, 5, MetalDark);
            Run.Fill(handle, x - 1, 35, 28, 1, MetalHigh);
            Run.Fill(handle, x + 1, 38, 24, 2, on ? TubeLit : MetalMid);
            if (!on)
                continue;

            Run.Ellipse(handle, new Vector2(centre, 42), 30, 9, Green.WithAlpha(0.05f));
            Run.Ellipse(handle, new Vector2(centre, 41), 16, 4, Green.WithAlpha(0.07f));
            Run.Quad(handle, new Vector2(x + 1, 40), new Vector2(x + 25, 40), new Vector2(x + 66, FloorY), new Vector2(x - 40, FloorY), Green.WithAlpha(0.035f));
            Run.Ellipse(handle, new Vector2(centre, FloorY + 9), 54, 8, Green.WithAlpha(0.07f));
            Run.Ellipse(handle, new Vector2(centre, FloorY + 8), 26, 4, Green.WithAlpha(0.07f));
        }
    }

    /// <summary>Railings and cable runs nearer than the deck, at its bottom edge, moving past faster.</summary>
    private void DrawForeground(DrawingHandleScreen handle)
    {
        var u = Scroll * ForegroundDepth;
        for (var c = (int) MathF.Floor(u / 150) - 1; c * 150 - u < CMUHuggerRun.ScreenWidth + 40; c++)
        {
            var kind = CMUHuggerRun.Hash(c + 101) % 4;
            var x = c * 150 - u + CMUHuggerRun.Hash01(c, 41) * 60;
            if (kind == 0)
            {
                // a railing: two posts and a bar
                Run.Fill(handle, x, 209, 70, 3, MetalOutline);
                Run.Fill(handle, x, 209, 70, 1, MetalMid);
                for (var post = 0; post < 2; post++)
                {
                    var px = x + post * 66;
                    Run.Fill(handle, px, 206, 5, CMUHuggerRun.ScreenHeight - 206, MetalOutline);
                    Run.Fill(handle, px, 206, 5, 1, MetalMid);
                }
            }
            else if (kind == 1)
            {
                // a cable bundle along the deck edge
                for (var k = 0; k < 3; k++)
                {
                    for (var sx = 0; sx < 90; sx += 3)
                    {
                        Run.Fill(handle, x + sx, 218 + k * 2 + MathF.Sin((sx + c * 7) / 14f + k) * 1.5f, 3, 2, k == 1 ? MetalMid : MetalOutline);
                    }
                }
            }
        }
    }

    private void DrawShadows(DrawingHandleScreen handle)
    {
        var shade = Color.Black.WithAlpha(0.45f);
        foreach (var human in Humans)
        {
            if (!human.Active)
                continue;

            var x = human.X - Scroll;
            if (human.State == CMUHuggerHumanState.Down)
                Run.Ellipse(handle, new Vector2(x + human.Fall * 46, FloorY + 3), 50, 5, shade);
            else
                Run.Ellipse(handle, new Vector2(x, FloorY + 1), 18, 4, shade);
        }

        if (!Corpse && !Halted)
        {
            var lift = MathF.Min(Y, 120) / 120;
            Run.Ellipse(handle, new Vector2(PlayerX, FloorY + 1), (HalfWidth + 3) * (1 - lift * 0.45f), 3.5f, Color.Black.WithAlpha(0.45f * (1 - lift * 0.6f)));
        }

        if (Corpse)
            Run.Ellipse(handle, new Vector2(CorpseAt.X - Scroll, FloorY + 1), HalfWidth, 3, shade);
    }

    private void DrawHuman(DrawingHandleScreen handle, CMUHuggerHuman human)
    {
        var x = human.X - Scroll;
        if (x < -90 || x > CMUHuggerRun.ScreenWidth + 90)
            return;

        Pose(human, out var rotation, out var lean, out var bob);
        var direction = human.East ? RsiDirection.East : RsiDirection.West;
        var feet = new Vector2(x + lean, FloorY - bob);
        Run.DrawPerson(handle, human.Person, feet, direction, HumanScale, rotation);
        if (human.Knifeman && human.State is CMUHuggerHumanState.WindUp or CMUHuggerHumanState.Stab)
            DrawKnife(handle, human, feet);

        if (FaceMask(human, direction) is not { } mask)
            return;

        // the mask is drawn in the host's own box, so it turns with the body
        Run.DrawSprite(handle, mask, feet + Rotate(new Vector2(0, -HumanScale * 16), rotation), new Vector2(32 * HumanScale, 32 * HumanScale),
            rotation, CMUHuggerRun.Phosphor);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    /// <summary>The bayonet raised overhead on the wind-up, then driven down and forward along the floor on the stab.</summary>
    private void DrawKnife(DrawingHandleScreen handle, CMUHuggerHuman human, Vector2 feet)
    {
        var knife = Run.KnifeIcon;
        if (human.State == CMUHuggerHumanState.WindUp)
        {
            var k = human.Timer / WindUpTime;
            var at = feet + new Vector2(-6 + 10 * k, -78 - 10 * k);
            Run.DrawSprite(handle, knife, at, new Vector2(30, 30), -2.2f - k * 0.6f, CMUHuggerRun.Phosphor);
            if (Run.Clock % 0.16f < 0.08f)
                Run.Circle(handle, at + new Vector2(-6, -8), 2.5f, Color.White.WithAlpha(0.8f));
            return;
        }

        var t = Math.Clamp(human.Timer / StabTime, 0, 1);
        var tip = feet + new Vector2(-StabReachNear - (StabReachFar - StabReachNear) * t, -StabHeight + 18 * t);
        Run.DrawSprite(handle, knife, tip + new Vector2(10, -6), new Vector2(30, 30), 0.6f, CMUHuggerRun.Phosphor);
        Run.Beam(handle, feet + new Vector2(-6, -70), tip, 3, Color.White.WithAlpha(0.35f * (1 - t)));
    }

    /// <summary>How a human stands this frame: rotation about the feet in radians, a sideways lean and a bob, in units.</summary>
    private void Pose(CMUHuggerHuman human, out float rotation, out float lean, out float bob)
    {
        rotation = 0;
        lean = 0;
        bob = 0;
        switch (human.State)
        {
            case CMUHuggerHumanState.Walk:
                bob = MathF.Abs(MathF.Sin(human.Step)) * 3;
                break;
            case CMUHuggerHumanState.Flee:
                bob = MathF.Abs(MathF.Sin(human.Step)) * 4;
                rotation = 0.12f;
                break;
            case CMUHuggerHumanState.Stand:
            case CMUHuggerHumanState.Aim:
                bob = human.Rifleman && Playing
                    ? MathF.Abs(MathF.Sin(human.Step)) * 2.5f
                    : 0.8f + MathF.Sin(Run.Clock * 2 + human.X) * 0.8f;
                break;
            case CMUHuggerHumanState.Alert:
                lean = (Run.Fx() - 0.5f) * 2;
                rotation = 0.06f;
                break;
            case CMUHuggerHumanState.WindUp:
                rotation = 0.12f * human.Timer / WindUpTime;
                break;
            case CMUHuggerHumanState.Stab:
                var stab = Math.Clamp(human.Timer / StabTime, 0, 1);
                rotation = 0.12f - 0.4f * stab;
                lean = -14 * stab;
                break;
            case CMUHuggerHumanState.Recover:
                var back = 1 - Math.Clamp(human.Timer / RecoverTime, 0, 1);
                rotation = -0.28f * back;
                lean = -14 * back;
                break;
            case CMUHuggerHumanState.Kick:
                var kick = MathF.Sin(MathF.Min(1, human.Timer / KickTime) * MathF.PI);
                rotation = (human.East ? 0.18f : -0.18f) * kick;
                lean = (human.East ? 5 : -8) * kick;
                break;
            case CMUHuggerHumanState.Latched:
                if (human.Timer < Struggle)
                {
                    rotation = MathF.Sin(human.Timer * 40) * 0.08f;
                    break;
                }

                // goes over backwards, slow at first and then all at once, settling onto the floor
                var fall = Math.Clamp((human.Timer - Struggle) / FallTime, 0, 1);
                rotation = human.Fall * MathF.PI / 2 * fall * fall;
                bob = -10 * fall * fall;
                break;
            case CMUHuggerHumanState.Shot:
                var drop = Math.Clamp(human.Timer / (FallTime * 0.8f), 0, 1);
                rotation = human.Fall * MathF.PI / 2 * drop * drop;
                bob = -10 * drop * drop;
                break;
            case CMUHuggerHumanState.Down:
                rotation = human.Fall * MathF.PI / 2 + Twitch(human);
                bob = -10;
                break;
        }
    }

    /// <summary>The small jerks of a body on the floor; stages can make a host twitch harder.</summary>
    protected virtual float Twitch(CMUHuggerHuman human)
    {
        return human.Timer < 1.5f ? MathF.Sin(human.Timer * 50) * 0.025f * (1 - human.Timer / 1.5f) : 0;
    }

    private void DrawCorpse(DrawingHandleScreen handle)
    {
        if (!Corpse)
            return;

        var x = CorpseAt.X - Scroll;
        var feet = FloorY - CorpseAt.Y;
        var size = new Vector2(CorpseSize, CorpseSize);
        Run.DrawSprite(handle, CorpseTexture(CorpseDown), new Vector2(x, feet - CorpseSize / 2 + CorpseSize * 0.06f), size,
            CorpseDown ? 0 : CorpseRotation, CMUHuggerRun.Phosphor);
    }

    /// <summary>Laser sights on the lanes about to be fired down, riflemen's aim, tracers and muzzle flashes.</summary>
    private void DrawFire(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _shotCount; i++)
        {
            var shot = _shots[i];
            if (shot.Delay >= Warning)
                continue;

            var k = 1 - shot.Delay / Warning;
            var flicker = 0.75f + 0.25f * MathF.Sin(Run.Clock * 60 + i);
            Run.Fill(handle, CMUHuggerRun.ScreenWidth - 90 * k, shot.Lane - 0.5f, 90 * k, 1, CMUHuggerRun.Laser.WithAlpha(0.3f * k * flicker));
            Run.Circle(handle, new Vector2(CMUHuggerRun.ScreenWidth - 1, shot.Lane), 2 + 7 * k, CMUHuggerRun.TracerGlow.WithAlpha((0.15f + 0.35f * k) * flicker));
        }

        foreach (var human in Humans)
        {
            if (!human.Active || human.State != CMUHuggerHumanState.Aim || !Playing)
                continue;

            var muzzle = Barrel(human.X - Scroll);
            var aim = MathF.Min(1, human.Timer / MarineAim);
            Run.Line(handle, muzzle, Vector2.Lerp(muzzle, Target(), aim), CMUHuggerRun.Laser.WithAlpha(0.35f + 0.3f * MathF.Sin(Run.Clock * 50)));
            if (aim >= 1)
                Run.Circle(handle, Target(), 1.5f, CMUHuggerRun.Laser);
        }

        for (var i = 0; i < _bulletCount; i++)
        {
            var bullet = _bullets[i];
            var tail = bullet.At - Vector2.Normalize(bullet.Velocity) * Tracer;
            Run.Beam(handle, tail, bullet.At, 5, CMUHuggerRun.TracerGlow.WithAlpha(0.3f));
            Run.Beam(handle, tail, bullet.At, 2, CMUHuggerRun.TracerCore);
            Run.Fill(handle, bullet.At.X - 1, bullet.At.Y - 1, 2, 2, Color.White);
        }

        for (var i = 0; i < _flashCount; i++)
        {
            var flash = _flashes[i];
            var k = 1 - flash.Age / 0.07f;
            Run.Circle(handle, flash.At, 7 * k + 2, CMUHuggerRun.TracerGlow.WithAlpha(0.5f * k));
            Run.Circle(handle, flash.At, 3 * k + 1, CMUHuggerRun.TracerCore.WithAlpha(k));
            Run.Beam(handle, flash.At + new Vector2(-12 * k, 0), flash.At + new Vector2(4, 0), 2, CMUHuggerRun.TracerCore.WithAlpha(k));
            Run.Beam(handle, flash.At + new Vector2(0, -6 * k), flash.At + new Vector2(0, 6 * k), 1.5f, CMUHuggerRun.TracerGlow.WithAlpha(k));
        }
    }

    /// <summary>Speed lines once the run gets going.</summary>
    private void DrawStreaks(DrawingHandleScreen handle)
    {
        if (!Playing || Speed < 250)
            return;

        var alpha = (Speed - 250) / (MaxSpeed - 250) * 0.18f;
        const float cell = 70;
        var u = Scroll * 1.25f;
        for (var c = (int) MathF.Floor(u / cell); c * cell - u < CMUHuggerRun.ScreenWidth; c++)
        {
            if ((CMUHuggerRun.Hash(c) & 3) != 0)
                continue;

            var x = c * cell - u + CMUHuggerRun.Hash01(c, 1) * cell;
            Run.Fill(handle, x, 50 + CMUHuggerRun.Hash01(c, 2) * 120, 20 + CMUHuggerRun.Hash01(c, 3) * 40, 1, CMUHuggerRun.Bright.WithAlpha(alpha));
        }
    }

    #endregion
}
