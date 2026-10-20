using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Tracker Reflex: an M314 motion tracker played on the keypad. The display is a 3 by 3 grid with you in the middle
/// (7 8 9 on top, 1 2 3 at the bottom). Xenos close in from every side, but a blip only shows where the last ping
/// saw it, and the ping quickens as they get closer. A key fires a pulse rifle burst into its sector, 5 point blank
/// into the centre, Space or Enter reloads. A xeno that reaches the middle mauls you; the third one ends the run.
/// Waves get bigger and faster until then.
/// </summary>
public sealed class CMUTrackerReflex : CMUArcadeScreen
{
    #region Tuning

    // the screen in base units (the control is 320 by 224 before UI scaling) and the display on it
    private const float CentreX = 160;
    private const float CentreY = 124;
    private const float Radius = 88;

    // the display is the unit disc: sector lines at a third, xenos come in from just past the rim
    private const float Third = 1f / 3;
    private const float Tolerance = 0.05f;
    private const float SpawnRadius = 1.04f;
    private const float MaulRadius = 0.1f;
    private const float RangeMetres = 30;

    private const float PingTravel = 0.45f;
    private const float PingFar = 1.5f;
    private const float PingNear = 0.4f;
    private const float PingIdle = 1.9f;
    private const float BlipFade = 1.4f;
    private const float EchoTime = 0.6f;

    private const int MaxHealth = 3;
    private const int Magazine = 10;
    private const int RoundsPerBurst = 4;
    private const float ReloadTime = 1.25f;
    private const float FireCooldown = 0.11f;
    private const float Invulnerable = 1.1f;

    private const int KillPoints = 10;
    private const int PointBlankPoints = 15;
    private const int MissPenalty = 5;
    private const int WavePoints = 50;
    private const int HealthPoints = 25;
    private const int StreakStep = 5;
    private const int MaxMultiplier = 4;

    private const float IntroTime = 1.8f;
    private const float ClearTime = 2.4f;
    private const float DyingTime = 1.6f;
    private const int DemoContacts = 5;
    private const float DemoGap = 1.4f;

    private const float BannerWidth = 176;

    private const int MaxDecals = 90;
    private const int MaxGlass = 40;
    private const int MaxStreaks = 18;
    private const int MaxParticles = 320;

    #endregion

    #region Assets

    private static readonly ResPath SplatterRsi = new("/Textures/Fluids/splatter.rsi");
    private static readonly ResPath DetectorRsi = new("/Textures/_RMC14/Objects/Tools/motion_detector.rsi");
    private const int SplatterStates = 6;

    private const string PingContact = "/Audio/_RMC14/Effects/motion_detector.ogg";
    private const string PingEmpty = "/Audio/_RMC14/Effects/motion_detector_none.ogg";
    private const string Shot = "/Audio/_RMC14/Weapons/Guns/Gunshots/gun_m54c_1.ogg";
    private const string DryFire = "/Audio/_RMC14/Weapons/Guns/gun_cmb_click1.ogg";
    private const string Unload = "/Audio/_RMC14/Weapons/Guns/Reload/m41_unload.ogg";
    private const string Load = "/Audio/_RMC14/Weapons/Guns/Reload/m41_reload.ogg";
    private const string PowerOn = "/Audio/_RMC14/Handling/hud_on.ogg";
    private const string Roar = "/Audio/_RMC14/Xeno/alien_distantroar_3.ogg";
    private const string Cleared = "/Audio/Effects/double_beep.ogg";
    private const string Static = "/Audio/_RMC14/Effects/radiostatic.ogg";
    private const string BruteDeath = "/Audio/_RMC14/Xeno/alien_death.ogg";

    private static readonly string[] Deaths =
    {
        "/Audio/_RMC14/Xeno/alien_death2.ogg", "/Audio/_RMC14/Xeno/alien_roar_larva1.ogg",
        "/Audio/_RMC14/Xeno/alien_roar_larva2.ogg",
    };

    private static readonly string[] Sizzles =
    {
        "/Audio/_RMC14/Xeno/acid_sizzle1.ogg", "/Audio/_RMC14/Xeno/acid_sizzle2.ogg",
        "/Audio/_RMC14/Xeno/acid_sizzle3.ogg", "/Audio/_RMC14/Xeno/acid_sizzle4.ogg",
    };

    private static readonly string[] Flesh =
    {
        "/Audio/_RMC14/Bullets/impact_flesh_1.ogg", "/Audio/_RMC14/Bullets/impact_flesh_2.ogg",
        "/Audio/_RMC14/Bullets/impact_flesh_3.ogg", "/Audio/_RMC14/Bullets/impact_flesh_4.ogg",
    };

    private static readonly string[] Hisses =
    {
        "/Audio/_RMC14/Xeno/alien_hiss1.ogg", "/Audio/_RMC14/Xeno/alien_hiss2.ogg", "/Audio/_RMC14/Xeno/alien_hiss3.ogg",
    };

    private static readonly string[] Growls =
    {
        "/Audio/_RMC14/Xeno/alien_growl1.ogg", "/Audio/_RMC14/Xeno/alien_growl2.ogg", "/Audio/_RMC14/Xeno/alien_growl3.ogg",
    };

    private static readonly string[] Charges =
    {
        "/Audio/_RMC14/Xeno/runner_charging_1.ogg", "/Audio/_RMC14/Xeno/runner_charging_2.ogg",
    };

    private static readonly string[] Claws =
    {
        "/Audio/_RMC14/Xeno/alien_claw_flesh1.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh2.ogg",
        "/Audio/_RMC14/Xeno/alien_claw_flesh3.ogg", "/Audio/_RMC14/Xeno/alien_claw_flesh4.ogg",
        "/Audio/_RMC14/Xeno/alien_claw_flesh5.ogg",
    };

    private static readonly string[] Bites =
    {
        "/Audio/_RMC14/Xeno/alien_bite1.ogg", "/Audio/_RMC14/Xeno/alien_bite2.ogg",
    };

    private static readonly Color Face = Color.FromHex("#031006");
    private static readonly Color Bezel = Color.FromHex("#0A1A0D");
    private static readonly Color Hot = Color.FromHex("#E8FFD8");
    private static readonly Color Warn = Color.FromHex("#FF4433");
    private static readonly Color Acid = Color.FromHex("#C6F03A");
    private static readonly Color AcidStain = Color.FromHex("#9FD02A").WithAlpha(0.7f);
    private static readonly Color Blood = Color.FromHex("#A10D0D").WithAlpha(0.85f);
    private static readonly Color BloodDark = Color.FromHex("#6A0606").WithAlpha(0.9f);

    private static readonly string[] Digits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    #endregion

    private enum Phase : byte
    {
        Attract,
        Intro,
        Wave,
        Clear,
        Dying,
        Over,
    }

    private enum Kind : byte
    {
        /// <summary>Stalks in with a stop-start pace.</summary>
        Drone,

        /// <summary>Small and fast.</summary>
        Runner,

        /// <summary>Weaves from side to side, so it slips between sectors.</summary>
        Lurker,

        /// <summary>Big and slow; takes two bursts.</summary>
        Brute,
    }

    private struct Xeno
    {
        public Kind Kind;
        public float Angle;
        public float Distance;
        public float Speed;
        public float Phase;
        public int Health;

        /// <summary>Where the last ping (or muzzle flash) saw it: the display only knows that.</summary>
        public Vector2 Shown;

        public float ShownAge;
        public bool Pinged;
        public bool Lunging;
        public float Hurt;

        public readonly Vector2 At => new(MathF.Cos(Angle) * Distance, MathF.Sin(Angle) * Distance);
    }

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public float Drag;
        public Color Colour;

        /// <summary>On the glass in front of the display: it does not shake with the screen.</summary>
        public bool Glass;

        /// <summary>Leaves a drop where it comes to rest.</summary>
        public bool Lands;
    }

    private struct Decal
    {
        public Vector2 Position;
        public float Size;
        public int State;
        public float Rotation;
        public Color Colour;
    }

    private struct Drip
    {
        public Vector2 Top;
        public float Length;
        public float Max;
        public float Speed;
        public float Width;
    }

    private struct Ring
    {
        public Vector2 Position;
        public float Age;
        public float Life;
        public float Size;
        public Color Colour;
    }

    private struct Popup
    {
        public string Text;
        public Vector2 Position;
        public float Age;
        public Color Colour;
    }

    /// <summary>Each sector's outline on the unit disc (the grid cell clipped to the display), and its middle.</summary>
    private static readonly Vector2[][] Cells = new Vector2[10][];

    private static readonly Vector2[] CellCentres = new Vector2[10];
    private const int CellEdge = 8;

    private readonly Random _random = new();
    private readonly List<Xeno> _xenos = new();
    private readonly List<Particle> _particles = new();
    private readonly List<Decal> _acid = new();
    private readonly List<Decal> _glass = new();
    private readonly List<(Vector2 From, Vector2 To, float Width)> _streaks = new();
    private readonly List<Drip> _drips = new();
    private readonly List<Ring> _rings = new();
    private readonly List<Popup> _popups = new();
    private readonly float[] _keyFlash = new float[10];
    private readonly Vector2[] _tracers = new Vector2[3];
    private readonly (Vector2 From, Vector2 Direction)[] _claws = new (Vector2, Vector2)[3];

    private readonly Font _small;
    private readonly Font _medium;
    private readonly Font _big;
    private Texture[]? _splatter;
    private Texture[]? _echo;

    private readonly string _textModel;
    private readonly string _textTitle;
    private readonly string _textStart;
    private readonly string _textRetry;
    private readonly string _textPaused;
    private readonly string _textWave;
    private readonly string _textInbound;
    private readonly string _textClear;
    private readonly string _textLost;
    private readonly string _textVitals;
    private readonly string _textAmmo;
    private readonly string _textReload;
    private readonly string _textReloading;
    private readonly string _textStreak;
    private readonly string _textRange;
    private readonly string _textNone;
    private readonly string _textKeypad;
    private readonly string _textMiss;
    private readonly string _textClose;
    private readonly string _textDanger;

    private Phase _phase = Phase.Attract;
    private float _phaseTimer;
    private float _time;
    private float _freeze;
    private bool _paused;
    private bool _female;

    private int _wave;
    private int _toSpawn;
    private float _spawnTimer;
    private float _lastSpawnAngle;

    private float _pingAge;
    private float _pingPeriod = PingIdle;
    private bool _danger;

    private int _health;
    private int _ammo;
    private float _reload;
    private float _cooldown;
    private float _invulnerable;
    private int _streak;
    private int _kills;

    private float _shakeTime;
    private float _shakeDuration = 1;
    private float _shakePower;
    private Vector2 _shake;
    private float _redFlash;
    private float _clawAge = 99;
    private float _ammoWarn;

    private int _flashSector;
    private float _flashAge = 99;
    private bool _flashHit;
    private Vector2 _flashTo;

    private string? _banner;
    private string? _bannerSub;
    private float _bannerAge;
    private float _bannerLife;

    private string _ammoText = string.Empty;
    private string _waveText = string.Empty;
    private string _rangeText = string.Empty;
    private string _multiplierText = string.Empty;
    private string _killsText = string.Empty;
    private int _range = -1;
    private int _shownAmmo = -1;
    private int _shownWave = -1;
    private int _shownRange = int.MinValue;
    private int _shownMultiplier = -1;
    private int _shownKills = -1;

    static CMUTrackerReflex()
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        for (var sector = 1; sector <= 9; sector++)
        {
            var column = (sector - 1) % 3;
            var row = 2 - (sector - 1) / 3;
            var x0 = -1 + column * 2f / 3;
            var y0 = -1 + row * 2f / 3;
            corners[0] = new Vector2(x0, y0);
            corners[1] = new Vector2(x0 + 2f / 3, y0);
            corners[2] = new Vector2(x0 + 2f / 3, y0 + 2f / 3);
            corners[3] = new Vector2(x0, y0 + 2f / 3);

            // the rim cuts the outer cells: their outline points outside the disc go onto the rim
            var points = new Vector2[4 * CellEdge];
            var sum = Vector2.Zero;
            for (var edge = 0; edge < 4; edge++)
            {
                for (var step = 0; step < CellEdge; step++)
                {
                    var point = Vector2.Lerp(corners[edge], corners[(edge + 1) % 4], step / (float) CellEdge);
                    if (point.Length() > 1)
                        point = Vector2.Normalize(point);

                    points[edge * CellEdge + step] = point;
                    sum += point;
                }
            }

            Cells[sector] = points;
            CellCentres[sector] = sum / points.Length;
        }
    }

    public CMUTrackerReflex(IGameTiming timing) : base(timing)
    {
        // the cabinet's own faces, not the player's UI font: the layout is measured for them
        var cache = IoCManager.Resolve<IResourceCache>();
        _small = new VectorFont(cache.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Bold.ttf"), 7);
        _medium = new VectorFont(cache.GetResource<FontResource>("/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf"), 10);
        _big = new VectorFont(cache.GetResource<FontResource>("/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf"), 13);

        _textModel = Loc.GetString("cmu-game-tracker-model");
        _textTitle = Loc.GetString("cmu-game-tracker-title");
        _textStart = Loc.GetString("cmu-game-tracker-start");
        _textRetry = Loc.GetString("cmu-game-tracker-retry");
        _textPaused = Loc.GetString("cmu-game-tracker-paused");
        _textWave = Loc.GetString("cmu-game-tracker-wave-label");
        _textInbound = Loc.GetString("cmu-game-tracker-inbound");
        _textClear = Loc.GetString("cmu-game-tracker-clear");
        _textLost = Loc.GetString("cmu-game-tracker-lost");
        _textVitals = Loc.GetString("cmu-game-tracker-vitals");
        _textAmmo = Loc.GetString("cmu-game-tracker-ammo");
        _textReload = Loc.GetString("cmu-game-tracker-reload");
        _textReloading = Loc.GetString("cmu-game-tracker-reloading");
        _textStreak = Loc.GetString("cmu-game-tracker-streak");
        _textRange = Loc.GetString("cmu-game-tracker-range");
        _textNone = Loc.GetString("cmu-game-tracker-none");
        _textKeypad = Loc.GetString("cmu-game-tracker-keypad");
        _textMiss = Loc.GetString("cmu-game-tracker-miss");
        _textClose = Loc.GetString("cmu-game-tracker-close");
        _textDanger = Loc.GetString("cmu-game-tracker-danger");

        _health = MaxHealth;
        _ammo = Magazine;
        RefreshTexts();
        ShowBanner(_textTitle, _textStart, float.PositiveInfinity);
    }

    public override string Help => Loc.GetString("cmu-game-tracker-help");

    /// <summary>Xenos can reach you and you can shoot.</summary>
    private bool Playing => _phase is Phase.Intro or Phase.Wave or Phase.Clear;

    private int Multiplier => Math.Min(MaxMultiplier, 1 + _streak / StreakStep);

    public override void Start()
    {
        base.Start();
        _xenos.Clear();
        _particles.Clear();
        _acid.Clear();
        _glass.Clear();
        _streaks.Clear();
        _drips.Clear();
        _rings.Clear();
        _popups.Clear();
        Array.Clear(_keyFlash);

        _female = _random.Next(2) == 0;
        _health = MaxHealth;
        _ammo = Magazine;
        _reload = 0;
        _cooldown = 0;
        _invulnerable = 0;
        _streak = 0;
        _kills = 0;
        _wave = 0;
        _redFlash = 0;
        _clawAge = 99;
        _flashAge = 99;
        _freeze = 0;
        _pingAge = 0;
        _pingPeriod = PingIdle;
        _range = -1;

        Sound(PowerOn, -2);
        Sound(Load);
        NextWave();
    }

    #region Update

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // a game left without the keys waits for them
        _paused = Running && !HasKeyboardFocus();
        if (_paused)
            return;

        var dt = MathF.Min(args.DeltaSeconds, 0.1f);
        if (_freeze > 0)
        {
            // the impact frame: everything holds still for a moment
            _freeze -= dt;
            return;
        }

        _time += dt;
        UpdatePhase(dt);
        UpdateXenos(dt);
        UpdatePing(dt);
        UpdateGun(dt);
        UpdateEffects(dt);
        RefreshTexts();
    }

    private void UpdatePhase(float dt)
    {
        switch (_phase)
        {
            case Phase.Attract:
            case Phase.Over:
                // the idle tracker still picks things up, silently: they come in and are gone
                _spawnTimer -= dt;
                if (_spawnTimer <= 0 && _xenos.Count < DemoContacts)
                {
                    Spawn(Next() * MathF.Tau);
                    _spawnTimer = DemoGap * (0.6f + 0.8f * Next());
                }

                break;
            case Phase.Intro:
                _phaseTimer -= dt;
                if (_phaseTimer <= 0)
                {
                    _phase = Phase.Wave;
                    _spawnTimer = 0.2f;
                }

                break;
            case Phase.Wave:
                _spawnTimer -= dt;
                if (_toSpawn > 0 && _spawnTimer <= 0)
                    SpawnNext();

                if (_toSpawn == 0 && _xenos.Count == 0)
                    ClearWave();

                break;
            case Phase.Clear:
                _phaseTimer -= dt;
                if (_phaseTimer <= 0)
                    NextWave();

                break;
            case Phase.Dying:
                _phaseTimer -= dt;
                if (_phaseTimer <= 0)
                {
                    _phase = Phase.Over;
                    Sound(Static, 0, 0.8f);
                    ShowBanner(_textLost, _textRetry, float.PositiveInfinity);
                    GameOver();
                }

                break;
        }
    }

    private void NextWave()
    {
        _wave++;
        _toSpawn = 4 + 3 * _wave;
        _phase = Phase.Intro;
        _phaseTimer = IntroTime;
        ShowBanner(Loc.GetString("cmu-game-tracker-wave", ("wave", _wave)), _textInbound, IntroTime);
        Sound(Roar, _wave == 1 ? -9 : -5);
    }

    private void ClearWave()
    {
        var bonus = WavePoints * _wave + HealthPoints * _health;
        AddScore(bonus);
        _phase = Phase.Clear;
        _phaseTimer = ClearTime;
        ShowBanner(_textClear, Loc.GetString("cmu-game-tracker-bonus", ("points", bonus)), ClearTime);
        Sound(Cleared);
    }

    private void SpawnNext()
    {
        // spread arrivals around the display instead of stacking them in one sector
        var angle = _lastSpawnAngle;
        for (var tries = 0; tries < 4 && AngleGap(angle, _lastSpawnAngle) < 0.6f; tries++)
        {
            angle = Next() * MathF.Tau;
        }

        _lastSpawnAngle = angle;
        Spawn(angle);
        _toSpawn--;

        // later waves come in pairs now and then
        if (_wave >= 4 && _toSpawn > 0 && Next() < 0.2f)
        {
            Spawn(angle + (Next() < 0.5f ? -0.35f : 0.35f));
            _toSpawn--;
        }

        _spawnTimer = MathF.Max(0.4f, 1.8f - 0.16f * _wave) * (0.7f + 0.6f * Next());
        if (Next() < 0.35f)
            CMUGameAudio.PlayAny(this, Growls, -10);
    }

    private static float AngleGap(float a, float b)
    {
        var gap = MathF.Abs(a - b) % MathF.Tau;
        return gap > MathF.PI ? MathF.Tau - gap : gap;
    }

    private void Spawn(float angle)
    {
        var runner = MathF.Min(0.35f, 0.07f * _wave);
        var lurker = _wave >= 2 ? MathF.Min(0.25f, 0.06f * (_wave - 1)) : 0;
        var brute = _wave >= 3 ? MathF.Min(0.2f, 0.05f * (_wave - 2)) : 0;
        var roll = Next();
        var kind = roll < brute ? Kind.Brute
            : roll < brute + runner ? Kind.Runner
            : roll < brute + runner + lurker ? Kind.Lurker
            : Kind.Drone;
        var pace = kind switch
        {
            Kind.Runner => 1.75f,
            Kind.Lurker => 0.9f,
            Kind.Brute => 0.6f,
            _ => 1f,
        };

        _xenos.Add(new Xeno
        {
            Kind = kind,
            Angle = angle,
            Distance = SpawnRadius,
            Speed = MathF.Min(0.3f, 0.12f + 0.016f * _wave) * pace * (0.9f + 0.2f * Next()),
            Phase = Next() * MathF.Tau,
            Health = kind == Kind.Brute ? 2 : 1,
            ShownAge = 99,
        });
    }

    private void UpdateXenos(float dt)
    {
        _danger = false;
        _invulnerable = MathF.Max(0, _invulnerable - dt);
        for (var i = _xenos.Count - 1; i >= 0; i--)
        {
            var xeno = _xenos[i];
            var pace = xeno.Kind is Kind.Drone or Kind.Brute ? 0.75f + 0.35f * MathF.Sin(_time * 2.3f + xeno.Phase) : 1f;
            xeno.Distance -= xeno.Speed * pace * dt;
            if (xeno.Kind == Kind.Lurker)
                xeno.Angle += MathF.Sin(_time * 1.6f + xeno.Phase) * 0.9f * dt;

            xeno.Hurt = MathF.Max(0, xeno.Hurt - dt);
            xeno.ShownAge += dt;

            // the lunge: once in the middle cell it is coming for you, and only 5 can stop it
            var lunging = InSector(xeno.At, 5, 0);
            if (lunging && !xeno.Lunging && Playing)
                CMUGameAudio.PlayAny(this, Charges, -4);

            xeno.Lunging = lunging;

            _danger |= xeno.Lunging && Playing;
            if (xeno.Distance <= MaulRadius)
            {
                xeno.Distance = MaulRadius;
                if (!Playing)
                {
                    _xenos.RemoveAt(i);
                    continue;
                }

                if (_invulnerable <= 0)
                {
                    _xenos.RemoveAt(i);
                    Maul(xeno);
                    continue;
                }
            }

            _xenos[i] = xeno;
        }
    }

    private void UpdatePing(float dt)
    {
        if (_phase == Phase.Dying)
            return;

        _pingAge += dt;
        var ring = _pingAge / PingTravel * 1.06f;
        for (var i = 0; i < _xenos.Count; i++)
        {
            var xeno = _xenos[i];
            if (xeno.Pinged || ring < xeno.Distance)
                continue;

            xeno.Pinged = true;
            xeno.Shown = xeno.At;
            xeno.ShownAge = 0;
            _xenos[i] = xeno;
        }

        if (_pingAge >= _pingPeriod)
            Ping();
    }

    /// <summary>A new sweep: it comes round faster, higher and louder the closer the nearest contact is.</summary>
    private void Ping()
    {
        _pingAge = 0;
        var closest = float.MaxValue;
        for (var i = 0; i < _xenos.Count; i++)
        {
            var xeno = _xenos[i];
            xeno.Pinged = false;
            _xenos[i] = xeno;
            if (xeno.Distance <= 1)
                closest = MathF.Min(closest, xeno.Distance);
        }

        if (closest > 1)
        {
            _pingPeriod = PingIdle;
            _range = -1;
            if (Playing)
                Sound(PingEmpty, -7);

            return;
        }

        var near = 1 - Math.Clamp((closest - MaulRadius) / (1 - MaulRadius), 0, 1);
        _pingPeriod = PingFar + (PingNear - PingFar) * near;
        _range = (int) MathF.Round(closest * RangeMetres);

        // fast pings overlap their tails, so they come down a little to stay under the gunfire
        if (Playing)
            Sound(PingContact, -5 + 3 * near - (near > 0.6f ? 2 : 0), 1 + 0.3f * near);
    }

    private void UpdateGun(float dt)
    {
        _cooldown = MathF.Max(0, _cooldown - dt);
        _ammoWarn = MathF.Max(0, _ammoWarn - dt);
        if (_reload <= 0)
            return;

        var before = _reload;
        _reload -= dt;
        if (before > ReloadTime * 0.45f && _reload <= ReloadTime * 0.45f)
            Sound(Load);

        if (_reload > 0)
            return;

        _reload = 0;
        _ammo = Magazine;
    }

    private void UpdateEffects(float dt)
    {
        if (_shakeTime > 0)
        {
            _shakeTime -= dt;
            var power = _shakePower * MathF.Max(0, _shakeTime / _shakeDuration);
            _shake = new Vector2(Next() * 2 - 1, Next() * 2 - 1) * power;
        }
        else
        {
            _shake = Vector2.Zero;
        }

        _redFlash = MathF.Max(0, _redFlash - dt * 1.8f);
        _clawAge += dt;
        _flashAge += dt;
        _bannerAge += dt;
        for (var i = 0; i < _keyFlash.Length; i++)
        {
            _keyFlash[i] = MathF.Max(0, _keyFlash[i] - dt * 4);
        }

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Age += dt;
            if (particle.Age >= particle.Life)
            {
                if (particle.Lands)
                    AddDecal(particle.Glass ? _glass : _acid, particle.Position, particle.Size * 2.6f, particle.Colour,
                        particle.Glass ? MaxGlass : MaxDecals);

                _particles[i] = _particles[^1];
                _particles.RemoveAt(_particles.Count - 1);
                continue;
            }

            particle.Position += particle.Velocity * dt;
            particle.Velocity *= MathF.Exp(-particle.Drag * dt);
            if (particle.Glass)
                particle.Velocity.Y += 90 * dt;

            _particles[i] = particle;
        }

        for (var i = 0; i < _drips.Count; i++)
        {
            var drip = _drips[i];
            if (drip.Length >= drip.Max || drip.Speed < 1)
                continue;

            drip.Length = MathF.Min(drip.Max, drip.Length + drip.Speed * dt);
            drip.Speed *= MathF.Exp(-1.4f * dt);
            _drips[i] = drip;
        }

        for (var i = _rings.Count - 1; i >= 0; i--)
        {
            var ring = _rings[i];
            ring.Age += dt;
            if (ring.Age >= ring.Life)
                _rings.RemoveAt(i);
            else
                _rings[i] = ring;
        }

        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            var popup = _popups[i];
            popup.Age += dt;
            if (popup.Age >= 0.9f)
                _popups.RemoveAt(i);
            else
                _popups[i] = popup;
        }
    }

    /// <summary>The panel numbers are strings; they are only rebuilt when the number behind them changes.</summary>
    private void RefreshTexts()
    {
        if (_shownAmmo != _ammo)
        {
            _shownAmmo = _ammo;
            var rounds = _ammo * RoundsPerBurst;
            _ammoText = Digits[rounds / 10 % 10] + Digits[rounds % 10];
        }

        if (_shownWave != _wave)
        {
            _shownWave = _wave;
            _waveText = _wave.ToString();
        }

        if (_shownRange != _range)
        {
            _shownRange = _range;
            _rangeText = _range < 0 ? _textNone : Loc.GetString("cmu-game-tracker-metres", ("metres", _range));
        }

        if (_shownMultiplier != Multiplier)
        {
            _shownMultiplier = Multiplier;
            _multiplierText = Loc.GetString("cmu-game-tracker-multiplier", ("multiplier", _shownMultiplier));
        }

        if (_shownKills != _kills)
        {
            _shownKills = _kills;
            _killsText = Loc.GetString("cmu-game-tracker-kills", ("kills", _kills));
        }
    }

    #endregion

    #region Shooting

    /// <summary>The keypad is the tracker in a 3 by 3 grid: 7 8 9 on top, 1 2 3 at the bottom, 5 is you.</summary>
    private static bool InSector(Vector2 at, int sector, float tolerance)
    {
        var column = (sector - 1) % 3;
        var row = 2 - (sector - 1) / 3;
        return InBand(at.X, column, tolerance) && InBand(at.Y, row, tolerance);
    }

    private static bool InBand(float value, int band, float tolerance)
    {
        return band switch
        {
            0 => value < -Third + tolerance,
            1 => value > -Third - tolerance && value < Third + tolerance,
            _ => value > Third - tolerance,
        };
    }

    protected override void OnKey(CMUArcadeInput input)
    {
        if (!input.Down || input.Repeat || !Running)
            return;

        var sector = input.Pad != 0
            ? input.Pad
            : input.Key switch
            {
                CMUArcadeKey.Up => 8,
                CMUArcadeKey.Down => 2,
                CMUArcadeKey.Left => 4,
                CMUArcadeKey.Right => 6,
                _ => 0,
            };

        if (sector != 0)
            Fire(sector);
        else if (input.Key == CMUArcadeKey.Action)
            StartReload();
    }

    private void StartReload()
    {
        if (!Playing || _reload > 0 || _ammo == Magazine)
            return;

        _reload = ReloadTime;
        Sound(Unload);
    }

    private void Fire(int sector)
    {
        if (!Playing || _cooldown > 0)
            return;

        if (_reload > 0 || _ammo == 0)
        {
            Sound(DryFire);
            _cooldown = FireCooldown * 2;
            if (_reload <= 0)
                _ammoWarn = 0.6f;

            return;
        }

        _ammo--;
        _cooldown = FireCooldown;
        Sound(Shot, -2, 0.95f + Next() * 0.1f);
        _keyFlash[sector] = 1;
        Shake(1.2f, 0.08f);

        // the muzzle flash lights the sector: anything there shows up where it really is
        var target = -1;
        for (var i = 0; i < _xenos.Count; i++)
        {
            var xeno = _xenos[i];
            if (xeno.Distance > 1 || !InSector(xeno.At, sector, Tolerance))
                continue;

            xeno.Shown = xeno.At;
            xeno.ShownAge = 0;
            _xenos[i] = xeno;
            if (target < 0 || xeno.Distance < _xenos[target].Distance)
                target = i;
        }

        _flashSector = sector;
        _flashAge = 0;
        _flashHit = target >= 0;
        _flashTo = target >= 0 ? _xenos[target].At : CellCentres[sector] * (sector == 5 ? 0 : 1.15f);
        for (var i = 0; i < _tracers.Length; i++)
        {
            _tracers[i] = new Vector2(Next() - 0.5f, Next() - 0.5f) * (target >= 0 ? 0.06f : 0.25f);
        }

        if (target < 0)
        {
            Miss(sector);
            return;
        }

        Hit(target, sector == 5);
    }

    private void Miss(int sector)
    {
        _streak = 0;
        AddScore(-MissPenalty);
        Pop(_textMiss, Unit(CellCentres[sector]), Dim);
    }

    private void Hit(int index, bool pointBlank)
    {
        var xeno = _xenos[index];
        var at = Unit(xeno.At);
        var outward = xeno.At.LengthSquared() > 0.0001f ? Vector2.Normalize(xeno.At) : new Vector2(0, -1);
        xeno.Health--;
        if (xeno.Health > 0)
        {
            // a brute shrugs it off and gets knocked back, bleeding acid
            xeno.Hurt = 0.15f;
            xeno.Distance = MathF.Min(1, xeno.Distance + 0.08f);
            xeno.Shown = xeno.At;
            xeno.ShownAge = 0;
            _xenos[index] = xeno;
            Spray(at, outward, 0.9f, 8, 30, 70, Acid, false, 0.4f);
            AddDecal(_acid, at, 18 + Next() * 8, AcidStain, MaxDecals);
            CMUGameAudio.PlayAny(this, Flesh, -3);
            CMUGameAudio.PlayAny(this, Hisses, -4);
            return;
        }

        _xenos.RemoveAt(index);
        _kills++;
        _streak++;
        var points = KillPoints * Multiplier + (pointBlank ? PointBlankPoints : 0);
        AddScore(points);
        Pop("+" + points, at + new Vector2(0, -6), Hot);
        if (pointBlank)
            Pop(_textClose, at + new Vector2(0, 8), Warn);
        else if (_streak % StreakStep == 0 && Multiplier > 1)
            Pop(Loc.GetString("cmu-game-tracker-multiplier", ("multiplier", Multiplier)), at + new Vector2(0, 8), Acid);

        var big = xeno.Kind == Kind.Brute;
        Spray(at, outward, 1.3f, big ? 26 : 16, 35, big ? 120 : 95, Acid, false, 0.35f);
        AddDecal(_acid, at, (big ? 46 : 32) + Next() * 10, AcidStain, MaxDecals);
        _rings.Add(new Ring { Position = at, Life = 0.32f, Size = big ? 22 : 15, Colour = Acid });
        Shake(big ? 3 : 1.8f, 0.14f);
        _freeze = 0.035f;

        if (big)
            Sound(BruteDeath, -3);
        else
            CMUGameAudio.PlayAny(this, Deaths, -3);

        CMUGameAudio.PlayAny(this, Sizzles, -8);
    }

    /// <summary>One got through: claws across the glass, your blood on it for the rest of the run.</summary>
    private void Maul(Xeno xeno)
    {
        _health--;
        _streak = 0;
        _invulnerable = Invulnerable;
        _freeze = 0.12f;
        _redFlash = 1;
        _clawAge = 0;
        Shake(6, 0.5f);

        var from = xeno.Angle;
        var direction = new Vector2(MathF.Cos(from), MathF.Sin(from));
        var slash = from + MathF.PI / 2 + (Next() - 0.5f) * 0.8f;
        var along = new Vector2(MathF.Cos(slash), MathF.Sin(slash));
        var across = new Vector2(-along.Y, along.X);
        var middle = new Vector2(CentreX, CentreY) + direction * 18;
        for (var i = 0; i < _claws.Length; i++)
        {
            var start = middle + across * ((i - 1) * 13) - along * 80;
            _claws[i] = (start, along);
            AddStreak(start + along * 20, start + along * (140 - Next() * 30), 1.4f + Next());
        }

        var splats = _health <= 0 ? 7 : 3;
        for (var i = 0; i < splats; i++)
        {
            var spot = middle + direction * (Next() * 70) + new Vector2(Next() - 0.5f, Next() - 0.5f) * 110;
            var size = 50 + Next() * 55;
            AddDecal(_glass, spot, size, Blood, MaxGlass);
            for (var d = 0; d < 2; d++)
            {
                _drips.Add(new Drip
                {
                    Top = spot + new Vector2((Next() - 0.5f) * size * 0.5f, (Next() - 0.2f) * size * 0.3f),
                    Max = 18 + Next() * 55,
                    Speed = 40 + Next() * 50,
                    Width = 1.5f + Next() * 1.8f,
                });
            }
        }

        Spray(new Vector2(CentreX, CentreY), direction, MathF.PI, _health <= 0 ? 40 : 24, 90, 230, Blood, true, 0.45f);
        Pop("-1", new Vector2(CentreX, CentreY - 16), Warn);

        CMUGameAudio.PlayAny(this, Claws, 0);
        CMUGameAudio.Scream(this, _female);
        if (_health > 0)
            return;

        CMUGameAudio.PlayAny(this, Bites, 0);
        _phase = Phase.Dying;
        _phaseTimer = DyingTime;
        _freeze = 0.2f;
        _reload = 0;
        _banner = null;
    }

    #endregion

    #region Effects

    private float Next()
    {
        return (float) _random.NextDouble();
    }

    private void AddScore(int points)
    {
        Score = Math.Clamp(Score + points, 0, CMUArcadeLimits.MaxScore);
    }

    private void Shake(float power, float duration)
    {
        if (power < _shakePower * MathF.Max(0, _shakeTime / _shakeDuration))
            return;

        _shakePower = power;
        _shakeTime = duration;
        _shakeDuration = duration;
    }

    private void ShowBanner(string text, string? sub, float life)
    {
        _banner = text;
        _bannerSub = sub;
        _bannerAge = 0;
        _bannerLife = life;
    }

    private void Pop(string text, Vector2 at, Color colour)
    {
        _popups.Add(new Popup { Text = text, Position = at, Colour = colour });
    }

    private void Spray(Vector2 at, Vector2 direction, float spread, int count, float slowest, float fastest, Color colour,
        bool glass, float lands)
    {
        var heading = MathF.Atan2(direction.Y, direction.X);
        for (var i = 0; i < count && _particles.Count < MaxParticles; i++)
        {
            var angle = heading + (Next() - 0.5f) * 2 * spread;
            var speed = slowest + Next() * (fastest - slowest);
            _particles.Add(new Particle
            {
                Position = at,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Life = 0.3f + Next() * 0.45f,
                Size = 0.8f + Next() * 1.6f,
                Drag = glass ? 2.5f : 3.2f,
                Colour = colour,
                Glass = glass,
                Lands = Next() < lands,
            });
        }
    }

    private void AddDecal(List<Decal> decals, Vector2 at, float size, Color colour, int max)
    {
        if (decals.Count >= max)
            decals.RemoveAt(0);

        decals.Add(new Decal
        {
            Position = at,
            Size = size,
            State = _random.Next(SplatterStates),
            Rotation = Next() * MathF.Tau,
            Colour = colour,
        });
    }

    private void AddStreak(Vector2 from, Vector2 to, float width)
    {
        if (_streaks.Count >= MaxStreaks)
            _streaks.RemoveAt(0);

        _streaks.Add((from, to, width));
    }

    #endregion

    #region Drawing

    /// <summary>A point on the unit disc of the display, in base screen units.</summary>
    private static Vector2 Unit(Vector2 point)
    {
        return new Vector2(CentreX + point.X * Radius, CentreY + point.Y * Radius);
    }

    /// <summary>Base units to pixels, shaken with the screen.</summary>
    private Vector2 Px(Vector2 units)
    {
        return (units + _shake) * UIScale;
    }

    /// <summary>Base units to pixels on the glass, which stays still.</summary>
    private Vector2 Fixed(Vector2 units)
    {
        return units * UIScale;
    }

    private void LoadTextures()
    {
        if (_splatter != null && _echo != null)
            return;

        var sprites = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        _splatter = new Texture[SplatterStates];
        for (var i = 0; i < SplatterStates; i++)
        {
            _splatter[i] = sprites.Frame0(new SpriteSpecifier.Rsi(SplatterRsi, $"splatter-{i}"));
        }

        // the real tracker's blip: a dot that rings outward
        var blip = sprites.RsiStateLike(new SpriteSpecifier.Rsi(DetectorRsi, "detector_blip"));
        _echo = new Texture[Math.Max(1, blip.AnimationFrameCount)];
        for (var i = 0; i < _echo.Length; i++)
        {
            _echo[i] = blip.GetFrame(RsiDirection.South, i);
        }
    }

    protected override void DrawGame(DrawingHandleScreen handle)
    {
        LoadTextures();
        var dead = _phase is Phase.Dying or Phase.Over;

        // a dying tracker flickers out
        var lit = _phase != Phase.Dying || (int) (_time * 22) % 3 != 0;
        DrawDisplay(handle, lit);
        if (lit)
        {
            foreach (var decal in _acid)
            {
                DrawDecal(handle, decal, true);
            }

            DrawPing(handle);
            DrawFlash(handle);
            DrawBlips(handle);
            DrawMarine(handle);
            foreach (var ring in _rings)
            {
                var t = ring.Age / ring.Life;
                handle.DrawCircle(Px(ring.Position), ring.Size * t * UIScale, ring.Colour.WithAlpha(1 - t), false);
                handle.DrawCircle(Px(ring.Position), (ring.Size * t + 1) * UIScale, ring.Colour.WithAlpha((1 - t) * 0.5f), false);
            }
        }

        if (dead)
            DrawStatic(handle, _phase == Phase.Dying ? 18 : 6);

        DrawGlare(handle);
        DrawPanels(handle);

        foreach (var particle in _particles)
        {
            var t = particle.Age / particle.Life;
            var at = particle.Glass ? Fixed(particle.Position) : Px(particle.Position);
            handle.DrawCircle(at, particle.Size * (1 - t * 0.4f) * UIScale, particle.Colour.WithAlpha(particle.Colour.A * (1 - t * t)));
        }

        foreach (var popup in _popups)
        {
            var t = popup.Age / 0.9f;
            var at = popup.Position + new Vector2(0, -18 * (1 - (1 - t) * (1 - t)));
            Centred(handle, _small, popup.Text, at, popup.Colour.WithAlpha(1 - t * t), false);
        }

        DrawGlass(handle);
        DrawDanger(handle);
        DrawBanner(handle);
        DrawScanlines(handle);
    }

    private void DrawDisplay(DrawingHandleScreen handle, bool lit)
    {
        var centre = Px(new Vector2(CentreX, CentreY));
        handle.DrawCircle(centre, (Radius + 7) * UIScale, Bezel);
        handle.DrawCircle(centre, (Radius + 7) * UIScale, Dim.WithAlpha(0.6f), false);
        handle.DrawCircle(centre, (Radius + 2) * UIScale, Color.Black);
        handle.DrawCircle(centre, Radius * UIScale, Face);
        if (!lit)
            return;

        // a soft phosphor bloom towards the middle
        handle.DrawCircle(centre, Radius * 0.55f * UIScale, Green.WithAlpha(0.025f));
        handle.DrawCircle(centre, Radius * UIScale, Dim.WithAlpha(0.9f), false);
        handle.DrawCircle(centre, Radius * 0.5f * UIScale, Dim.WithAlpha(0.35f), false);
        handle.DrawCircle(centre, Radius * MaulRadius * UIScale, Dim.WithAlpha(0.5f), false);

        for (var i = 0; i < 24; i++)
        {
            var angle = i * MathF.Tau / 24;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var inner = i % 6 == 0 ? 0.9f : 0.95f;
            handle.DrawLine(Px(Unit(direction * inner)), Px(Unit(direction)), Dim.WithAlpha(0.8f));
        }

        // the keypad grid: chords at a third either way, the middle cell boxed in
        var chord = MathF.Sqrt(1 - Third * Third);
        var grid = Dim.WithAlpha(0.55f);
        handle.DrawLine(Px(Unit(new Vector2(-Third, -chord))), Px(Unit(new Vector2(-Third, chord))), grid);
        handle.DrawLine(Px(Unit(new Vector2(Third, -chord))), Px(Unit(new Vector2(Third, chord))), grid);
        handle.DrawLine(Px(Unit(new Vector2(-chord, -Third))), Px(Unit(new Vector2(chord, -Third))), grid);
        handle.DrawLine(Px(Unit(new Vector2(-chord, Third))), Px(Unit(new Vector2(chord, Third))), grid);

        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 18);
        var middle = _danger ? Warn.WithAlpha(0.45f + 0.5f * pulse) : Dim;
        handle.DrawRect(new UIBox2(Px(Unit(new Vector2(-Third, -Third))), Px(Unit(new Vector2(Third, Third)))), middle, false);
        if (_danger)
            DrawCell(handle, 5, Warn.WithAlpha(0.06f + 0.08f * pulse));

        for (var sector = 1; sector <= 9; sector++)
        {
            if (sector == 5)
                continue;

            var label = CellCentres[sector] * 1.12f;
            var bright = _keyFlash[sector];
            Centred(handle, _small, Digits[sector], Unit(label), Dim.WithAlpha(0.45f + 0.55f * bright), false);
        }
    }

    private void DrawCell(DrawingHandleScreen handle, int sector, Color colour)
    {
        var outline = Cells[sector];
        Span<Vector2> fan = stackalloc Vector2[4 * CellEdge + 2];
        fan[0] = Px(Unit(CellCentres[sector]));
        for (var i = 0; i < outline.Length; i++)
        {
            fan[i + 1] = Px(Unit(outline[i]));
        }

        fan[^1] = fan[1];
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, fan, colour);
    }

    private void DrawPing(DrawingHandleScreen handle)
    {
        var t = _pingAge / PingTravel;
        if (t >= 1)
            return;

        var centre = Px(new Vector2(CentreX, CentreY));
        var radius = t * 1.06f * Radius * UIScale;
        var colour = _danger ? Color.InterpolateBetween(Green, Warn, 0.4f) : Green;
        handle.DrawCircle(centre, radius, colour.WithAlpha(0.05f * (1 - t)));
        handle.DrawCircle(centre, radius, colour.WithAlpha(0.85f * (1 - t * 0.7f)), false);
        handle.DrawCircle(centre, MathF.Max(0, radius - 2 * UIScale), colour.WithAlpha(0.35f * (1 - t)), false);
        handle.DrawCircle(centre, MathF.Max(0, radius - 5 * UIScale), colour.WithAlpha(0.15f * (1 - t)), false);
    }

    /// <summary>The burst: the sector lights up, tracers run out from you, a spark where they land.</summary>
    private void DrawFlash(DrawingHandleScreen handle)
    {
        const float life = 0.14f;
        if (_flashAge >= life)
            return;

        var a = 1 - _flashAge / life;
        DrawCell(handle, _flashSector, Green.WithAlpha((_flashHit ? 0.26f : 0.16f) * a));

        var target = _flashTo;
        var heading = target.LengthSquared() > 0.0001f ? Vector2.Normalize(target) : new Vector2(0, -1);
        var muzzle = heading * 0.07f;
        foreach (var jitter in _tracers)
        {
            Thick(handle, Px(Unit(muzzle)), Px(Unit(target + jitter)), 1.3f * UIScale, Hot.WithAlpha(a));
        }

        // a star of a muzzle flash, then the hit spark
        Span<Vector2> star = stackalloc Vector2[14];
        var at = Px(Unit(muzzle));
        star[0] = at;
        for (var i = 0; i < 13; i++)
        {
            var angle = i * MathF.Tau / 12 + _flashAge * 9;
            var reach = (i % 2 == 0 ? 7 : 2.5f) * (0.6f + 0.4f * a) * UIScale;
            star[i + 1] = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, star, Hot.WithAlpha(a));
        if (_flashHit)
        {
            handle.DrawCircle(Px(Unit(target)), (2 + 6 * (1 - a)) * UIScale, Hot.WithAlpha(a));
            handle.DrawCircle(Px(Unit(target)), (4 + 9 * (1 - a)) * UIScale, Acid.WithAlpha(0.6f * a), false);
        }
    }

    private void DrawBlips(DrawingHandleScreen handle)
    {
        foreach (var xeno in _xenos)
        {
            if (xeno.ShownAge >= BlipFade)
                continue;

            var fade = 1 - xeno.ShownAge / BlipFade;
            if (xeno.Kind == Kind.Lurker)
                fade *= 0.7f + 0.3f * MathF.Sin(_time * 25 + xeno.Phase);

            var at = Unit(xeno.Shown);
            var colour = xeno.Hurt > 0 ? Hot : xeno.Lunging ? Warn : Green;
            var size = xeno.Kind switch
            {
                Kind.Runner => 2.2f,
                Kind.Lurker => 2.7f,
                Kind.Brute => 4.2f,
                _ => 3f,
            };

            // it pops when the ping finds it, then settles
            size *= 1 + 0.7f * MathF.Max(0, 0.12f - xeno.ShownAge) / 0.12f;
            if (_echo != null && xeno.ShownAge < EchoTime)
            {
                var frame = Math.Min(_echo.Length - 1, (int) (xeno.ShownAge / EchoTime * _echo.Length));
                DrawSprite(handle, _echo[frame], Px(at), size * 7.5f * UIScale, 0, colour.WithAlpha(0.8f * fade));
            }

            var pixel = Px(at);
            handle.DrawCircle(pixel, size * 2.6f * UIScale, colour.WithAlpha(0.1f * fade));
            handle.DrawCircle(pixel, size * 1.7f * UIScale, colour.WithAlpha(0.22f * fade));
            handle.DrawCircle(pixel, size * UIScale, colour.WithAlpha(fade));
            handle.DrawCircle(pixel, size * 0.45f * UIScale, Hot.WithAlpha(fade));
            if (xeno.Kind == Kind.Brute)
                handle.DrawCircle(pixel, (size + 3) * UIScale, colour.WithAlpha(0.6f * fade), false);
        }
    }

    private void DrawMarine(DrawingHandleScreen handle)
    {
        var hurt = _invulnerable > 0 && (int) (_time * 12) % 2 == 0;
        var colour = hurt ? Warn : _phase is Phase.Dying or Phase.Over ? BloodDark : Green;
        var at = Px(new Vector2(CentreX, CentreY));
        Span<Vector2> chevron = stackalloc Vector2[4];
        chevron[0] = at + new Vector2(0, -5) * UIScale;
        chevron[1] = at + new Vector2(4, 4) * UIScale;
        chevron[2] = at + new Vector2(0, 2) * UIScale;
        chevron[3] = at + new Vector2(-4, 4) * UIScale;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, chevron, colour);
    }

    private void DrawStatic(DrawingHandleScreen handle, int lines)
    {
        for (var i = 0; i < lines; i++)
        {
            var y = (Next() * 2 - 1) * 0.95f;
            var half = MathF.Sqrt(1 - y * y);
            var x = (Next() * 2 - 1) * half;
            var length = Next() * 0.5f;
            var from = Unit(new Vector2(MathF.Max(-half, x - length), y));
            var to = Unit(new Vector2(MathF.Min(half, x + length), y));
            handle.DrawLine(Px(from), Px(to), Green.WithAlpha(0.15f + Next() * 0.3f));
        }
    }

    private void DrawGlare(DrawingHandleScreen handle)
    {
        Span<Vector2> band = stackalloc Vector2[4];
        band[0] = Px(Unit(new Vector2(-0.82f, -0.3f)));
        band[1] = Px(Unit(new Vector2(-0.3f, -0.82f)));
        band[2] = Px(Unit(new Vector2(-0.12f, -0.66f)));
        band[3] = Px(Unit(new Vector2(-0.66f, -0.12f)));
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, band, Color.White.WithAlpha(0.035f));
    }

    private void DrawPanels(DrawingHandleScreen handle)
    {
        // left: vitals, the rifle's counter, the streak
        Text(handle, _small, _textVitals, new Vector2(10, 30), Dim);
        for (var i = 0; i < MaxHealth; i++)
        {
            var alive = i < _health;
            var at = new Vector2(16 + i * 16, 47);
            var colour = alive ? Green : BloodDark;
            if (alive && _health == 1)
                colour = Color.InterpolateBetween(Green, Warn, 0.5f + 0.5f * MathF.Sin(_time * 9));

            Fill(handle, at + new Vector2(-5, -1.5f), new Vector2(10, 3), colour);
            Fill(handle, at + new Vector2(-1.5f, -5), new Vector2(3, 10), colour);
            if (!alive)
            {
                Thick(handle, Px(at + new Vector2(-5, -5)), Px(at + new Vector2(5, 5)), 1.2f * UIScale, Warn.WithAlpha(0.7f));
                Thick(handle, Px(at + new Vector2(5, -5)), Px(at + new Vector2(-5, 5)), 1.2f * UIScale, Warn.WithAlpha(0.7f));
            }
        }

        Text(handle, _small, _textAmmo, new Vector2(10, 60), Dim);
        var blink = (int) (_time * 4) % 2 == 0;
        var low = _ammo <= 2 && _reload <= 0;
        var counter = low && blink ? Warn : Green;
        Text(handle, _big, _ammoText, new Vector2(10, 72), counter);
        if (_reload > 0)
        {
            var done = 1 - _reload / ReloadTime;
            Fill(handle, new Vector2(10, 95), new Vector2(48, 4), Dim.WithAlpha(0.4f));
            Fill(handle, new Vector2(10, 95), new Vector2(48 * done, 4), Green);
            Text(handle, _small, _textReloading, new Vector2(10, 102), Green);
        }
        else
        {
            for (var i = 0; i < Magazine; i++)
            {
                Fill(handle, new Vector2(10 + i * 5, 95), new Vector2(3, 4), i < _ammo ? Green : Dim.WithAlpha(0.35f));
            }

            if ((_ammo == 0 || _ammoWarn > 0) && blink)
                Text(handle, _small, _textReload, new Vector2(10, 102), Warn);
        }

        Text(handle, _small, _textStreak, new Vector2(10, 120), Dim);
        Text(handle, _medium, _multiplierText, new Vector2(10, 132), Multiplier > 1 ? Acid : Green);
        Text(handle, _small, _killsText, new Vector2(10, 200), Dim);

        // right: the range to the nearest contact, the wave, the keypad
        Text(handle, _small, _textRange, new Vector2(262, 30), Dim);
        var range = _danger ? _textDanger : _rangeText;
        Text(handle, _medium, range, new Vector2(262, 42), _danger && blink ? Warn : Green);
        Text(handle, _small, _textModel, new Vector2(314, 4), Dim, true);

        Text(handle, _small, _textWave, new Vector2(262, 62), Dim);
        Text(handle, _medium, _waveText, new Vector2(262, 74), Green);

        Text(handle, _small, _textKeypad, new Vector2(262, 136), Dim);
        for (var sector = 1; sector <= 9; sector++)
        {
            var column = (sector - 1) % 3;
            var row = 2 - (sector - 1) / 3;
            var at = new Vector2(262 + column * 17, 148 + row * 17);
            var contact = sector == 5 ? _danger : SectorShows(sector);
            var fill = sector == 5 && _danger ? Warn.WithAlpha(0.35f)
                : contact ? Dim.WithAlpha(0.45f)
                : Color.Transparent;
            fill = Color.InterpolateBetween(fill, Green, _keyFlash[sector]);
            if (fill.A > 0)
                Fill(handle, at, new Vector2(15, 15), fill);

            handle.DrawRect(new UIBox2(Px(at), Px(at + new Vector2(15, 15))), Dim, false);
            Centred(handle, _small, Digits[sector], at + new Vector2(7.5f, 7.5f), _keyFlash[sector] > 0.5f ? Color.Black : Green, false);
        }
    }

    /// <summary>Whether a blip still glows in that sector, for the keypad on the side panel.</summary>
    private bool SectorShows(int sector)
    {
        foreach (var xeno in _xenos)
        {
            if (xeno.ShownAge < BlipFade * 0.8f && InSector(xeno.Shown, sector, 0))
                return true;
        }

        return false;
    }

    /// <summary>Your blood on the glass: it stays put when the screen shakes, and runs down slowly.</summary>
    private void DrawGlass(DrawingHandleScreen handle)
    {
        foreach (var (from, to, width) in _streaks)
        {
            Thick(handle, Fixed(from), Fixed(to), width * UIScale, BloodDark.WithAlpha(0.7f));
        }

        foreach (var decal in _glass)
        {
            DrawDecal(handle, decal, false);
        }

        foreach (var drip in _drips)
        {
            var bottom = drip.Top + new Vector2(0, drip.Length);
            Thick(handle, Fixed(drip.Top), Fixed(bottom), drip.Width * UIScale, BloodDark);
            handle.DrawCircle(Fixed(bottom), drip.Width * 0.75f * UIScale, BloodDark);
        }

        // the claws: three slashes torn across in a blink, fading out
        const float swipe = 0.07f;
        const float hold = 0.45f;
        if (_clawAge < swipe + hold)
        {
            var reach = MathF.Min(1, _clawAge / swipe);
            var fade = _clawAge < swipe ? 1 : 1 - (_clawAge - swipe) / hold;
            var colour = Color.InterpolateBetween(Color.White, Warn, MathF.Min(1, _clawAge / 0.15f)).WithAlpha(fade);
            foreach (var (start, along) in _claws)
            {
                Thick(handle, Fixed(start), Fixed(start + along * 160 * reach), 3.5f * UIScale, colour);
            }
        }

        if (_redFlash > 0)
            handle.DrawRect(PixelSizeBox, Warn.WithAlpha(0.5f * _redFlash * _redFlash));

        if (_phase is Phase.Dying or Phase.Over)
            handle.DrawRect(PixelSizeBox, BloodDark.WithAlpha(0.18f));
    }

    /// <summary>The edges pulse red while something is in the middle cell, and slowly on your last life.</summary>
    private void DrawDanger(DrawingHandleScreen handle)
    {
        var alpha = 0f;
        if (_danger)
            alpha = 0.18f + 0.14f * MathF.Sin(_time * 18);
        else if (_health == 1 && Playing)
            alpha = 0.08f * MathF.Max(0, MathF.Sin(_time * 5));

        if (alpha <= 0)
            return;

        var width = PixelWidth;
        var height = PixelHeight;
        for (var layer = 1; layer <= 2; layer++)
        {
            var edge = 6 * layer * UIScale;
            var colour = Warn.WithAlpha(alpha / layer);
            handle.DrawRect(new UIBox2(0, 0, width, edge), colour);
            handle.DrawRect(new UIBox2(0, height - edge, width, height), colour);
            handle.DrawRect(new UIBox2(0, edge, edge, height - edge), colour);
            handle.DrawRect(new UIBox2(width - edge, edge, width, height - edge), colour);
        }
    }

    private void DrawBanner(DrawingHandleScreen handle)
    {
        if (_paused)
        {
            Banner(handle, _textPaused, null, 1, 1);
            return;
        }

        if (_banner == null || _bannerAge >= _bannerLife)
            return;

        var fade = MathF.Min(1, _bannerAge / 0.12f);
        if (!float.IsPositiveInfinity(_bannerLife))
            fade = MathF.Min(fade, (_bannerLife - _bannerAge) / 0.3f);

        // it slams in a little oversized and settles
        var slam = 1 + 0.35f * MathF.Max(0, 1 - _bannerAge / 0.15f);
        var sub = _bannerSub;
        if (sub != null && (sub == _textStart || sub == _textRetry))
        {
            if (!HasKeyboardFocus() || (int) (_time * 2) % 2 == 1)
                sub = null;
        }

        Banner(handle, _banner, sub, fade, slam);
    }

    private void Banner(DrawingHandleScreen handle, string text, string? sub, float fade, float slam)
    {
        // a long one shrinks to fit inside the display
        var fit = MathF.Min(1, BannerWidth * UIScale / handle.GetDimensions(_big, text, UIScale).X);
        slam *= fit;
        var size = handle.GetDimensions(_big, text, UIScale * slam);
        var centre = new Vector2(CentreX, CentreY - 10) * UIScale;
        var box = UIBox2.FromDimensions(centre - size / 2 - new Vector2(8, 4) * UIScale, size + new Vector2(16, 8) * UIScale);
        handle.DrawRect(box, Color.Black.WithAlpha(0.75f * fade));
        handle.DrawRect(box, Green.WithAlpha(0.8f * fade), false);
        var at = centre - size / 2;
        var glow = Green.WithAlpha(0.25f * fade);
        handle.DrawString(_big, at + new Vector2(-1, 0) * UIScale, text, UIScale * slam, glow);
        handle.DrawString(_big, at + new Vector2(1, 0) * UIScale, text, UIScale * slam, glow);
        handle.DrawString(_big, at, text, UIScale * slam, Green.WithAlpha(fade));
        if (sub == null)
            return;

        var subSize = handle.GetDimensions(_small, sub, UIScale);
        var subAt = new Vector2(CentreX, CentreY + 12) * UIScale - subSize / 2;
        handle.DrawRect(UIBox2.FromDimensions(subAt - new Vector2(4, 2) * UIScale, subSize + new Vector2(8, 4) * UIScale),
            Color.Black.WithAlpha(0.7f * fade));
        handle.DrawString(_small, subAt, sub, UIScale, Hot.WithAlpha(fade));
    }

    /// <summary>The CRT: dark lines across the picture and a faint bright band rolling down it.</summary>
    private void DrawScanlines(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var line = Color.Black.WithAlpha(0.2f);
        for (var y = 0f; y < 224; y += 3)
        {
            handle.DrawRect(new UIBox2(0, y * UIScale, width, (y + 1) * UIScale), line);
        }

        var roll = (_time * 40) % 264 - 20;
        handle.DrawRect(new UIBox2(0, roll * UIScale, width, (roll + 20) * UIScale), Green.WithAlpha(0.025f));
    }

    private void DrawDecal(DrawingHandleScreen handle, Decal decal, bool shaken)
    {
        if (_splatter == null)
            return;

        var centre = shaken ? Px(decal.Position) : Fixed(decal.Position);
        DrawSprite(handle, _splatter[decal.State], centre, decal.Size * UIScale, decal.Rotation, decal.Colour);
    }

    /// <summary>Draws a texture centred on a pixel spot, <paramref name="size"/> pixels across, turned by <paramref name="rotation"/>.</summary>
    private static void DrawSprite(DrawingHandleScreen handle, Texture texture, Vector2 centre, float size, float rotation, Color colour)
    {
        var half = new Vector2(size, size) / 2;
        if (rotation == 0)
        {
            handle.DrawTextureRect(texture, new UIBox2(centre - half, centre + half), colour);
            return;
        }

        var transform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.Multiply(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(centre), transform));
        handle.DrawTextureRect(texture, new UIBox2(-half, half), colour);
        handle.SetTransform(transform);
    }

    /// <summary>A line with a width, in pixels: the engine's lines are one pixel thin.</summary>
    private static void Thick(DrawingHandleScreen handle, Vector2 from, Vector2 to, float width, Color colour)
    {
        var along = to - from;
        var length = along.Length();
        if (length < 0.01f)
            return;

        var side = new Vector2(-along.Y, along.X) / length * (width / 2);
        Span<Vector2> quad = stackalloc Vector2[4];
        quad[0] = from + side;
        quad[1] = from - side;
        quad[2] = to + side;
        quad[3] = to - side;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, quad, colour);
    }

    private void Fill(DrawingHandleScreen handle, Vector2 at, Vector2 size, Color colour)
    {
        handle.DrawRect(new UIBox2(Px(at), Px(at + size)), colour);
    }

    private void Text(DrawingHandleScreen handle, Font font, string text, Vector2 at, Color colour, bool right = false)
    {
        var pixel = Px(at);
        if (right)
            pixel.X -= handle.GetDimensions(font, text, UIScale).X;

        handle.DrawString(font, pixel, text, UIScale, colour);
    }

    private void Centred(DrawingHandleScreen handle, Font font, string text, Vector2 centre, Color colour, bool glass)
    {
        var size = handle.GetDimensions(font, text, UIScale);
        var at = (glass ? Fixed(centre) : Px(centre)) - size / 2;
        handle.DrawString(font, at, text, UIScale, colour);
    }

    #endregion
}
