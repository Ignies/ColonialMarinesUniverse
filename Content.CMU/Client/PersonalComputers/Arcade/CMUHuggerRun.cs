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
/// Hugger Run: a xeno's life cycle as one arcade run. A facehugger dodges fire down a colony corridor until it lands
/// on a face, bursts out as a larva, slips through the vents, evolves, sneaks past marines as a runner and ends as
/// a ravager. This class is the cabinet and the stage machine: it runs one <see cref="CMUHuggerStage"/> at a time,
/// fades between them with a title card, and owns everything the stages share: input, score, particles, decals,
/// popups, shake, flashes, slow motion, sounds, sprites, dressed humans, the drawing helpers, the HUD and the
/// phosphor screen. The stage contract is written up in hugger-stage-contract.md; the JS port mirrors it.
/// </summary>
public sealed class CMUHuggerRun : CMUArcadeScreen
{
    #region Layout and tuning

    // The cabinet screen is 320 by 224 units at UI scale 1; the HUD band takes the top 16.
    public const float ScreenWidth = 320;
    public const float ScreenHeight = 224;
    public const float HudHeight = 16;

    /// <summary>Stages update in fixed steps of this many seconds.</summary>
    public const float Step = 1 / 120f;

    /// <summary>Distance in world units that makes one point in the runner stages.</summary>
    public const float UnitsPerPoint = 12;

    private const int Milestone = 500;
    private const float FadeTime = 0.4f;
    private const float CardTime = 1.8f;
    private const float RestartDelay = 0.9f;
    private const float SlowFactor = 0.35f;

    private const int MaxParticles = 480;
    private const int MaxDecals = 180;
    private const int MaxPopups = 10;
    private const int MaxPending = 12;

    public const byte Drop = 0;
    public const byte Spark = 1;
    public const byte Smoke = 2;

    #endregion

    #region Shared art and palette

    public const string FontPath = "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf";
    public const string Splatter = "/Textures/Fluids/splatter.rsi";
    public const string Parasite = "/Textures/_RMC14/Mobs/Xenonids/Parasite/parasite.rsi";
    public const string Larva = "/Textures/_RMC14/Mobs/Xenonids/Larva/larva.rsi";
    public const string Runner = "/Textures/_RMC14/Mobs/Xenonids/Runner/runner.rsi";
    public const string Ravager = "/Textures/_RMC14/Mobs/Xenonids/Ravager/ravager.rsi";

    private const string Parts = "/Textures/Mobs/Species/Human/parts.rsi";
    private const string Eyes = "/Textures/Mobs/Customization/eyes.rsi";
    private const string Hair = "/Textures/Mobs/Customization/human_hair.rsi";
    private const string Clothing = "/Textures/_RMC14/Objects/Clothing/";
    private const string Rifle = "/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi";
    private const string Knife = "/Textures/_RMC14/Objects/Weapons/Melee/m5_bayonet.rsi";
    private const string Shotgun = "/Textures/_RMC14/Objects/Weapons/Guns/Shotguns/m37a1/classic.rsi";

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

    private const string NewGame = "/Audio/Effects/Arcade/newgame.ogg";
    private const string Lost = "/Audio/Effects/Arcade/gameover.ogg";
    private const string Won = "/Audio/Effects/Arcade/win.ogg";
    private const string Beep = "/Audio/Effects/beep1.ogg";

    /// <summary>Sprites are drawn with this multiplied in, so they sit in the green phosphor without losing colour.</summary>
    public static readonly Color Phosphor = Color.FromHex("#E2FFE2");

    public static readonly Color Bright = Color.FromHex("#A8FFB8");
    public static readonly Color Amber = Color.FromHex("#D8A428");
    public static readonly Color Alarm = Color.FromHex("#FF3B30");
    public static readonly Color Acid = Color.FromHex("#C6F02A");
    public static readonly Color DarkAcid = Color.FromHex("#6E9410");
    public static readonly Color Blood = Color.FromHex("#A80E0E");
    public static readonly Color DarkBlood = Color.FromHex("#5E0606");
    public static readonly Color TracerCore = Color.FromHex("#FFF6CC");
    public static readonly Color TracerGlow = Color.FromHex("#FFB43C");
    public static readonly Color Dust = Color.FromHex("#7A9C7C");
    public static readonly Color Laser = Color.FromHex("#FF2A2A");
    public static readonly Color PhosphorGreen = Color.FromHex("#3FE05A");
    public static readonly Color PhosphorDim = Color.FromHex("#1F8A33");
    private static readonly Color HudColour = Color.FromHex("#010401");
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

    private enum Flow : byte
    {
        Attract,
        Playing,
        FadeOut,
        FadeIn,
        Over,
    }

    private struct Particle
    {
        public Vector2 At;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public float Gravity;
        public float Drag;

        /// <summary>The y this drop lands on, or NaN to fly until it fades.</summary>
        public float Floor;

        public float Flat;
        public Color Colour;
        public byte Kind;
        public bool Stains;
        public bool Acid;
    }

    private struct Decal
    {
        public Vector2 At;
        public float Size;
        public float Rotation;
        public float Flat;
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

    private struct PendingSound
    {
        public float Delay;
        public string? Path;
        public float Volume;
        public float Pitch;

        /// <summary>1 for a male scream, 2 for a female one, 0 for <see cref="Path"/>.</summary>
        public int Scream;
    }

    private readonly System.Random _random = new();

    // reused vertex buffer: the client sandbox rejects stackalloc, and these are drawn every frame
    private readonly Vector2[] _quad = new Vector2[4];
    private readonly Dictionary<(string Rsi, string State), IRsiStateLike> _states = new();
    private readonly Dictionary<string, Texture> _images = new();
    private SpriteSystem? _sprites;

    private readonly CMUHuggerStage[] _stages;
    private int _stage;
    private Flow _flow = Flow.Attract;
    private float _flowTime;
    private float _card;
    private bool _paused;
    private readonly bool[] _held = new bool[6];

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _particleCount;
    private readonly Decal[] _decals = new Decal[MaxDecals];
    private int _decalCount;
    private int _decalNext;
    private readonly Popup[] _popups = new Popup[MaxPopups];
    private int _popupCount;
    private readonly PendingSound[] _pending = new PendingSound[MaxPending];
    private int _pendingCount;

    private float _distance;
    private int _milestones;
    private int _best;
    private int _bestShown = -1;
    private string _bestText = string.Empty;
    private float _slow;
    private float _shake;
    private float _shakeTime;
    private Vector2 _offset;
    private Color _flash;
    private float _flashLife;
    private float _flashAge;
    private float _hudFlash;
    private string _endText = string.Empty;
    private Color _endColour;

    private readonly string _title;
    private readonly string _start;
    private readonly string _keys;
    private readonly string _again;
    private readonly string _pausedText;

    // Set for each draw: the UI scale and the shake applied to the world (zero for overlays and the HUD).
    private float _s = 1;
    private Vector2 _o;

    public CMUHuggerRun(IGameTiming timing) : base(timing)
    {
        var cache = IoCManager.Resolve<IResourceCache>();

        // a wide face: these sizes keep the longest lines inside the 320 unit screen
        TextFont = cache.GetFont(FontPath, 8);
        SmallFont = cache.GetFont(FontPath, 7);
        SignFont = cache.GetFont(FontPath, 6);
        BigFont = cache.GetFont(FontPath, 16);
        CardFont = cache.GetFont(FontPath, 12);
        _title = Loc.GetString("cmu-game-hugger-title");
        _start = Loc.GetString("cmu-game-hugger-start");
        _keys = Loc.GetString("cmu-game-hugger-keys");
        _again = Loc.GetString("cmu-game-hugger-again");
        _pausedText = Loc.GetString("cmu-game-hugger-paused");

        // the campaign, in order
        _stages = new CMUHuggerStage[]
        {
            new CMUHuggerFacehugger(),
            new CMUHuggerLarva(),
            new CMUHuggerVent(),
            new CMUHuggerStealth(),
            new CMUHuggerRavager(),
        };

        foreach (var stage in _stages)
        {
            stage.Attach(this);
        }

        _stages[0].Enter();
    }

    public override string Help => Loc.GetString("cmu-game-hugger-help");

    #region For the stages

    public Font TextFont { get; }
    public Font SmallFont { get; }
    public Font SignFont { get; }
    public Font BigFont { get; }

    /// <summary>The stage card's title size for titles too wide for <see cref="BigFont"/>.</summary>
    private Font CardFont { get; }

    /// <summary>Real seconds since the cabinet was made, for blinking and pulsing.</summary>
    public float Clock { get; private set; }

    /// <summary>The title screen: only the first stage ever sees it, scrolling by under the title with nothing spawning.</summary>
    public bool Attract => _flow == Flow.Attract;

    /// <summary>The run has ended (lost or won). Stages keep updating so their ending plays out, but take no input.</summary>
    public bool Over => _flow == Flow.Over;

    /// <summary>Seconds since the run ended.</summary>
    public float OverTime { get; private set; }

    /// <summary>Where the world is seen from: a world point is drawn at <c>point - Camera</c>. Stages set it as they move.</summary>
    public Vector2 Camera;

    /// <summary>The run's score so far.</summary>
    public int Points => Score;

    /// <summary>Gameplay and effect randomness. The arcade has nothing to replay, so one source does for both.</summary>
    public System.Random Rng => _random;

    public float Fx()
    {
        return (float) _random.NextDouble();
    }

    /// <summary>Whether a key is held right now (keys are mapped as in <see cref="CMUArcadeKey"/>).</summary>
    public bool Held(CMUArcadeKey key)
    {
        return _held[(int) key];
    }

    public void AddScore(int points)
    {
        Score = Math.Clamp(Score + points, 0, CMUArcadeLimits.MaxScore);
        if (Score / Milestone <= _milestones)
            return;

        _milestones = Score / Milestone;
        _hudFlash = 0.6f;
        Play(Beep, -8, 1.2f);
    }

    /// <summary>Adds the points for running <paramref name="units"/> of world, keeping the fraction for next time.</summary>
    public void AddDistance(float units)
    {
        _distance += units;
        var whole = (int) (_distance / UnitsPerPoint);
        if (whole <= 0)
            return;

        _distance -= whole * UnitsPerPoint;
        AddScore(whole);
    }

    /// <summary>A bonus with its "+N" popup at a world point.</summary>
    public void Bonus(int points, Vector2 at, Color colour)
    {
        AddScore(points);
        Pop(Loc.GetString("cmu-game-hugger-bonus", ("bonus", points)), at, colour, 1.4f);
    }

    /// <summary>Ends this stage: the screen fades out, the next stage enters behind its title card. The last stage wins the run.</summary>
    public void NextStage()
    {
        if (_flow != Flow.Playing)
            return;

        if (_stage >= _stages.Length - 1)
        {
            Win(Loc.GetString("cmu-game-hugger-victory"));
            return;
        }

        _flow = Flow.FadeOut;
        _flowTime = 0;
    }

    /// <summary>The run is lost: big <paramref name="text"/> in the middle and the score goes to the board.</summary>
    public void Lose(string text, Color colour)
    {
        if (_flow != Flow.Playing)
            return;

        End(text, colour);
        Pend(0.6f, Lost, -6);
    }

    /// <summary>The run is won. The status line says so with the final score.</summary>
    public void Win(string text)
    {
        if (_flow != Flow.Playing)
            return;

        End(text, Acid);
        SetStatus(Loc.GetString("cmu-game-hugger-victory-status", ("score", Score)));
        Pend(0.3f, Won, -4);
    }

    private void End(string text, Color colour)
    {
        _flow = Flow.Over;
        OverTime = 0;
        _endText = text;
        _endColour = colour;
        GameOver();
        _best = Math.Max(_best, Score);
    }

    /// <summary>Slows everything (stages and effects) for a moment, for an impact.</summary>
    public void SlowMotion(float seconds)
    {
        _slow = MathF.Max(_slow, seconds);
    }

    #endregion

    #region Run flow and input

    public override void Start()
    {
        // a key mashed as the run ends must not throw it straight into a new one
        if (_flow == Flow.Over && OverTime < RestartDelay)
            return;

        base.Start();
        _flow = Flow.Playing;
        _distance = 0;
        _milestones = 0;
        _slow = 0;
        _pendingCount = 0;
        Array.Clear(_held);
        Enter(0);
        Play(NewGame, -6);
    }

    private void Enter(int stage)
    {
        _stage = stage;
        _particleCount = 0;
        _decalCount = 0;
        _decalNext = 0;
        _popupCount = 0;
        _shakeTime = 0;
        _flashLife = 0;
        _card = CardTime;
        Camera = Vector2.Zero;
        // a demo's bot starts each stage with its hands off the keys
        if (Demo)
            Array.Clear(_held);

        _stages[stage].Enter();
    }

    protected override void OnKey(CMUArcadeInput input)
    {
        if (input.Key != CMUArcadeKey.None)
            _held[(int) input.Key] = input.Down;

        if (input.Repeat)
            return;

        // W, Up and 8 start a run as well as Space
        if (!Running && input.Down && input.Key == CMUArcadeKey.Up)
        {
            Start();
            return;
        }

        if (_flow == Flow.Playing)
            _stages[_stage].OnKey(input);
    }

    /// <summary>A demo plays itself: the stage on screen gets to think and press keys while it is in play.</summary>
    protected override void DemoThink(float frameTime)
    {
        if (_flow == Flow.Playing && !_paused)
            _stages[_stage].DemoThink(MathF.Min(frameTime, 0.05f));
    }

    /// <summary>For a stage's demo bot: holds or lets go of a key, pressing it through the normal key handling like a player.</summary>
    public void Bot(CMUArcadeKey key, bool down)
    {
        if (Held(key) != down)
            DemoKey(key, down);
    }

    /// <summary>For a stage's demo bot: taps a key down and up.</summary>
    public void BotTap(CMUArcadeKey key)
    {
        DemoKey(key, true);
        DemoKey(key, false);
    }

    protected override void OnFocusLost()
    {
        Array.Clear(_held);
        _stages[_stage].OnRelease();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var real = MathF.Min(args.DeltaSeconds, 0.05f);
        Clock += real;

        // a run without the keys waits for them instead of running into the next bullet
        _paused = _flow is Flow.Playing or Flow.FadeIn or Flow.FadeOut && !Playing;
        if (_paused)
            return;

        if (_slow > 0)
            _slow -= real;

        var delta = _slow > 0 ? real * SlowFactor : real;
        switch (_flow)
        {
            case Flow.FadeOut:
                _flowTime += real;
                if (_flowTime >= FadeTime)
                {
                    _stages[_stage].Exit();
                    Enter(_stage + 1);
                    _flow = Flow.FadeIn;
                    _flowTime = 0;
                }

                break;
            case Flow.FadeIn:
                _flowTime += real;
                if (_flowTime >= FadeTime)
                    _flow = Flow.Playing;
                break;
            default:
                if (_flow == Flow.Over)
                    OverTime += real;

                if (_flow == Flow.Playing && _card > 0)
                    _card -= real;

                var stage = _stages[_stage];
                for (var left = delta; left > 0; left -= Step)
                {
                    stage.Update(MathF.Min(left, Step));
                }

                break;
        }

        UpdateEffects(delta);
        UpdateSounds(real);
    }

    #endregion

    #region Effects

    /// <summary>
    /// Throws particles from a world point. <paramref name="direction"/> and <paramref name="spread"/> are radians, 0
    /// pointing right and positive turning down the screen. Drops with a <paramref name="floor"/> land somewhere
    /// between it and <paramref name="depth"/> further down; staining ones leave a decal there, squashed by
    /// <paramref name="flat"/>. Staining ones with no floor (a top-down spray) leave their decal where they stop.
    /// </summary>
    public void Emit(Vector2 at, int count, Color colour, float speed, float life, float size, float direction, float spread,
        float gravity = 0, float floor = float.NaN, float depth = 0, bool stains = false, byte kind = Drop, bool acid = false,
        float inherit = 0, float drag = 0, float flat = 0.45f)
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
                Drag = drag,
                Floor = float.IsNaN(floor) ? floor : floor + Fx() * depth,
                Flat = flat,
                Colour = colour,
                Kind = kind,
                Stains = stains,
                Acid = acid,
            };
        }
    }

    /// <summary>Stamps a stain at a world point. <paramref name="flat"/> squashes it for a floor seen at a slant (1 is top-down).</summary>
    public void Splat(Vector2 at, float size, Color colour, float flat = 0.45f, bool acid = false)
    {
        _decals[_decalNext] = new Decal
        {
            At = at,
            Size = size,
            Rotation = Fx() * MathF.Tau,
            Flat = flat,
            State = _random.Next(6),
            Colour = colour,
            Acid = acid,
        };
        _decalNext = (_decalNext + 1) % MaxDecals;
        _decalCount = Math.Min(_decalCount + 1, MaxDecals);
    }

    /// <summary>Text that floats up from a world point and fades.</summary>
    public void Pop(string text, Vector2 at, Color colour, float life = 0.9f)
    {
        if (_popupCount < MaxPopups)
            _popups[_popupCount++] = new Popup { Text = text, At = at, Colour = colour, Life = life };
    }

    public void Shake(float strength, float time = 0.3f)
    {
        // a light knock does not cut short a big one still shaking
        if (_shakeTime > 0 && strength < _shake)
            return;

        _shake = strength;
        _shakeTime = time;
    }

    /// <summary>Washes the play area with a colour (its alpha is the strength) that fades over <paramref name="time"/>.</summary>
    public void Flash(Color colour, float time = 0.25f)
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
            else if (p.Drag > 0)
            {
                p.Velocity *= MathF.Max(0, 1 - p.Drag * dt);
            }

            p.At += p.Velocity * dt;
            var landed = !float.IsNaN(p.Floor) && p.At.Y >= p.Floor && p.Velocity.Y > 0;
            var expired = p.Age >= p.Life;
            if (p.Stains && (landed || expired && float.IsNaN(p.Floor)))
                Splat(landed ? p.At with { Y = p.Floor } : p.At, p.Size * 2.4f, p.Colour, p.Flat, p.Acid);

            if (landed || expired)
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

    #endregion

    #region Sound

    public void Play(string path, float volume = 0, float pitch = 1)
    {
        Sound(path, volume, pitch);
    }

    /// <summary>One of several takes, with a little pitch variation around <paramref name="pitch"/>.</summary>
    public void PlayAny(IReadOnlyList<string> paths, float volume = 0, float pitch = 1)
    {
        Sound(paths[_random.Next(paths.Count)], volume, pitch * (0.94f + Fx() * 0.12f));
    }

    public void Scream(bool female, float volume = 0)
    {
        CMUGameAudio.Scream(this, female, volume);
    }

    /// <summary>A sound a moment later, like the scream after the pounce.</summary>
    public void Pend(float delay, string path, float volume = 0, float pitch = 1)
    {
        if (_pendingCount < MaxPending)
            _pending[_pendingCount++] = new PendingSound { Delay = delay, Path = path, Volume = volume, Pitch = pitch };
    }

    public void PendScream(float delay, bool female, float volume = 0)
    {
        if (_pendingCount < MaxPending)
            _pending[_pendingCount++] = new PendingSound { Delay = delay, Volume = volume, Scream = female ? 2 : 1 };
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
                Scream(sound.Scream == 2, sound.Volume);
            else if (sound.Path != null)
                Play(sound.Path, sound.Volume, sound.Pitch);
        }
    }

    #endregion

    #region Sprites and people

    private IRsiStateLike State(string rsi, string state)
    {
        if (_states.TryGetValue((rsi, state), out var found))
            return found;

        _sprites ??= IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        found = _sprites.RsiStateLike(new SpriteSpecifier.Rsi(new ResPath(rsi), state));
        _states[(rsi, state)] = found;
        return found;
    }

    /// <summary>A frame of a sprite state facing a direction, cached; frames wrap, and one-direction states ignore it.</summary>
    public Texture Frame(string rsi, string state, RsiDirection direction = RsiDirection.South, int frame = 0)
    {
        var art = State(rsi, state);
        if (art.RsiDirections == RsiDirectionType.Dir1)
            direction = RsiDirection.South;

        return art.GetFrame(direction, art.IsAnimated ? frame % art.AnimationFrameCount : 0);
    }

    /// <summary>A plain texture file rather than an RSI state, such as a floor tile under <c>/Textures/_RMC14/Tiles/</c>, cached.</summary>
    public Texture Image(string path)
    {
        if (_images.TryGetValue(path, out var texture))
            return texture;

        texture = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>(path).Texture;
        _images[path] = texture;
        return texture;
    }

    public int FrameCount(string rsi, string state)
    {
        var art = State(rsi, state);
        return art.IsAnimated ? art.AnimationFrameCount : 1;
    }

    /// <summary>One of the six blood splatter states, greyscale, to tint.</summary>
    public Texture SplatterFrame(int state)
    {
        return Frame(Splatter, state switch
        {
            0 => "splatter-0",
            1 => "splatter-1",
            2 => "splatter-2",
            3 => "splatter-3",
            4 => "splatter-4",
            _ => "splatter-5",
        });
    }

    /// <summary>
    /// Dresses a person the way the game draws one: body parts, eyes, clothes and hair (or marine kit) in layer order,
    /// in all four directions. Picks sex, skin, clothes and hair at random. Call it again to reuse the person.
    /// </summary>
    public void Dress(CMUHuggerPerson person, CMUHuggerOutfit outfit)
    {
        person.Layers.Clear();
        person.Outfit = outfit;
        var marine = outfit != CMUHuggerOutfit.Colonist;
        person.Female = Fx() < (marine ? 0.2f : 0.45f);
        var skin = Skins[_random.Next(Skins.Length)];
        var sex = person.Female ? "_f" : "_m";
        Layer(person, Parts, "torso" + sex, skin);
        Layer(person, Parts, "head" + sex, skin);
        Layer(person, Eyes, "eyes", EyeColour);
        Layer(person, Parts, "r_arm", skin);
        Layer(person, Parts, "l_arm", skin);
        Layer(person, Parts, "r_leg", skin);
        Layer(person, Parts, "l_leg", skin);
        Layer(person, Clothing + (marine ? MarineUniform : Jumpsuits[_random.Next(Jumpsuits.Length)]), "equipped-INNERCLOTHING", Color.White);
        Layer(person, Parts, "l_foot", skin);
        Layer(person, Parts, "r_foot", skin);
        Layer(person, Parts, "l_hand", skin);
        Layer(person, Parts, "r_hand", skin);
        Layer(person, Clothing + (marine ? MarineBoots : Shoes[_random.Next(Shoes.Length)]), "equipped-FEET", Color.White);
        if (!marine)
        {
            var styles = person.Female ? FemaleHair : MaleHair;
            Layer(person, Hair, styles[_random.Next(styles.Length)], HairColours[_random.Next(HairColours.Length)]);
            return;
        }

        Layer(person, Clothing + MarineArmor, "equipped-OUTERCLOTHING", Color.White);
        Layer(person, Clothing + MarineHelmet, "equipped-HELMET", Color.White);
        if (outfit == CMUHuggerOutfit.Rifleman)
            Layer(person, Rifle, "wielded-inhand-left", Color.White);
        else if (outfit == CMUHuggerOutfit.Shotgunner)
            Layer(person, Shotgun, "wielded-inhand-left", Color.White);
        else
            Layer(person, Knife, "inhand-left", Color.White);
    }

    private void Layer(CMUHuggerPerson person, string rsi, string state, Color tint)
    {
        person.Layers.Add(new CMUHuggerPerson.Layer(
            Frame(rsi, state, RsiDirection.South), Frame(rsi, state, RsiDirection.North),
            Frame(rsi, state, RsiDirection.East), Frame(rsi, state, RsiDirection.West), tint));
    }

    /// <summary>The bayonet's icon, for drawing a stab bigger than the in-hand sprite shows it.</summary>
    public Texture KnifeIcon => Frame(Knife, "icon");

    /// <summary>
    /// Draws a dressed person. <paramref name="at"/> is the screen point under their feet (the sprite's bottom
    /// middle), or the sprite's centre when <paramref name="centred"/> (for top-down views). The sprite is 32 px
    /// drawn at <paramref name="scale"/>, turned by <paramref name="rotation"/> radians about that point.
    /// </summary>
    public void DrawPerson(DrawingHandleScreen handle, CMUHuggerPerson person, Vector2 at, RsiDirection direction, float scale,
        float rotation = 0, bool centred = false, Color? tint = null)
    {
        var size = 32 * scale * _s;
        var saved = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation((at + _o) * _s) * saved);
        var box = centred ? new UIBox2(-size / 2, -size / 2, size / 2, size / 2) : new UIBox2(-size / 2, -size, size / 2, 0);
        var modulate = tint ?? Phosphor;
        foreach (var layer in person.Layers)
        {
            handle.DrawTextureRect(layer.Facing(direction), box, layer.Tint * modulate);
        }

        handle.SetTransform(saved);
    }

    #endregion

    #region Drawing

    protected override void DrawGame(DrawingHandleScreen handle)
    {
        _s = UIScale;
        _o = _offset;
        var stage = _stages[_stage];
        stage.DrawBack(handle);
        DrawDecals(handle);
        stage.DrawFront(handle);
        DrawParticles(handle);
        DrawPopups(handle);
        if (_flashLife > 0 && _flashAge < _flashLife)
            Fill(handle, 0, HudHeight, ScreenWidth, ScreenHeight - HudHeight, _flash.WithAlpha(_flash.A * (1 - _flashAge / _flashLife)));

        _o = Vector2.Zero;
        stage.DrawOverlay(handle);
        var fade = _flow switch
        {
            Flow.FadeOut => _flowTime / FadeTime,
            Flow.FadeIn => 1 - _flowTime / FadeTime,
            _ => 0,
        };

        if (fade > 0)
            Fill(handle, 0, HudHeight, ScreenWidth, ScreenHeight - HudHeight, Color.Black.WithAlpha(Math.Clamp(fade, 0, 1)));

        DrawHud(handle, stage);
        DrawCrt(handle);
    }

    private void DrawDecals(DrawingHandleScreen handle)
    {
        var saved = handle.GetTransform();
        var start = _decalCount < MaxDecals ? 0 : _decalNext;
        for (var i = 0; i < _decalCount; i++)
        {
            var decal = _decals[(start + i) % MaxDecals];
            var at = decal.At - Camera;
            if (at.X < -decal.Size || at.X > ScreenWidth + decal.Size || at.Y < -decal.Size || at.Y > ScreenHeight + decal.Size)
                continue;

            var colour = decal.Colour;
            if (decal.Acid)
                colour = Color.InterpolateBetween(colour, DarkAcid.WithAlpha(colour.A), MathF.Min(1, decal.Age / 4));

            var half = decal.Size * _s / 2;
            handle.SetTransform(Matrix3x2.CreateRotation(decal.Rotation) * Matrix3x2.CreateScale(1, decal.Flat)
                * Matrix3x2.CreateTranslation((at + _o) * _s) * saved);
            var texture = SplatterFrame(decal.State);
            handle.DrawTextureRect(texture, new UIBox2(-half, -half, half, half), colour);
            if (decal.Acid && decal.Age < 2.5f)
                handle.DrawTextureRect(texture, new UIBox2(-half, -half, half, half), Acid.WithAlpha(0.5f * (1 - decal.Age / 2.5f)));
        }

        handle.SetTransform(saved);
    }

    private void DrawParticles(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _particleCount; i++)
        {
            var p = _particles[i];
            var alpha = Math.Clamp(1 - p.Age / p.Life, 0, 1);
            var colour = p.Colour.WithAlpha(p.Colour.A * alpha);
            var at = p.At - Camera;
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

    private void DrawPopups(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _popupCount; i++)
        {
            var popup = _popups[i];
            if (popup.Text == null)
                continue;

            var progress = popup.Age / popup.Life;
            var at = popup.At - Camera;
            CentreText(handle, SmallFont, popup.Text, at.X, at.Y - 18 * progress, popup.Colour.WithAlpha(Math.Clamp(2 - progress * 2, 0, 1)));
        }
    }

    private void DrawHud(DrawingHandleScreen handle, CMUHuggerStage stage)
    {
        Fill(handle, 0, 0, ScreenWidth, HudHeight, HudColour);
        Fill(handle, 0, HudHeight - 1, ScreenWidth, 1, _hudFlash > 0 && _hudFlash % 0.2f > 0.1f ? Bright : Dim);
        if (_best != _bestShown)
        {
            _bestShown = _best;
            _bestText = Loc.GetString("cmu-game-hugger-best", ("score", _best.ToString("D5")));
        }

        var width = handle.GetDimensions(SmallFont, _bestText, _s).X / _s;
        Text(handle, SmallFont, _bestText, ScreenWidth - 6 - width, 3, Dim);

        var focused = HasKeyboardFocus();
        var blink = Clock % 1.1f < 0.75f;
        switch (_flow)
        {
            case Flow.Attract:
                CentreText(handle, BigFont, _title, ScreenWidth / 2, 62, Green);
                if (focused && blink)
                    CentreText(handle, TextFont, _start, ScreenWidth / 2, 92, Bright);

                CentreText(handle, SmallFont, _keys, ScreenWidth / 2, 108, Dim);
                return;
            case Flow.Over:
                if (OverTime < 0.15f)
                    return;

                var end = handle.GetDimensions(BigFont, _endText, _s).X / _s <= ScreenWidth - 16 ? BigFont : CardFont;
                CentreText(handle, end, _endText, ScreenWidth / 2, 66, _endColour);
                if (focused && OverTime >= RestartDelay && blink)
                    CentreText(handle, TextFont, _again, ScreenWidth / 2, 96, Bright);
                return;
        }

        if (_paused)
        {
            CentreText(handle, BigFont, _pausedText, ScreenWidth / 2, 80, Green);
            return;
        }

        // the stage card, over the start of each stage
        if (_flow == Flow.FadeOut || _card <= 0)
            return;

        var alpha = Math.Clamp(_card / 0.4f, 0, 1);
        // a long title steps down a size rather than running off the screen
        var title = handle.GetDimensions(BigFont, stage.Title, _s).X / _s <= ScreenWidth - 16 ? BigFont : CardFont;
        CentreText(handle, title, stage.Title, ScreenWidth / 2, 54, Green.WithAlpha(alpha));
        CentreText(handle, SmallFont, stage.Hint, ScreenWidth / 2, 80, Bright.WithAlpha(alpha));
    }

    /// <summary>Scanlines, a slow roll bar and darkened edges, so the whole screen reads as an old tube.</summary>
    private void DrawCrt(DrawingHandleScreen handle)
    {
        var line = Color.Black.WithAlpha(0.22f);
        for (var y = 1f; y < ScreenHeight; y += 2)
        {
            Fill(handle, 0, y, ScreenWidth, 1, line);
        }

        var roll = Clock * 26 % (ScreenHeight + 60) - 30;
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

    // Everything takes screen units (world point minus Camera) and applies the UI scale and, while the world is
    // drawn, the shake.

    public void Fill(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        x += _o.X;
        y += _o.Y;
        handle.DrawRect(new UIBox2(x * _s, y * _s, (x + width) * _s, (y + height) * _s), colour);
    }

    public void Outline(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        x += _o.X;
        y += _o.Y;
        handle.DrawRect(new UIBox2(x * _s, y * _s, (x + width) * _s, (y + height) * _s), colour, false);
    }

    public void Line(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color colour)
    {
        handle.DrawLine((from + _o) * _s, (to + _o) * _s, colour);
    }

    public void Circle(DrawingHandleScreen handle, Vector2 at, float radius, Color colour, bool filled = true)
    {
        handle.DrawCircle((at + _o) * _s, radius * _s, colour, filled);
    }

    public void Ellipse(DrawingHandleScreen handle, Vector2 at, float radiusX, float radiusY, Color colour)
    {
        if (radiusX <= 0 || radiusY <= 0)
            return;

        var saved = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(1, radiusY / radiusX) * Matrix3x2.CreateTranslation((at + _o) * _s) * saved);
        handle.DrawCircle(Vector2.Zero, radiusX * _s, colour);
        handle.SetTransform(saved);
    }

    /// <summary>A straight bar of some width from one point to another, for tracers and beams.</summary>
    public void Beam(DrawingHandleScreen handle, Vector2 from, Vector2 to, float width, Color colour)
    {
        var along = to - from;
        var length = along.Length();
        if (length < 0.01f)
            return;

        var side = new Vector2(-along.Y, along.X) / length * (width / 2);
        _quad[0] = (from + side + _o) * _s;
        _quad[1] = (from - side + _o) * _s;
        _quad[2] = (to + side + _o) * _s;
        _quad[3] = (to - side + _o) * _s;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _quad, colour);
    }

    /// <summary>A filled four-sided shape, corners in order round its edge.</summary>
    public void Quad(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
    {
        _quad[0] = (a + _o) * _s;
        _quad[1] = (b + _o) * _s;
        _quad[2] = (c + _o) * _s;
        _quad[3] = (d + _o) * _s;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _quad, colour);
    }

    /// <summary>Draws a texture centred on a screen point at a size, turned by <paramref name="rotation"/> radians.</summary>
    public void DrawSprite(DrawingHandleScreen handle, Texture texture, Vector2 at, Vector2 size, float rotation, Color modulate)
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

    /// <summary>
    /// Draws part of a texture into a screen rectangle, for cutting or stretching a sprite. <paramref name="source"/>
    /// is in the texture's own pixels (for an RSI frame, 0..32).
    /// </summary>
    public void DrawRegion(DrawingHandleScreen handle, Texture texture, float x, float y, float width, float height, UIBox2 source, Color modulate)
    {
        x += _o.X;
        y += _o.Y;
        handle.DrawTextureRectRegion(texture, new UIBox2(x * _s, y * _s, (x + width) * _s, (y + height) * _s), source, modulate);
    }

    /// <summary>Phosphor text from its top left: a dark drop, a soft glow either side, then the letters.</summary>
    public void Text(DrawingHandleScreen handle, Font font, string text, float x, float y, Color colour)
    {
        var at = (new Vector2(x, y) + _o) * _s;
        handle.DrawString(font, at + new Vector2(_s, _s), text, _s, Color.Black.WithAlpha(colour.A * 0.8f));
        handle.DrawString(font, at + new Vector2(-_s, 0), text, _s, colour.WithAlpha(colour.A * 0.25f));
        handle.DrawString(font, at + new Vector2(_s, 0), text, _s, colour.WithAlpha(colour.A * 0.25f));
        handle.DrawString(font, at, text, _s, colour);
    }

    public void CentreText(DrawingHandleScreen handle, Font font, string text, float x, float y, Color colour)
    {
        var width = handle.GetDimensions(font, text, _s).X / _s;
        Text(handle, font, text, x - width / 2, y, colour);
    }

    #endregion

    #region Scenery helpers

    private static uint Mix(int n)
    {
        var h = (uint) n * 0x9E3779B1u;
        h ^= h >> 15;
        h *= 0x85EBCA77u;
        h ^= h >> 13;
        h *= 0xC2B2AE3Du;
        h ^= h >> 16;
        return h;
    }

    /// <summary>A steady pseudo-random number for a spot in the world, so scenery does not flicker as it scrolls. Same as the JS port.</summary>
    public static uint Hash(int n)
    {
        return Mix(n);
    }

    /// <summary>A steady 0..1 for a spot in the world and a salt.</summary>
    public static float Hash01(int n, int salt)
    {
        return (Mix(n * 31 + salt) & 0xFFFF) / 65535f;
    }

    #endregion
}

/// <summary>What a <see cref="CMUHuggerPerson"/> wears: a colonist, or a marine with an M41A, a bayonet or an M37A1 shotgun.</summary>
public enum CMUHuggerOutfit : byte
{
    Colonist,
    Rifleman,
    Knifeman,
    Shotgunner,
}

/// <summary>A human built from the game's body part and clothing sprites by <see cref="CMUHuggerRun.Dress"/>.</summary>
public sealed class CMUHuggerPerson
{
    public readonly record struct Layer(Texture South, Texture North, Texture East, Texture West, Color Tint)
    {
        public Texture Facing(RsiDirection direction)
        {
            return direction switch
            {
                RsiDirection.North => North,
                RsiDirection.East => East,
                RsiDirection.West => West,
                _ => South,
            };
        }
    }

    public CMUHuggerOutfit Outfit;
    public bool Female;
    public readonly List<Layer> Layers = new();
}

/// <summary>
/// One stage of the Hugger Run campaign. The framework (<see cref="CMUHuggerRun"/>) makes each stage once, calls
/// <see cref="Enter"/> every time the stage begins, then <see cref="Update"/> in fixed steps and the three draw
/// calls every frame until the stage calls <see cref="CMUHuggerRun.NextStage"/>, <see cref="CMUHuggerRun.Lose"/> or
/// <see cref="CMUHuggerRun.Win"/>. After a loss or win the stage keeps updating (with <see cref="CMUHuggerRun.Over"/>
/// set) so its ending plays out. See hugger-stage-contract.md.
/// </summary>
public abstract class CMUHuggerStage
{
    protected CMUHuggerRun Run = default!;

    internal void Attach(CMUHuggerRun run)
    {
        Run = run;
    }

    /// <summary>The stage card's title, like "STAGE 2 - LARVA".</summary>
    public abstract string Title { get; }

    /// <summary>The line under the title on the card: what to do here.</summary>
    public abstract string Hint { get; }

    /// <summary>Starts the stage from scratch. Effects are already cleared and the camera is at zero.</summary>
    public abstract void Enter();

    /// <summary>Advances the stage by <paramref name="dt"/> seconds (at most <see cref="CMUHuggerRun.Step"/>).</summary>
    public abstract void Update(float dt);

    /// <summary>A key went down or up while the stage plays. Held keys can also be read with <see cref="CMUHuggerRun.Held"/>.</summary>
    public virtual void OnKey(CMUArcadeInput input)
    {
    }

    /// <summary>The keys went elsewhere: let go of anything held.</summary>
    /// <summary>
    /// Plays the stage in a demo (a "Let's Play" tape): called every frame the stage is in play, before it updates. Look
    /// at the stage and press keys with <see cref="CMUHuggerRun.Bot"/> or <see cref="CMUHuggerRun.BotTap"/>, like a
    /// decent human: react a beat late, sometimes fumble. The default does nothing, so the stage just runs.
    /// </summary>
    public virtual void DemoThink(float dt)
    {
    }

    public virtual void OnRelease()
    {
    }

    /// <summary>The stage is over and the next one is about to enter.</summary>
    public virtual void Exit()
    {
    }

    /// <summary>The scenery, under the decals.</summary>
    public abstract void DrawBack(DrawingHandleScreen handle);

    /// <summary>Actors and anything over the decals, under the particles and popups.</summary>
    public virtual void DrawFront(DrawingHandleScreen handle)
    {
    }

    /// <summary>Over everything but the HUD, unshaken: cutscene text, prompts.</summary>
    public virtual void DrawOverlay(DrawingHandleScreen handle)
    {
    }
}
