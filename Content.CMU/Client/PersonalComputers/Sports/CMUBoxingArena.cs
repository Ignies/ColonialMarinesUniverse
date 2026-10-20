using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Boxing: red corner against blue under the lights, three rounds, the crowd in the dark. Every pose is worked out
/// from the match time and its events, so a late joiner sees the same fight: jabs, crosses, hooks and uppercuts
/// with a wind-up and a recoil, heads snapping back, cuts that bleed more each round, teeth on the canvas, the
/// referee's count over a knocked down boxer lying in a spreading pool, and the corner men and the ring card
/// between rounds.
/// </summary>
public sealed partial class CMUBoxingArena : CMUSportsArena
{
    // the ring, in arena units: a canvas seen from the front row, back edge narrower than the front
    private const float Feet = 191;
    private const float BoxerY = Feet - 32;
    private const float LyingY = Feet - 9;
    private const float CanvasBack = 124;
    private const float CanvasFront = 206;
    private const float BackLeft = 70;
    private const float BackRight = 450;
    private const float FrontLeft = 26;
    private const float FrontRight = 494;
    private const float BackPost = 46;
    private const float FrontPost = 58;
    private const float Middle = ArenaWidth / 2;

    private const float RefScale = 1.7f;
    private const float RefFeet = 177;
    private const float RefY = RefFeet - 16 * RefScale;
    private const float WalkerScale = 1.9f;
    private const float WalkerFeet = 186;
    private const float WalkerY = WalkerFeet - 16 * WalkerScale;
    private const float CornerFeet = 200;
    private const float CornerY = CornerFeet - 32;

    private const int RefDoll = 0;
    private const int WalkerDoll = 1;
    private const int BoxerDoll = 2;
    private const int CornerDoll = 4;

    /// <summary>When the referee has counted ten over a knocked out boxer, from the knockout.</summary>
    private const float Counted = CMUSports.BoxingCountDelay + 9 * CMUSports.BoxingCountStep + 0.3f;

    private const float ToothFlight = 0.55f;

    private static readonly float[] Ropes = { 0.27f, 0.52f, 0.77f };
    private static readonly Color[] RopeColours = { Color.FromHex("#C8102E"), Color.FromHex("#E8E8EC"), Color.FromHex("#1F4FC8") };
    private static readonly float[] Corners = { 92, 428 };
    private static readonly Color[] Gloves = { Color.FromHex("#C8102E"), Color.FromHex("#2152D6") };
    private static readonly Color[] Plates = { Color.FromHex("#FF6B6B"), Color.FromHex("#6BA8FF") };
    private static readonly Color Skin = Color.FromHex("#E2B08A");
    private static readonly Color Sweat = Color.FromHex("#DDF1FFCC");
    private static readonly Color Water = Color.FromHex("#9FD8FFCC");
    private static readonly Color Shadow = Color.Black.WithAlpha(0.35f);
    private static readonly string[] Numbers = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
    private const string Splatter = "/Textures/Fluids/splatter.rsi";
    private static readonly string[] Splatters = { "splatter-0", "splatter-1", "splatter-2", "splatter-3", "splatter-4", "splatter-5" };

    private static readonly string[] Punches =
        { "/Audio/Weapons/boxingpunch1.ogg", "/Audio/Weapons/boxingpunch2.ogg", "/Audio/Weapons/boxingpunch3.ogg" };

    private static readonly string[] Flesh =
    {
        "/Audio/_RMC14/Bullets/impact_flesh_1.ogg", "/Audio/_RMC14/Bullets/impact_flesh_2.ogg",
        "/Audio/_RMC14/Bullets/impact_flesh_3.ogg", "/Audio/_RMC14/Bullets/impact_flesh_4.ogg",
    };

    private static readonly string[] Claps =
    {
        "/Audio/Effects/Emotes/clap1.ogg", "/Audio/Effects/Emotes/clap2.ogg", "/Audio/Effects/Emotes/clap3.ogg",
        "/Audio/Effects/Emotes/clap4.ogg",
    };

    private static readonly string[] Gasps =
    {
        "/Audio/Effects/Gasp/gasp_male1.ogg", "/Audio/Effects/Gasp/gasp_male2.ogg", "/Audio/Effects/Gasp/gasp_female1.ogg",
        "/Audio/Effects/Gasp/gasp_female2.ogg", "/Audio/Effects/Gasp/gasp_female3.ogg",
    };

    private static readonly string[] Whistles =
        { "/Audio/Voice/Human/whistle_1.ogg", "/Audio/Voice/Human/whistle_2.ogg", "/Audio/Voice/Human/whistle_3.ogg", "/Audio/Voice/Human/whistle_4.ogg" };

    private static readonly string[] Tinks =
    {
        "/Audio/Weapons/Guns/Casings/casing_fall_1.ogg", "/Audio/Weapons/Guns/Casings/casing_fall_2.ogg",
        "/Audio/Weapons/Guns/Casings/casing_fall_3.ogg",
    };

    private static readonly string[] Splashes =
        { "/Audio/_RMC14/Effects/Water/shallowwading1.ogg", "/Audio/_RMC14/Effects/Water/shallowwading2.ogg" };

    private static readonly string[] Bleeding = { "/Audio/Effects/Fluids/blood1.ogg", "/Audio/Effects/Fluids/blood2.ogg" };

    private static readonly string[] Heavy =
        { "/Audio/CMU14/PersonalComputers/Games/punch_1.ogg", "/Audio/CMU14/PersonalComputers/Games/punch_2.ogg" };

    private static readonly string[] Swooshes =
    {
        "/Audio/CMU14/PersonalComputers/Games/swoosh_1.ogg", "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg",
        "/Audio/CMU14/PersonalComputers/Games/swoosh_3.ogg",
    };

    private static readonly string[] Scuffles =
        { "/Audio/CMU14/PersonalComputers/Games/boxing_scuffle_1.ogg", "/Audio/CMU14/PersonalComputers/Games/boxing_scuffle_2.ogg" };

    private static readonly string[] Cheers =
        { "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_1.ogg", "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_2.ogg" };

    // the round's opening bell, the bell that ends it, and the one rung over a knockout or the decision
    private const string OpeningBell = "/Audio/CMU14/PersonalComputers/Games/boxing_bell_1.ogg";
    private const string ClosingBell = "/Audio/CMU14/PersonalComputers/Games/boxing_bell_2.ogg";
    private const string FinalBell = "/Audio/CMU14/PersonalComputers/Games/boxing_bell_3.ogg";
    private const string SmallCheer = "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_small.ogg";
    private const string Fall = "/Audio/CMU14/PersonalComputers/Games/fall.ogg";
    private const string Crunch = "/Audio/_RMC14/Weapons/synthpunch1.ogg";
    private const string Thump = "/Audio/Effects/soft_thump.ogg";
    private const string Thud = "/Audio/Effects/Footsteps/largethud.ogg";
    private const string Camera = "/Audio/Misc/camera_snap.ogg";
    private const string Clap = "/Audio/Effects/Emotes/clap-single.ogg";

    /// <summary>A seat in the stands, worked out once: where it is, how its fan bobs, whether they throw their arms up.</summary>
    private readonly record struct Seat(float X, int Row, float Phase, bool Arms);

    /// <summary>One layer of a fan, in drawing order. Arm layers are left off when the arms go up.</summary>
    private readonly record struct FanLayer(string Rsi, string State, Color? Tint, bool Arm);

    // The fans are the game's people: body parts, eyes, hair and clothes in layer order, as Hugger Run builds them.
    private const string FanParts = "/Textures/Mobs/Species/Human/parts.rsi";
    private const string FanEyes = "/Textures/Mobs/Customization/eyes.rsi";
    private const string FanHair = "/Textures/Mobs/Customization/human_hair.rsi";
    private const string FanClothes = "/Textures/_RMC14/Objects/Clothing/";
    private const string FanColourSuit = "/Textures/Clothing/Uniforms/Jumpsuits/Color/color.rsi";
    private const string FanMarineCap = "Head/Hats/cap.rsi";
    private const int FanColumns = 32;

    private static readonly string[] FanCivilian =
    {
        "Uniforms/Civilian/blue_jumpsuit.rsi", "Uniforms/Civilian/brown_utility.rsi", "Uniforms/Civilian/khaki_workwear.rsi",
        "Uniforms/Civilian/grey_jumpsuit.rsi", "Uniforms/Civilian/green_utility.rsi", "Uniforms/Civilian/blue_workwear.rsi",
        "Uniforms/Civilian/lightbrown_jumpsuit.rsi", "Uniforms/Civilian/doctor.rsi", "Uniforms/Civilian/emt.rsi",
        "Uniforms/Civilian/red_utility.rsi", "Uniforms/Civilian/yellow_jumpsuit.rsi", "Uniforms/Civilian/sanitation.rsi",
        "Uniforms/Civilian/weyamart.rsi", "Uniforms/Civilian/daily_grind.rsi", "Uniforms/Civilian/tartarus_mining.rsi",
        "Uniforms/Civilian/scientist_jumpsuit.rsi", "Uniforms/Civilian/pizza_galaxy.rsi", "Uniforms/Civilian/white_jumpsuit_alt.rsi",
    };

    private static readonly string[] FanMarine = { "Uniforms/Marine/standard.rsi", "Uniforms/Marine/engineer.rsi", "Uniforms/Marine/medic.rsi" };

    private static readonly string[] FanHats =
    {
        "Head/Beanies/beanie.rsi", "Head/Beanies/gray.rsi", "Head/Beanies/green.rsi", "Head/Hats/blue_trucker_cap.rsi",
        "Head/Hats/cargo_cap.rsi", "Head/Hats/bucket.rsi", "Head/Hats/durag.rsi", "Head/Hats/pizza_galaxy_cap.rsi",
    };

    private static readonly string[] FanMaleHair = { "crewcut", "business", "bedhead", "buzzcut", "afro", "sidecut", "bald" };
    private static readonly string[] FanFemaleHair = { "ponytail", "bob", "bun", "longstraightponytail", "long", "pixie", "bobcurl", "ponytail2" };

    private static readonly Color[] FanHairColours =
    {
        Color.FromHex("#2A1A10"), Color.FromHex("#5A3A20"), Color.FromHex("#C89A50"), Color.FromHex("#1A1A1A"),
        Color.FromHex("#8A3A1A"), Color.FromHex("#D8C078"), Color.FromHex("#8C8C8C"),
    };

    private static readonly Color FanEyeColour = Color.FromHex("#2B1A10");

    /// <summary>The corners' jumpsuits, ClothingUniformJumpsuitColorRed and Blue, for the fans who came for one fighter.</summary>
    private static readonly Color[] FanCorners = { Color.FromHex("#eb0c07"), Color.FromHex("#52aecc") };

    /// <summary>Fans are drawn this size per row, back to front, and darker the further back they sit.</summary>
    private static readonly float[] FanScale = { 0.8f, 0.9f, 1f, 1.1f, 1.2f };

    private static readonly float[] FanLight = { 0.22f, 0.29f, 0.36f, 0.43f, 0.5f };

    /// <summary>The layer images the fans are built from, read once for every arena.</summary>
    private static readonly Dictionary<string, Image<Rgba32>?> FanImages = new();

    private static readonly float[] RowY = { 50, 64, 79, 94, 109 };
    private static readonly Seat[] Seats = MakeSeats();

    /// <summary>A knockdown or knockout: where the boxer fell, and where their head lies in the blood.</summary>
    private readonly record struct Down(float Time, int Side, int Count, bool Out, int Index, Vector2 Face, Vector2 Pool);

    /// <summary>A tooth: knocked out at a time, flying from a face to its spot on the canvas.</summary>
    private readonly record struct Tooth(float Time, int Side, Vector2 From, Vector2 To, int Index);

    private struct Boxer
    {
        public float X;
        public float Y;

        /// <summary>Tilt in degrees, positive turning anticlockwise.</summary>
        public float Rot;

        public Direction Facing;
        public bool Visible;
        public bool Down;

        /// <summary>The gloves in front of the face: the fighting stance.</summary>
        public bool Arms;

        /// <summary>Both gloves up around the head, taking a punch on them.</summary>
        public bool Guard;

        /// <summary>Both gloves over the head, celebrating.</summary>
        public bool Raised;

        /// <summary>One glove held up by the referee.</summary>
        public bool HandUp;

        /// <summary>The punch being thrown, or -1.</summary>
        public int Punch;

        public bool Lead;
        public Vector2 Glove;
        public Vector2 Shoulder;

        /// <summary>How red the boxer flashes from a hit, 0 to 1.</summary>
        public float Hurt;
    }

    /// <summary>Anyone else in the ring: the referee, the ring card walker, the corner men.</summary>
    private struct Figure
    {
        public float X;
        public float Y;
        public Direction Facing;
        public bool Visible;
    }

    private enum RefArms : byte
    {
        None,
        Count,
        Wave,
        Break,
        Raise,
    }

    private struct CameraFlash
    {
        public float X;
        public float Y;
        public float Born;
        public float Size;
    }

    private CMUBetMatch? _match;
    private CMUSportsPlay? _play;
    private readonly List<Down> _downs = new();
    private readonly List<float> _clinches = new();
    private readonly List<(float Time, int Side, int Level)> _cuts = new();
    private readonly List<Tooth> _teeth = new();
    private readonly (int Boss, int Red, int Blue)[] _cards = new (int, int, int)[CMUSports.BoxingRounds];
    private int _ko = -1;

    private readonly Boxer[] _boxers = new Boxer[2];
    private readonly Boxer[] _then = new Boxer[2];
    private readonly Boxer[] _next = new Boxer[2];
    private readonly int[] _landed = new int[2];
    private readonly int[] _cutLevel = new int[2];
    private readonly Figure[] _corner = new Figure[2];
    private readonly Vector2?[] _sponge = new Vector2?[2];
    private Figure _ref;
    private Figure _walker;
    private RefArms _refArms;
    private float _refPhase;
    private int _refPointing = 1;
    private Vector2 _refTarget;
    private int _sign;
    private bool _resting;
    private float _spot = Middle;
    private float _hype;
    private float _t = -1;
    private float _lastT = float.NaN;
    private float _lastReal;

    /// <summary>Until when, in real seconds, a big or a small cheer is still running, so they never stack.</summary>
    private float _cheering;

    private float _cheeringSmall;

    private readonly CameraFlash[] _flashes = new CameraFlash[24];
    private int _nextFlash;
    private readonly Vector2[] _fan = new Vector2[18];
    private readonly Vector2[] _quad = new Vector2[4];
    private Texture[]? _spark;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private ILogManager _logs = default!;

    /// <summary>The stands of the match on show: two cells per seat, arms down and arms up, darkened for its row.</summary>
    private OwnedTexture? _crowd;

    private int _crowdId = -1;
    private readonly int[] _fanCorner = new int[Seats.Length];
    private Color[] _sleeves = { Color.White, Color.White };
    private string _apron = string.Empty;
    private readonly string[] _signs = new string[CMUSports.BoxingRounds + 1];

    protected override void Build(CMUBetMatch match)
    {
        // drawing order is depth: the referee and the ring card walker stand behind the boxers, the corner men in front
        AddDoll(CMUSports.Kit("Runner", 6), RefScale);
        AddDoll(CMUSports.Kit("Runner", 3), WalkerScale);
        AddDoll(CMUSports.Kit("Boxer", 0), 2f);
        AddDoll(CMUSports.Kit("Boxer", 1), 2f);
        AddDoll(CMUSports.Kit("Player", 7), 2f);
        AddDoll(CMUSports.Kit("Player", 7), 2f);

        _match = match;
        _play = match.Winner >= 0 ? CMUSports.Play(match.Sport, match.Seed, match.Entrants.Count, match.Winner) : null;
        _lastT = float.NaN;
        _sleeves = new[] { Darker(KitColour(match, 0)), Darker(KitColour(match, 1)) };
        _apron = Loc.GetString("cmu-game-boxing-apron");
        for (var round = 1; round <= CMUSports.BoxingRounds; round++)
        {
            _signs[round] = Loc.GetString("cmu-sport-round", ("round", round));
        }

        Index();
    }

    /// <summary>Picks out the moments the drawing needs to look up every frame, and where things land.</summary>
    private void Index()
    {
        _downs.Clear();
        _clinches.Clear();
        _cuts.Clear();
        _teeth.Clear();
        _ko = -1;
        Array.Fill(_cards, (-1, 0, 0));
        if (_play == null)
            return;

        var events = _play.Events;
        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            switch (ev.Action)
            {
                case CMUSportsAction.Knockdown:
                case CMUSportsAction.Knockout:
                {
                    var away = Dir(1 - ev.Side);
                    FightBase(ev.Time, _then);
                    var fallen = _then[ev.Side];
                    var face = Offset(fallen, Dir(ev.Side) * 8, -17);
                    DownPose(ref fallen, _then[ev.Side], 1.5f, away, -1);
                    var head = Offset(fallen, 0, -16);
                    var knockout = ev.Action == CMUSportsAction.Knockout;
                    _downs.Add(new Down(ev.Time, ev.Side, knockout ? -1 : ev.Value, knockout, i, face, new Vector2(head.X, Feet - 3)));
                    if (knockout)
                        _ko = _downs.Count - 1;

                    break;
                }
                case CMUSportsAction.Injury:
                    _cuts.Add((ev.Time, ev.Side, ev.Extra));
                    break;
                case CMUSportsAction.Special when ev.Value == CMUSports.BoxingClinch:
                    _clinches.Add(ev.Time);
                    break;
                case CMUSportsAction.Special when ev.Value == CMUSports.BoxingCard:
                    _cards[RoundOf(ev.Time) - 1] = (ev.Side, ev.Extra, ev.State);
                    break;
                case CMUSportsAction.Special when ev.Value == CMUSports.BoxingTooth:
                {
                    FightBase(ev.Time, _then);
                    var from = Offset(_then[ev.Side], Dir(ev.Side) * 8, -15);
                    var away = Dir(1 - ev.Side);
                    var to = new Vector2(Math.Clamp(from.X + away * (26 + Hash(i) * 36), 44, ArenaWidth - 44), Feet - 6 + Hash(i + 7) * 12);
                    _teeth.Add(new Tooth(ev.Time, ev.Side, from, to, i));
                    break;
                }
            }
        }
    }

    #region Posing

    protected override void UpdateMatch(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        var now = (float) Timing.RealTime.TotalSeconds;
        var delta = Math.Clamp(now - _lastReal, 0, 0.1f);
        _lastReal = now;
        if (play == null || _play == null)
            t = -1;

        // the first frame, or a replay starting over: nothing has just happened
        if (float.IsNaN(_lastT) || t < _lastT - 0.05f)
            _lastT = t;

        _t = t;
        Stage(t, now);
        PoseDolls();
        Scoreboard(match, t);
        Moments(t);
        Ambience(t, delta);
        _lastT = t;
    }

    /// <summary>Where everyone is at <paramref name="t"/> seconds from kickoff.</summary>
    private void Stage(float t, float now)
    {
        _ref = new Figure { X = Middle, Y = RefY, Facing = Direction.South, Visible = true };
        _walker = default;
        _corner[0] = _corner[1] = default;
        _sponge[0] = _sponge[1] = null;
        _refArms = RefArms.None;
        _sign = 0;
        _resting = false;

        if (_play == null || t < 0)
        {
            Waiting(now);
            return;
        }

        if (t < 4)
        {
            Introductions(t);
            return;
        }

        var round = RoundOf(t);
        var start = RoundStart(round);
        var into = t - start;
        if (_ko >= 0 && t >= _downs[_ko].Time)
        {
            KnockedOut(t, _downs[_ko]);
            return;
        }

        if (into >= CMUSports.RoundLength)
        {
            if (round == CMUSports.BoxingRounds)
                Decision(t, start);
            else
                Rest(t, round, start, into - CMUSports.RoundLength);

            return;
        }

        Fight(t);
    }

    /// <summary>Before the bell: the boxers shadowbox in their corners while the ring card goes round.</summary>
    private void Waiting(float now)
    {
        for (var side = 0; side < 2; side++)
        {
            var dir = Dir(side);
            ref var b = ref _boxers[side];
            b = Fresh(side);
            b.X = Corners[side] + dir * 6 + MathF.Sin(now * 1.3f + side) * 6;
            b.Y = BoxerY - MathF.Abs(MathF.Sin(now * 6 + side)) * 2;

            // a jab at the air every so often
            var phase = (now + side * 0.7f) % 1.6f;
            if (phase < 0.36f)
            {
                var reach = MathF.Sin(phase / 0.36f * MathF.PI);
                b.Punch = CMUSports.BoxingJab;
                b.Lead = true;
                b.Shoulder = Offset(b, dir * 3, -7);
                b.Glove = Offset(b, dir * (11 + reach * 13), -9 - reach * 2);
            }
        }

        var lap = now % 14;
        if (lap < 5)
            Walk(lap / 5, 1);

        _spot = Middle;
    }

    /// <summary>The first four seconds: out of the corners, touch gloves, back to fighting distance.</summary>
    private void Introductions(float t)
    {
        FightBase(4, _next);
        for (var side = 0; side < 2; side++)
        {
            var dir = Dir(side);
            ref var b = ref _boxers[side];
            b = Fresh(side);
            var touch = Middle - dir * 17;
            b.X = t < 1.4f
                ? Lerp(Corners[side], touch, Ease((t - 0.2f) / 1.2f))
                : Lerp(touch, _next[side].X, Ease((t - 2.1f) / 1.3f));
            b.Y = BoxerY - MathF.Abs(MathF.Sin(t * 7 + side)) * 2;
            if (t > 1.3f && t < 2.2f)
            {
                var reach = MathF.Sin(Math.Clamp((t - 1.3f) / 0.9f, 0, 1) * MathF.PI);
                b.Punch = CMUSports.BoxingJab;
                b.Lead = true;
                b.Shoulder = Offset(b, dir * 3, -7);
                b.Glove = Vector2.Lerp(Offset(b, dir * 11, -9), new Vector2(Middle - dir * 3, BoxerY - 9), reach);
            }
        }

        if (t > 1.1f && t < 2.4f)
            _refArms = RefArms.Break;
    }

    /// <summary>A round in progress: the exchanges, with clinches and knockdowns laid over them.</summary>
    private void Fight(float t)
    {
        FightBase(t, _boxers);
        ref var red = ref _boxers[0];
        ref var blue = ref _boxers[1];
        _ref.X = RefSpot(_boxers, t);
        _ref.Y = RefY - MathF.Abs(MathF.Sin(t * 3));

        foreach (var clinch in _clinches)
        {
            var length = CMUSports.BoxingClinchLength + 0.4f;
            var u = t - clinch;
            if (u < 0 || u > length)
                continue;

            var weight = Ease(u / 0.3f) * Ease((length - u) / 0.4f);
            var middle = (red.X + blue.X) / 2;
            var wobble = MathF.Sin(t * 9) * 3 * weight;
            red.X = Lerp(red.X, middle - 12, weight);
            blue.X = Lerp(blue.X, middle + 12, weight);
            red.Rot += -6 * weight + wobble;
            blue.Rot += 6 * weight + wobble;
            red.Arms = blue.Arms = weight < 0.5f;
            red.Punch = blue.Punch = -1;

            // the referee steps in and pulls them apart
            var step = Ease((u - 0.8f) / 0.5f) * Ease((length - u) / 0.6f);
            _ref.X = Lerp(_ref.X, middle, step);
            if (u > 1.1f && u < CMUSports.BoxingClinchLength + 0.2f)
                _refArms = RefArms.Break;
        }

        foreach (var down in _downs)
        {
            var length = CMUSports.BoxingDownLength(down.Count);
            var a = t - down.Time;
            if (down.Out || a < 0 || a > length)
                continue;

            FightBase(down.Time, _then);
            var fallen = down.Side;
            var puncher = 1 - fallen;
            var away = Dir(puncher);
            var leaving = Ease((length - a) / 0.8f);

            var live = _boxers[fallen];
            ref var b = ref _boxers[fallen];
            b = _then[fallen];
            DownPose(ref b, _then[fallen], a, away, down.Count);
            b.X = Lerp(live.X, b.X, leaving);
            b.Y = Lerp(live.Y, b.Y, leaving);
            b.Rot = Lerp(live.Rot, b.Rot, leaving);

            // the one who dropped them goes to a neutral corner
            ref var p = ref _boxers[puncher];
            var corner = Math.Clamp(_then[puncher].X - away * 46, 100, 420);
            p.X = Lerp(p.X, corner, Ease(a / 0.8f) * leaving);
            p.Punch = -1;

            _ref.X = Lerp(_ref.X, _then[fallen].X - away * 26, Ease(a / 0.6f) * leaving);
            _ref.Facing = away > 0 ? Direction.East : Direction.West;
            _refPointing = away;
            var counting = a - CMUSports.BoxingCountDelay;
            if (counting >= 0 && counting < down.Count * CMUSports.BoxingCountStep)
            {
                _refArms = RefArms.Count;
                _refPhase = counting / CMUSports.BoxingCountStep;
            }
        }

        _spot = (red.X + blue.X) / 2;
    }

    /// <summary>A boxer on the canvas from a knockdown <paramref name="a"/> seconds ago, getting up after the count if it is not a knockout.</summary>
    private static void DownPose(ref Boxer b, Boxer from, float a, int away, int count)
    {
        var fall = Math.Clamp(a / 0.45f, 0, 1);
        var lying = fall * fall;
        var bounce = a > 0.45f && a < 0.8f ? MathF.Sin((a - 0.45f) / 0.35f * MathF.PI) * 5 : 0;
        var rise = 0f;
        if (count >= 0)
        {
            var up = CMUSports.BoxingCountDelay + (count - 1) * CMUSports.BoxingCountStep + 0.15f;
            rise = Ease((a - up) / 0.7f);
            lying *= 1 - rise;
        }

        b.X = from.X + away * 12 * Ease(a / 0.4f);
        b.Y = Lerp(from.Y, LyingY, lying) - bounce * lying;
        b.Rot = Lerp(from.Rot, -away * 90, lying);
        if (rise > 0)
            b.Rot += MathF.Sin(a * 9) * 4 * rise * Math.Clamp(3 - (a - CMUSports.BoxingCountDelay - count * CMUSports.BoxingCountStep), 0, 1);

        b.Down = lying > 0.5f;
        b.Arms = lying < 0.2f && rise >= 1;
        b.Guard = false;
        b.Punch = -1;
    }

    /// <summary>The bell between rounds: corners, water and the sponge, the ring card going round.</summary>
    private void Rest(float t, int round, float start, float a)
    {
        _resting = true;
        FightBase(start + CMUSports.RoundLength, _then);
        FightBase(start + CMUSports.RoundLength + CMUSports.RoundRest, _next);
        for (var side = 0; side < 2; side++)
        {
            var dir = Dir(side);
            var corner = Corners[side];
            ref var b = ref _boxers[side];
            b = Fresh(side);
            b.Arms = a > 5.3f;
            var walking = false;
            if (a < 1.3f)
            {
                b.X = Lerp(_then[side].X, corner, Ease(a / 1.3f));
                walking = a < 1.2f;
                if (walking)
                    b.Facing = side == 0 ? Direction.West : Direction.East;
            }
            else if (a > 4.8f)
            {
                b.X = Lerp(corner, _next[side].X, Ease((a - 4.8f) / 1.2f));
                walking = true;
            }
            else
            {
                b.X = corner;
            }

            b.Y = BoxerY - (walking ? MathF.Abs(MathF.Sin(t * 9 + side)) * 2 : MathF.Sin(t * 2 + side) * 0.6f);

            // the corner man ducks through the ropes with the sponge, then gets out before the bell
            var enter = Ease((a - 0.5f) / 0.9f);
            var leave = Ease((a - 4.2f) / 0.9f);
            if (a > 0.5f && a < 5.1f)
            {
                ref var man = ref _corner[side];
                man.Visible = true;
                man.X = Lerp(corner - dir * 44, corner + dir * 22, enter * (1 - leave));
                man.Y = CornerY - (enter < 1 || leave > 0 ? MathF.Abs(MathF.Sin(t * 9)) * 2 : 0);

                // walking in faces the ring; working, or walking back out, faces the corner
                man.Facing = enter < 1 == (side == 0) ? Direction.East : Direction.West;

                if (a > 1.6f && a < 4.0f)
                {
                    var dab = MathF.Sin(((a - 1.6f) % 0.8f) / 0.8f * MathF.PI);
                    var hand = new Vector2(man.X - dir * 8, man.Y - 6);
                    _sponge[side] = Vector2.Lerp(hand, Offset(b, dir * 6, -17), dab);
                }
            }
        }

        _ref.X = Lerp(RefSpot(_then, start + CMUSports.RoundLength), Middle, Ease(a / 1.5f));
        Walk((a - 0.6f) / 4.4f, round + 1);
        _spot = Middle;
    }

    /// <summary>After the last bell: both to the middle, the cards are read, the referee lifts the winner's hand.</summary>
    private void Decision(float t, float start)
    {
        var a = t - start - CMUSports.RoundLength;
        FightBase(start + CMUSports.RoundLength, _then);
        var winner = _match?.Winner ?? 0;
        var arrive = Ease(a / 1.4f);
        for (var side = 0; side < 2; side++)
        {
            ref var b = ref _boxers[side];
            b = Fresh(side);
            b.X = Lerp(_then[side].X, Middle - Dir(side) * 30, arrive);
            b.Y = BoxerY - (arrive < 1 ? MathF.Abs(MathF.Sin(t * 9 + side)) * 2 : 0);
            b.Arms = a < 0.8f;
            if (arrive >= 1)
                b.Facing = Direction.South;

            if (a > 3 && side == winner)
                b.HandUp = true;
        }

        _ref.X = Lerp(RefSpot(_then, start + CMUSports.RoundLength), Middle, arrive);
        if (a > 3)
        {
            _refArms = RefArms.Raise;
            _refTarget = RaisedGlove(_boxers[winner], winner);
        }

        _spot = Middle;
    }

    /// <summary>A knockout and everything after it: the fall, the full count, the winner going wild.</summary>
    private void KnockedOut(float t, Down down)
    {
        var a = t - down.Time;
        FightBase(down.Time, _then);
        var loser = down.Side;
        var winner = 1 - loser;
        var away = Dir(winner);

        ref var l = ref _boxers[loser];
        l = _then[loser];
        DownPose(ref l, _then[loser], a, away, -1);

        ref var w = ref _boxers[winner];
        w = _then[winner];
        w.Punch = -1;
        w.Hurt = 0;
        var retreat = Math.Clamp(_then[winner].X - away * 50, 100, 420);
        w.X = Lerp(_then[winner].X, retreat, Ease(a / 0.9f));
        w.Rot = Lerp(w.Rot, 0, Ease(a / 0.4f));
        w.Y = BoxerY - MathF.Abs(MathF.Sin(t * 6.5f)) * 2;
        if (a > Counted)
        {
            w.Facing = Direction.South;
            w.Arms = false;
            if (a > Counted + 2.4f)
            {
                w.HandUp = true;
                w.Y = BoxerY;
            }
            else
            {
                w.Raised = true;
                w.Y = BoxerY - MathF.Abs(MathF.Sin((a - Counted) * 7)) * 7;
            }
        }

        // the referee counts over the body, waves it off, then finds the winner
        var over = _then[loser].X - away * 26;
        _ref.Facing = away > 0 ? Direction.East : Direction.West;
        _refPointing = away;
        if (a < Counted + 1.4f)
        {
            _ref.X = Lerp(RefSpot(_then, down.Time), over, Ease(a / 0.6f));
            var counting = a - CMUSports.BoxingCountDelay;
            if (counting >= 0 && a < Counted - 0.3f)
            {
                _refArms = RefArms.Count;
                _refPhase = counting / CMUSports.BoxingCountStep;
            }
            else if (a >= Counted - 0.3f)
            {
                _refArms = RefArms.Wave;
                _ref.Facing = Direction.South;
            }
        }
        else
        {
            var beside = w.X + away * 22;
            _ref.X = Lerp(over, beside, Ease((a - Counted - 1.4f) / 1.0f));
            _ref.Facing = Direction.South;
            if (w.HandUp)
            {
                _refArms = RefArms.Raise;
                _refTarget = RaisedGlove(w, winner);
            }
        }

        // the corners rush in: one to the fallen boxer, one to celebrate
        if (a > Counted + 0.6f)
        {
            var rush = Ease((a - Counted - 0.6f) / 1.2f);
            for (var side = 0; side < 2; side++)
            {
                ref var man = ref _corner[side];
                var target = side == loser ? down.Pool.X - Dir(side) * 4 : w.X - Dir(side) * 26;
                var from = Corners[side] - Dir(side) * 44;
                man.Visible = true;
                man.X = Lerp(from, target, rush);
                man.Y = CornerY - (rush < 1 ? MathF.Abs(MathF.Sin(t * 9 + side)) * 2 : 0);
                man.Facing = side == winner && rush >= 1 ? Direction.South
                    : (rush < 1 ? target : l.X) > man.X ? Direction.East : Direction.West;
            }
        }

        _spot = Lerp((_then[0].X + _then[1].X) / 2, _then[loser].X, Ease(a / 1.5f));
    }

    /// <summary>
    /// The two boxers trading at <paramref name="t"/>: their footwork around the ring, pushed back by what lands,
    /// the punch each is throwing and how the other takes it. Pure in the time, so any moment can be looked up.
    /// </summary>
    private void FightBase(float t, Boxer[] into)
    {
        var centre = Math.Clamp(Middle + MathF.Sin(t * 0.37f) * 40 + MathF.Sin(t * 0.83f + 1) * 14 + Pressure(t), 128f, 392f);
        var gap = 54 + MathF.Sin(t * 1.9f) * 4 + MathF.Sin(t * 0.53f) * 5;
        for (var side = 0; side < 2; side++)
        {
            ref var b = ref into[side];
            b = Fresh(side);
            b.X = centre - Dir(side) * gap / 2 + MathF.Sin(t * 2.7f + side * 2) * 1.5f;
            b.Y = BoxerY - MathF.Abs(MathF.Sin(t * 6.5f + side * 1.7f)) * 2;
        }

        if (_play == null)
            return;

        // how each takes the other's punches first, so the gloves aim at where the head went
        var swing0 = -1;
        var swing1 = -1;
        var events = _play.Events;
        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.Time > t + 0.3f)
                break;

            if (ev.Action != CMUSportsAction.Punch || t > ev.Time + 0.7f)
                continue;

            var a = t - ev.Time;
            if (a >= -Windup(ev.Extra) && a <= Recover(ev.Extra))
            {
                if (ev.Side == 0)
                    swing0 = i;
                else
                    swing1 = i;
            }

            React(ref into[1 - ev.Side], ev, a);
        }

        if (swing0 >= 0)
            Swing(into, 0, events[swing0], t - events[swing0].Time);

        if (swing1 >= 0)
            Swing(into, 1, events[swing1], t - events[swing1].Time);
    }

    /// <summary>Landed punches push the ring's centre towards the one taking them, so a beating ends on the ropes.</summary>
    private float Pressure(float t)
    {
        if (_play == null)
            return 0;

        var shift = 0f;
        foreach (var ev in _play.Events)
        {
            if (ev.Time > t)
                break;

            if (ev.Action != CMUSportsAction.Punch || ev.Value != CMUSports.BoxingLanded || t - ev.Time > 10)
                continue;

            shift += Dir(ev.Side) * (ev.Extra == CMUSports.BoxingJab ? 5 : 9) * MathF.Exp(-(t - ev.Time) / 2.5f);
        }

        return Math.Clamp(shift, -70, 70);
    }

    /// <summary>How a boxer takes a punch thrown <paramref name="a"/> seconds ago (negative while it is coming).</summary>
    private static void React(ref Boxer d, CMUSportsEvent ev, float a)
    {
        var dir = Dir(ev.Side);
        var kind = ev.Extra;
        switch (ev.Value)
        {
            case CMUSports.BoxingLanded when a >= 0 && a < 0.7f:
            {
                var k = a < 0.05f ? a / 0.05f : MathF.Exp(-(a - 0.05f) * 6);
                if (ev.State == CMUSports.BoxingBody)
                {
                    // doubled over the glove
                    d.X += dir * 2 * k;
                    d.Y += 3 * k;
                    d.Rot += dir * 10 * k;
                }
                else
                {
                    var power = Power(kind);
                    d.X += dir * (3 + power * 2.5f) * k;
                    d.Rot -= dir * (4 + power * 3.5f) * k;
                    if (kind == CMUSports.BoxingUppercut)
                        d.Y -= 5 * k;
                }

                d.Hurt = Math.Max(d.Hurt, 1 - a / 0.15f);
                break;
            }
            case CMUSports.BoxingBlocked:
                if (a > -0.18f && a < 0.22f)
                    d.Guard = true;

                if (a >= 0 && a < 0.4f)
                    d.X += dir * 2 * MathF.Exp(-a * 8);

                break;
            case CMUSports.BoxingSlipped when MathF.Abs(a) < 0.22f:
            {
                var v = 1 - MathF.Abs(a) / 0.22f;
                if (kind == CMUSports.BoxingHook)
                {
                    d.Y += 6 * v;
                    d.Rot += dir * 3 * v;
                }
                else
                {
                    d.Rot -= dir * (kind == CMUSports.BoxingUppercut ? 9 : 7) * v;
                    d.X += dir * (kind == CMUSports.BoxingUppercut ? 4 : 3) * v;
                }

                break;
            }
        }
    }

    /// <summary>
    /// A boxer throwing a punch <paramref name="a"/> seconds from its impact: the wind-up pulls back, the strike
    /// lunges in with the glove on its path (straight, round the side or up from below), then the recoil.
    /// </summary>
    private static void Swing(Boxer[] boxers, int side, CMUSportsEvent ev, float a)
    {
        ref var b = ref boxers[side];
        var d = boxers[1 - side];
        var dir = Dir(side);
        var kind = ev.Extra;
        float cock;
        float strike;
        if (a < 0)
        {
            var u = 1 + a / Windup(kind);
            if (u < 0.45f)
            {
                cock = MathF.Sin(u / 0.45f * MathF.PI / 2);
                strike = 0;
            }
            else
            {
                var s = (u - 0.45f) / 0.55f;
                cock = 1 - s;
                strike = s * s;
            }
        }
        else
        {
            var r = Math.Min(1, a / Recover(kind));
            cock = 0;
            strike = 1 - Ease(r);

            // a miss carries on past where the head was
            if (ev.Value == CMUSports.BoxingSlipped)
                strike *= 1 + 0.25f * MathF.Sin(r * MathF.PI);
        }

        var (lunge, lean) = kind switch
        {
            CMUSports.BoxingJab => (5f, 3f),
            CMUSports.BoxingCross => (9f, 8f),
            CMUSports.BoxingHook => (7f, 7f),
            _ => (6f, -5f),
        };

        b.X += dir * (lunge * strike - 3 * cock);
        b.Rot -= dir * (lean * strike - 2 * cock);
        if (kind == CMUSports.BoxingUppercut)
            b.Y += 4 * cock - 3 * strike;

        var lead = kind is CMUSports.BoxingJab or CMUSports.BoxingHook;
        var guard = Offset(b, dir * (lead ? 11 : 6), lead ? -9 : -6);
        Vector2 target;
        if (ev.Value == CMUSports.BoxingBlocked)
            target = Offset(d, -dir * 11, -9);
        else if (ev.State == CMUSports.BoxingBody)
            target = Offset(d, -dir * 7, 2);
        else
            target = Offset(d, -dir * 8, -17);

        if (ev.Value == CMUSports.BoxingSlipped)
            target += new Vector2(dir * 9, kind == CMUSports.BoxingHook ? -3 : 0);

        var cocked = guard + kind switch
        {
            CMUSports.BoxingHook => new Vector2(-dir * 2, -9),
            CMUSports.BoxingUppercut => new Vector2(dir, 10),
            _ => new Vector2(-dir * 4, 1),
        };

        Vector2 glove;
        if (a < 0 && strike <= 0)
        {
            glove = Vector2.Lerp(guard, cocked, cock);
        }
        else if (a < 0)
        {
            // hooks loop over the top, uppercuts come up from under the guard
            var control = kind switch
            {
                CMUSports.BoxingHook => (cocked + target) / 2 + new Vector2(0, -12),
                CMUSports.BoxingUppercut => new Vector2(target.X - dir * 3, cocked.Y + 4),
                _ => (cocked + target) / 2,
            };
            var s = strike;
            glove = (1 - s) * (1 - s) * cocked + 2 * (1 - s) * s * control + s * s * target;
        }
        else
        {
            glove = Vector2.Lerp(guard, target, strike);
        }

        b.Punch = kind;
        b.Lead = lead;
        b.Glove = glove;
        b.Shoulder = Offset(b, dir * 3, -7);
    }

    /// <summary>The ring card walker crossing at <paramref name="progress"/> 0 to 1, holding up a round.</summary>
    private void Walk(float progress, int round)
    {
        if (progress <= 0 || progress >= 1 || round > CMUSports.BoxingRounds)
            return;

        _walker.Visible = true;
        _walker.X = Lerp(30, ArenaWidth - 30, progress);
        _walker.Y = WalkerY - MathF.Abs(MathF.Sin(progress * 40)) * 1.5f;
        _walker.Facing = Direction.East;
        _sign = round;
    }

    private void PoseDolls()
    {
        for (var side = 0; side < 2; side++)
        {
            var b = _boxers[side];
            var doll = BoxerDoll + side;
            if (!b.Visible)
            {
                Hide(doll);
                continue;
            }

            Pose(doll, b.X, b.Y, b.Facing, b.Rot);
            DollView(doll).Modulate = b.Hurt > 0
                ? Color.InterpolateBetween(Color.White, Color.FromHex("#FF8C8C"), Math.Clamp(b.Hurt, 0, 1))
                : Color.White;

            PoseFigure(CornerDoll + side, _corner[side]);
        }

        PoseFigure(RefDoll, _ref);
        PoseFigure(WalkerDoll, _walker);
    }

    private void PoseFigure(int doll, Figure figure)
    {
        if (figure.Visible)
            Pose(doll, figure.X, figure.Y, figure.Facing);
        else
            Hide(doll);
    }

    #endregion

    #region Calls and moments

    /// <summary>The broadcast graphics: names, punches landed, the clock, the count, the cards.</summary>
    private void Scoreboard(CMUBetMatch match, float t)
    {
        Say(match.Name(0), 12, Bar + 4, Color.White);
        Say(match.Name(1), ArenaWidth - 12, Bar + 4, Color.White, right: true);
        _landed[0] = _landed[1] = 0;
        _cutLevel[0] = _cutLevel[1] = 0;
        _hype = 0;
        if (_play == null || t < 0)
        {
            Say(Loc.GetString("cmu-game-boxing-red"), 12, Bar + 15, Plates[0]);
            Say(Loc.GetString("cmu-game-boxing-blue"), ArenaWidth - 12, Bar + 15, Plates[1], right: true);
            _hype = 0.1f;
            return;
        }

        foreach (var ev in _play.Events)
        {
            if (ev.Time > t)
                break;

            var weight = 0f;
            switch (ev.Action)
            {
                case CMUSportsAction.Punch when ev.Value == CMUSports.BoxingLanded:
                    _landed[ev.Side]++;
                    weight = ev.Extra switch { CMUSports.BoxingJab => 0.05f, CMUSports.BoxingCross => 0.12f, _ => 0.3f };
                    break;
                case CMUSportsAction.Injury:
                    _cutLevel[ev.Side] = ev.Extra;
                    weight = 0.3f;
                    break;
                case CMUSportsAction.Knockdown:
                    weight = 1.4f;
                    break;
                case CMUSportsAction.Knockout:
                    // the place stays on its feet for the rest of the night
                    _hype = Math.Max(_hype, 1.3f);
                    weight = 1.5f;
                    break;
                case CMUSportsAction.Bell:
                    weight = 0.25f;
                    break;
                case CMUSportsAction.Special:
                    weight = ev.Value == CMUSports.BoxingTooth ? 0.6f : ev.Value == CMUSports.BoxingCard ? 0.4f : 0.1f;
                    break;
            }

            if (weight > 0 && t - ev.Time < 12)
                _hype += weight * MathF.Exp(-(t - ev.Time) / 2.5f);
        }

        _hype = Math.Min(_hype, 1.6f);
        if (t >= CMUSports.Length(CMUSport.Boxing) - 6 && _ko < 0)
            _hype = Math.Max(_hype, 0.9f);

        for (var side = 0; side < 2; side++)
        {
            var landed = Loc.GetString("cmu-sport-landed", ("count", _landed[side]));
            if (side == 0)
                Say(landed, 12, Bar + 15, Plates[0]);
            else
                Say(landed, ArenaWidth - 12, Bar + 15, Plates[1], right: true);
        }

        if (t < 4)
        {
            Say(_signs[1], Middle, 92, Color.White, true);
            return;
        }

        var round = RoundOf(t);
        var start = RoundStart(round);
        var into = t - start;
        var shown = Math.Min(into, CMUSports.RoundLength);
        if (_ko >= 0 && t >= _downs[_ko].Time)
            shown = _downs[_ko].Time - RoundStart(RoundOf(_downs[_ko].Time));

        if (t < CMUSports.Length(CMUSport.Boxing))
            Clock = Loc.GetString("cmu-sport-round-clock", ("round", round), ("clock", $"{(int) shown / 60}:{(int) shown % 60:00}"));

        // the referee's count, big over the one on the canvas
        foreach (var down in _downs)
        {
            var a = t - down.Time;
            var counting = a - CMUSports.BoxingCountDelay;
            if (counting < 0)
                continue;

            var count = (int) (counting / CMUSports.BoxingCountStep) + 1;
            if (down.Out)
            {
                if (a >= Counted)
                    Say(Loc.GetString("cmu-sport-ko"), Middle, 92, Color.FromHex("#FF2A2A"), true);
                else
                    Say(Numbers[Math.Min(count, 10) - 1], down.Pool.X, 92, Color.White, true);

                continue;
            }

            if (count <= down.Count)
                Say(Numbers[count - 1], down.Pool.X, 92, Color.White, true);
            else if (a < CMUSports.BoxingDownLength(down.Count))
                Say(Loc.GetString("cmu-game-boxing-up"), down.Pool.X, 92, Color.FromHex("#FFE14D"), true);
        }

        foreach (var clinch in _clinches)
        {
            var u = t - clinch;
            if (u > 1.1f && u < CMUSports.BoxingClinchLength + 0.2f)
                Say(Loc.GetString("cmu-game-boxing-break"), _ref.X, 98, Color.White, true);
        }

        if (into >= CMUSports.RoundLength && (_ko < 0 || t < _downs[_ko].Time))
        {
            var rest = into - CMUSports.RoundLength;
            if (round == CMUSports.BoxingRounds)
            {
                if (rest > 1.5f)
                    Say(Loc.GetString("cmu-game-boxing-decision"), Middle, 116, Color.Gold, true);
            }
            else if (rest > 4.5f)
            {
                Say(_signs[round + 1], Middle, 92, Color.White, true);
            }
        }
    }

    /// <summary>Sounds and bursts for moments between events: the count, a tooth landing, hands raised, the sponge.</summary>
    private void Moments(float t)
    {
        if (_play == null || t < 0)
            return;

        if (Crossed(1.7f))
            Sound(Punches[0], -10, 1.2f);

        foreach (var down in _downs)
        {
            var counts = down.Out ? 10 : down.Count;
            for (var count = 0; count < counts; count++)
            {
                if (!Crossed(down.Time + CMUSports.BoxingCountDelay + count * CMUSports.BoxingCountStep))
                    continue;

                // the crowd claps along with the count
                Sound(Clap, -2 + count * 0.4f);
                if (down.Out && count == 9)
                {
                    Sound(FinalBell, 2);
                    Roar(0);
                }
            }

            if (!down.Out && Crossed(down.Time + CMUSports.BoxingCountDelay + (down.Count - 1) * CMUSports.BoxingCountStep + 0.85f))
            {
                SoundAny(Claps, -4);
                Flashes(3);
            }

            if (down.Out && Crossed(down.Time + Counted + 2.4f))
            {
                Roar(0);
                Confetti();
            }
        }

        foreach (var tooth in _teeth)
        {
            if (Crossed(tooth.Time + ToothFlight))
                SoundAny(Tinks, -6);
        }

        var decided = RoundStart(CMUSports.BoxingRounds) + CMUSports.RoundLength + 3;
        if (_ko < 0 && Crossed(decided))
        {
            Sound(FinalBell, 0);
            Roar(1);
            Confetti();
        }

        if (!_resting)
            return;

        // the sponge squeezed over each face, halfway through every dab
        var bell = RoundStart(RoundOf(t)) + CMUSports.RoundLength;
        for (var dab = 0; dab < 3; dab++)
        {
            if (!Crossed(bell + 2 + dab * 0.8f))
                continue;

            for (var side = 0; side < 2; side++)
            {
                var face = Offset(_boxers[side], Dir(side) * 6, -17);
                Burst(face, 7, Water, 60, 0.5f, 1.4f, -MathF.PI / 2, MathF.PI, gravity: 300);
            }

            SoundAny(Splashes, -12);
        }
    }

    /// <summary>The crowd's noise and lights, and cuts dripping while the fight goes on.</summary>
    private void Ambience(float t, float delta)
    {
        var flashes = 0.3f + _hype * 3.5f;
        if (FxFloat() < flashes * delta)
            Flashes(1, FxFloat() < 0.15f);

        if (FxFloat() < (0.08f + _hype * 0.5f) * delta)
            SoundAny(Claps, -16 + _hype * 6);

        if (_play == null || t < 4 || _resting)
            return;

        for (var side = 0; side < 2; side++)
        {
            var b = _boxers[side];
            var level = _cutLevel[side];
            if (level == 0 || !b.Visible || b.Down || FxFloat() >= level * 0.4f * delta)
                continue;

            var cut = Offset(b, Dir(side) * 5, -19);
            Burst(cut, 1, Blood, 14, 1.4f, 1.7f, MathF.PI / 2, 0.8f, gravity: 320, floor: Feet - 3 + FxFloat() * 8, stains: true);
        }
    }

    protected override void OnEvent(CMUBetMatch match, CMUSportsEvent ev, int index, bool live)
    {
        if (_play == null)
            return;

        switch (ev.Action)
        {
            case CMUSportsAction.Bell:
                Sound(OpeningBell);
                SoundAny(Claps, -8);
                Pop(Loc.GetString(ev.Value == 1 ? "cmu-game-boxing-fight" : "cmu-sport-round", ("round", ev.Value)),
                    new Vector2(Middle, 96), Color.White, true, 1.2f, 12);
                Flashes(4);
                break;
            case CMUSportsAction.Punch:
                Punched(ev, index, live);
                break;
            case CMUSportsAction.Knockdown:
            case CMUSportsAction.Knockout:
                Dropped(match, ev, index);
                break;
            case CMUSportsAction.Injury:
                Cut(match, ev);
                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.BoxingTooth:
                ToothOut(match, ev, index);
                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.BoxingClinch:
                Sound(Scuffles[index % Scuffles.Length], -3);
                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.BoxingCard:
                Sound(ClosingBell);
                SoundAny(Claps, -5);
                Flashes(3);
                break;
        }
    }

    private void Punched(CMUSportsEvent ev, int index, bool live)
    {
        FightBase(ev.Time, _then);
        var hurt = 1 - ev.Side;
        var d = _then[hurt];
        var dir = Dir(ev.Side);
        var kind = ev.Extra;
        var power = kind switch { CMUSports.BoxingJab => 1, CMUSports.BoxingCross => 2, _ => 3 };
        // jabs and glove taps are the gloved slaps, the heavier shots the meaty punches
        var punch = Punches[index % Punches.Length];
        var heavy = Heavy[index % Heavy.Length];
        switch (ev.Value)
        {
            case CMUSports.BoxingLanded when ev.State == CMUSports.BoxingBody:
            {
                var body = Offset(d, -dir * 7, 2);
                Sound(heavy, -1, 0.85f);
                Sound(Thump, -3 + power);
                Burst(body, 5 + power * 2, Sweat, 90, 0.4f, 1.3f, dir > 0 ? 0 : MathF.PI, 1.4f, gravity: 350);
                if (power >= 3)
                {
                    // the wind knocked out of them, and a little spit with it
                    Burst(Offset(d, -dir * 7, -15), 4, Color.White.WithAlpha(0.9f), 70, 0.5f, 1.4f, dir > 0 ? -0.3f : MathF.PI + 0.3f, 0.8f, gravity: 300);
                    Pop(Loc.GetString("cmu-game-boxing-body"), Offset(d, 0, -34), Color.FromHex("#FFD24D"));
                    Shake(1.2f, 0.18f);
                }

                break;
            }
            case CMUSports.BoxingLanded:
            {
                var face = Offset(d, -dir * 8, -17);
                var cut = CutAt(hurt, ev.Time);
                if (power >= 2 || cut > 0)
                    Splat(new Vector2(face.X + dir * (4 + FxFloat() * 12), Feet - 5 + FxFloat() * 10), 3 + power * 1.5f + cut * 2);

                if (!live)
                    return;

                if (power == 1)
                    Sound(punch, -2, 1.05f);
                else
                    Sound(heavy, power - 3, 1.06f - power * 0.04f);

                if (power >= 3)
                    SoundAny(Flesh, -7);

                if (Flurry(index))
                    Cheer();

                if (kind == CMUSports.BoxingUppercut)
                    Sound(Crunch, -6);

                var angle = kind == CMUSports.BoxingUppercut ? -MathF.PI / 2 + dir * 0.5f : dir > 0 ? -0.45f : MathF.PI + 0.45f;
                Burst(face, power + cut, Blood, 90 + power * 25, 1.3f, 2 + power * 0.3f, angle, 1.0f, gravity: 420,
                    floor: Feet - 4 + FxFloat() * 10, stains: true);
                Burst(face, 5 + power * 4 + cut * 3, Blood.WithAlpha(0.8f), 140, 0.3f, 1.3f, angle, 1.4f, drag: 5);
                Burst(face, 6 + power * 2, Sweat, 110, 0.45f, 1.3f, angle, 1.6f, gravity: 350);
                if (power >= 3)
                {
                    Shake(2.4f, 0.25f);
                    Pop(Loc.GetString(kind == CMUSports.BoxingHook ? "cmu-game-boxing-hook" : "cmu-game-boxing-uppercut"),
                        Offset(d, 0, -36), Color.FromHex("#FFE14D"));
                    Flashes(2 + FxRandom.Next(3));
                }
                else if (power == 2)
                {
                    Shake(1.1f, 0.15f);
                }

                break;
            }
            case CMUSports.BoxingBlocked:
                Sound(punch, -9, 1.35f);
                Burst(Offset(d, -dir * 11, -9), 4, Sweat, 60, 0.3f, 1.2f, dir > 0 ? 0 : MathF.PI, 2.4f, gravity: 250);
                break;
            default:
                SoundAny(Swooshes, -4, 0.1f);
                break;
        }
    }

    private void Dropped(CMUBetMatch match, CMUSportsEvent ev, int index)
    {
        var down = _downs.Find(d => d.Index == index);
        var knockout = ev.Action == CMUSportsAction.Knockout;
        var away = Dir(1 - ev.Side);

        // where the face hits the canvas, and the spray on the way down
        Splat(down.Pool, knockout ? 16 : 11);
        Splat(down.Pool + new Vector2(away * 9, 2), 6);
        Splat(down.Face with { Y = Feet - 2 } + new Vector2(away * 4, 0), 5);
        if (!Live)
            return;

        Sound(Fall, knockout ? 2 : 0);
        Sound(Thud, knockout ? -4 : -7, 0.9f);
        Sound(Gasps[index % Gasps.Length], -3);
        Sound(Gasps[(index + 2) % Gasps.Length], -5, 0.9f);
        SoundAny(Claps, -4);
        Bleed(down.Face, Feet - 2, knockout ? 3 : 2, away > 0 ? -0.5f : MathF.PI + 0.5f, 1.2f);
        Burst(down.Pool with { Y = Feet - 2 }, 14, Color.FromHex("#E8E4DCAA"), 60, 0.6f, 2.5f, -MathF.PI / 2, MathF.PI, gravity: -20, drag: 3);
        Shake(knockout ? 8 : 5, knockout ? 0.7f : 0.5f);
        Flash(Color.White.WithAlpha(knockout ? 0.45f : 0.3f), knockout ? 0.4f : 0.25f);
        Flashes(knockout ? 10 : 6, true);
        if (knockout)
        {
            Scream(Female(match, ev.Side));
            SoundAny(Whistles, -6);
        }
        else
        {
            Cheer();
            Pop(Loc.GetString("cmu-game-boxing-down"), new Vector2(down.Pool.X, 112), Color.FromHex("#FF4040"), true, 1.2f, 10);
        }
    }

    private void Cut(CMUBetMatch match, CMUSportsEvent ev)
    {
        FightBase(ev.Time, _then);
        var face = Offset(_then[ev.Side], Dir(ev.Side) * 5, -19);
        var away = Dir(1 - ev.Side);
        Splat(new Vector2(face.X + away * FxFloat() * 10, Feet - 4 + FxFloat() * 8), 5 + ev.Extra * 3);
        if (!Live)
            return;

        Burst(face, 3 + ev.Extra * 3, Blood, 70 + ev.Extra * 20, 1.2f, 2.2f, away > 0 ? -0.6f : MathF.PI + 0.6f, 1.2f,
            gravity: 420, floor: Feet - 4 + FxFloat() * 8, stains: true);
        SoundAny(Bleeding, -4);
        Pop(Loc.GetString("cmu-game-boxing-cut", ("level", ev.Extra)), Offset(_then[ev.Side], 0, -44), Color.FromHex("#FF3B3B"));
        if (ev.Extra >= 3)
            Scream(Female(match, ev.Side), -3);
    }

    private void ToothOut(CMUBetMatch match, CMUSportsEvent ev, int index)
    {
        var tooth = _teeth.Find(x => x.Index == index);
        Splat(tooth.To + new Vector2(0, 1), 4);
        if (!Live)
            return;

        Sound(Crunch, -2, 1.2f);
        Scream(Female(match, ev.Side), -2);
        Burst(tooth.From, 8, Blood, 100, 0.9f, 1.8f, Dir(1 - ev.Side) > 0 ? -0.7f : MathF.PI + 0.7f, 0.9f, gravity: 420,
            floor: Feet - 2, stains: true);
        Pop(Loc.GetString("cmu-game-boxing-tooth"), tooth.From - new Vector2(0, 26), Color.White, life: 1.1f);
    }

    /// <summary>The whole place on its feet: one of the big cheers, which runs its course rather than stacking.</summary>
    private void Roar(int cheer)
    {
        SoundAny(Whistles, -4);
        Flashes(6, true);
        var now = (float) Timing.RealTime.TotalSeconds;
        if (!Live || now < _cheering)
            return;

        Sound(Cheers[cheer % Cheers.Length], -1);
        _cheering = _cheeringSmall = now + 13;
    }

    /// <summary>A short cheer for a knockdown or a flurry, when no other cheer is running.</summary>
    private void Cheer()
    {
        var now = (float) Timing.RealTime.TotalSeconds;
        if (!Live || now < _cheeringSmall)
            return;

        Sound(SmallCheer, -5);
        _cheeringSmall = now + 3.5f;
    }

    /// <summary>Whether this landed punch is the third in a row from the same side inside a second and a half.</summary>
    private bool Flurry(int index)
    {
        if (_play == null)
            return false;

        var ev = _play.Events[index];
        var landed = 0;
        for (var i = index; i >= 0 && ev.Time - _play.Events[i].Time < 1.5f; i--)
        {
            var other = _play.Events[i];
            if (other.Action != CMUSportsAction.Punch || other.Value != CMUSports.BoxingLanded)
                continue;

            if (other.Side != ev.Side)
                return false;

            landed++;
        }

        return landed == 3;
    }

    private void Confetti()
    {
        for (var i = 0; i < 4; i++)
        {
            var colour = i switch { 0 => Gloves[0], 1 => Gloves[1], 2 => Color.Gold, _ => Color.White };
            Burst(new Vector2(Middle - 120 + i * 80, Bar + 6), 26, colour, 120, 2.6f, 2.2f, MathF.PI / 2, 2.4f, gravity: 50, drag: 0.8f);
        }
    }

    /// <summary>Camera flashes going off in the stands, staggered over the next moment.</summary>
    private void Flashes(int count, bool snap = false)
    {
        if (!Live)
            return;

        var now = (float) Timing.RealTime.TotalSeconds;
        for (var i = 0; i < count; i++)
        {
            ref var flash = ref _flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % _flashes.Length;
            flash.X = 6 + FxFloat() * (ArenaWidth - 12);
            flash.Y = RowY[FxRandom.Next(RowY.Length)] - 2 + FxFloat() * 6;
            flash.Born = now + (i == 0 ? 0 : FxFloat() * 0.5f);
            flash.Size = 0.6f + FxFloat();
        }

        if (snap)
            Sound(Camera, -12, 0.9f + FxFloat() * 0.2f);
    }

    /// <summary>Whether a moment passed between the last frame and this one, recently enough to make a noise for.</summary>
    private bool Crossed(float at)
    {
        return _lastT < at && at <= _t && _t - at < 0.5f;
    }

    #endregion

    #region Drawing

    protected override void DrawField(DrawingHandleScreen handle)
    {
        var now = (float) Timing.RealTime.TotalSeconds;
        Rect(handle, 0, Bar, ArenaWidth, ArenaHeight - Bar, Color.FromHex("#0C0A11"));
        for (var row = 0; row < RowY.Length; row++)
        {
            Rect(handle, 0, RowY[row] + 5, ArenaWidth, 9, Color.FromHex("#16131E").WithAlpha(0.4f + row * 0.1f));
        }

        // the stands: more of them on their feet and bouncing the louder it gets
        var hype = Math.Clamp(_hype, 0, 1.2f);
        if (_match is { } match && _crowdId != match.Id)
            BuildCrowd(match.Id);

        if (_crowd != null)
        {
            for (var seat = 0; seat < Seats.Length; seat++)
            {
                var fan = Seats[seat];
                var bob = MathF.Abs(MathF.Sin(now * (5 + fan.Phase) + fan.Phase * 7)) * (0.6f + hype * 3.2f);

                // the hyped throw their arms up; the ones in a corner's colours wave for their fighter
                var up = fan.Arms && hype >= 0.45f || _fanCorner[seat] >= 0 && hype >= 0.3f && MathF.Sin(now * 4 + fan.Phase * 9) > 0;
                var cell = seat * 2 + (up ? 1 : 0);
                var scale = FanScale[fan.Row];
                var source = UIBox2.FromDimensions(cell % FanColumns * 32, cell / FanColumns * 32, 32, 32);
                handle.DrawTextureRectRegion(_crowd, Box(fan.X - 16 * scale, RowY[fan.Row] - bob - 7 * scale, 32 * scale, 32 * scale), source);
            }
        }

        foreach (var flash in _flashes)
        {
            var age = now - flash.Born;
            if (age < 0 || age > 0.2f)
                continue;

            var alpha = 1 - age / 0.2f;
            var size = 2 + flash.Size * 2;
            Circle(handle, flash.X, flash.Y, size, Color.White.WithAlpha(alpha));
            Line(handle, new Vector2(flash.X - size * 2.5f, flash.Y), new Vector2(flash.X + size * 2.5f, flash.Y), Color.White.WithAlpha(alpha * 0.7f));
            Line(handle, new Vector2(flash.X, flash.Y - size * 2), new Vector2(flash.X, flash.Y + size * 2), Color.White.WithAlpha(alpha * 0.7f));
        }

        // the lights over the ring, through the haze
        for (var lamp = 0; lamp < 3; lamp++)
        {
            var x = Middle + (lamp - 1) * 130;
            Quad(handle, new Vector2(x - 6, Bar), new Vector2(x + 6, Bar), new Vector2(x + 90, CanvasFront),
                new Vector2(x - 90, CanvasFront), Color.FromHex("#FFF6DC").WithAlpha(0.035f));
            Rect(handle, x - 7, Bar, 14, 3, Color.FromHex("#FFF6DC").WithAlpha(0.8f));
        }

        // the back posts and ropes, behind the boxers
        Post(handle, BackLeft, CanvasBack, BackPost, 0, 6);
        Post(handle, BackRight, CanvasBack, BackPost, 1, 6);
        for (var rope = 0; rope < Ropes.Length; rope++)
        {
            var back = CanvasBack - BackPost * Ropes[rope];
            var front = CanvasFront - FrontPost * Ropes[rope];
            var colour = RopeColours[rope].WithAlpha(0.85f);
            Rect(handle, BackLeft, back - 1, BackRight - BackLeft, 2, colour);
            Line(handle, new Vector2(BackLeft, back), new Vector2(FrontLeft, front), colour);
            Line(handle, new Vector2(BackLeft, back + 1), new Vector2(FrontLeft, front + 1), colour);
            Line(handle, new Vector2(BackRight, back), new Vector2(FrontRight, front), colour);
            Line(handle, new Vector2(BackRight, back + 1), new Vector2(FrontRight, front + 1), colour);
        }

        // the canvas under its spotlight, following the action
        Quad(handle, new Vector2(BackLeft - 4, CanvasBack), new Vector2(BackRight + 4, CanvasBack),
            new Vector2(FrontRight + 4, CanvasFront), new Vector2(FrontLeft - 4, CanvasFront), Color.FromHex("#C9D1DC"));
        Ellipse(handle, _spot, Feet - 18, 170, 40, Color.White.WithAlpha(0.16f));
        Ellipse(handle, _spot, Feet - 14, 100, 24, Color.White.WithAlpha(0.14f));
        Rect(handle, FrontLeft - 4, CanvasFront - 2, FrontRight - FrontLeft + 8, 2, Color.FromHex("#8E99A8"));

        // the apron, with the night's name on it
        Rect(handle, FrontLeft - 4, CanvasFront, FrontRight - FrontLeft + 8, 18, Color.FromHex("#18224A"));
        Rect(handle, FrontLeft - 4, CanvasFront + 15, FrontRight - FrontLeft + 8, 3, Color.FromHex("#C8102E"));
        Rect(handle, 0, CanvasFront + 18, ArenaWidth, ArenaHeight - CanvasFront - 18, Color.FromHex("#08070C"));
        Centred(handle, _apron, Middle, CanvasFront + 3, Color.White.WithAlpha(0.85f));
    }

    protected override void DrawOverDecals(DrawingHandleScreen handle)
    {
        var t = _t;

        // the pool spreading under a fallen boxer's head, worked out from the time so it is there for late joiners too
        foreach (var down in _downs)
        {
            var a = t - down.Time - 0.5f;
            if (a <= 0)
                continue;

            if (!down.Out)
                a = Math.Min(a, CMUSports.BoxingCountDelay + down.Count * CMUSports.BoxingCountStep - 0.4f);

            var grown = MathF.Sqrt(Math.Clamp(a / (down.Out ? 9 : 3.5f), 0, 1));
            var size = (down.Out ? 40 : 16) * grown + 4;
            Texture(handle, Sprite(Splatter, Splatters[down.Index % 6]), down.Pool, new Vector2(size, size * 0.55f),
                down.Index * 1.3f, DarkBlood.WithAlpha(0.95f));
            Texture(handle, Sprite(Splatter, Splatters[(down.Index + 2) % 6]), down.Pool + new Vector2(size * 0.15f, 1),
                new Vector2(size * 0.75f, size * 0.42f), down.Index * 2.1f, Blood.WithAlpha(0.9f));
            Texture(handle, Sprite(Splatter, Splatters[(down.Index + 4) % 6]), down.Pool - new Vector2(size * 0.2f, -1),
                new Vector2(size * 0.6f, size * 0.35f), down.Index * 0.7f, Blood.WithAlpha(0.85f));
        }

        foreach (var tooth in _teeth)
        {
            if (t >= tooth.Time + ToothFlight)
                DrawTooth(handle, tooth.To, tooth.Index * 0.9f);
        }

        // shadows under everyone standing on the canvas
        for (var side = 0; side < 2; side++)
        {
            var b = _boxers[side];
            if (!b.Visible)
                continue;

            if (b.Down)
                Ellipse(handle, b.X, Feet - 2, 28, 4, Shadow);
            else
                Ellipse(handle, b.X, Feet + 1, 13 - (BoxerY - b.Y) * 0.5f, 3, Shadow);

            if (_corner[side].Visible)
                Ellipse(handle, _corner[side].X, CornerFeet + 1, 13, 3, Shadow);
        }

        if (_ref.Visible)
            Ellipse(handle, _ref.X, RefFeet + 1, 11, 2.5f, Shadow);

        if (_walker.Visible)
            Ellipse(handle, _walker.X, WalkerFeet + 1, 11, 2.5f, Shadow);
    }

    protected override void DrawOverlay(DrawingHandleScreen handle)
    {
        var t = _t;
        for (var side = 0; side < 2; side++)
        {
            DrawCut(handle, side);
            DrawGloves(handle, side);
        }

        DrawReferee(handle);
        for (var side = 0; side < 2; side++)
        {
            if (_sponge[side] is { } sponge)
            {
                Rect(handle, sponge.X - 3, sponge.Y - 2, 6, 4, Color.FromHex("#F2E6A0"));
                Frame(handle, sponge.X - 3, sponge.Y - 2, 6, 4, Color.FromHex("#9A8C40"));
            }
        }

        DrawSparks(handle, t);
        foreach (var tooth in _teeth)
        {
            var a = t - tooth.Time;
            if (a < 0 || a >= ToothFlight)
                continue;

            var s = a / ToothFlight;
            var at = Vector2.Lerp(tooth.From, tooth.To, s) - new Vector2(0, MathF.Sin(s * MathF.PI) * 30);
            DrawTooth(handle, at, s * 14 * Dir(1 - tooth.Side));
        }

        if (_walker.Visible && _sign > 0)
        {
            var top = _walker.Y - 36 * WalkerScale / 2 - 12;
            Line(handle, new Vector2(_walker.X + 3, top + 12), new Vector2(_walker.X + 3, _walker.Y - 20), Color.FromHex("#6B5A3A"));
            Rect(handle, _walker.X - 26, top - 2, 52, 14, Color.White);
            Frame(handle, _walker.X - 26, top - 2, 52, 14, Color.FromHex("#C8102E"));
            Centred(handle, _signs[_sign], _walker.X, top - 0.5f, Color.FromHex("#B00E24"));
        }

        // the front of the ring, in front of everyone
        Post(handle, FrontLeft, CanvasFront, FrontPost, 0, 8);
        Post(handle, FrontRight, CanvasFront, FrontPost, 1, 8);
        for (var rope = 0; rope < Ropes.Length; rope++)
        {
            var y = CanvasFront - FrontPost * Ropes[rope];
            Rect(handle, FrontLeft + 4, y - 1, FrontRight - FrontLeft - 8, 2, RopeColours[rope].WithAlpha(0.85f));
        }

        // ringside heads, the press row
        for (var seat = 0; seat < 21; seat++)
        {
            var x = 12 + seat * 25 + (seat % 3) * 3;
            var y = ArenaHeight - 6 + (seat % 2) * 2;
            Rect(handle, x - 11, y + 3, 22, 8, Color.FromHex("#100E14"));
            Circle(handle, x, y, 7, Color.FromHex("#18151E"));
        }

        DrawPlates(handle, t);
    }

    private void DrawPlates(DrawingHandleScreen handle, float t)
    {
        Rect(handle, 4, Bar + 2, 166, 24, Color.Black.WithAlpha(0.6f));
        Rect(handle, 4, Bar + 2, 3, 24, Plates[0]);
        Rect(handle, ArenaWidth - 170, Bar + 2, 166, 24, Color.Black.WithAlpha(0.6f));
        Rect(handle, ArenaWidth - 7, Bar + 2, 3, 24, Plates[1]);
        for (var side = 0; side < 2; side++)
        {
            for (var drop = 0; drop < _cutLevel[side]; drop++)
            {
                var x = side == 0 ? 160 - drop * 8 : ArenaWidth - 160 + drop * 8;
                Circle(handle, x, Bar + 18, 2.6f, Blood);
                Circle(handle, x - 0.8f, Bar + 17, 0.8f, Color.FromHex("#FF6060"));
            }
        }

        if (_play == null || t < 0)
            return;

        // the round lights, and the round's card between rounds
        var round = t < 4 ? 1 : RoundOf(t);
        for (var pip = 1; pip <= CMUSports.BoxingRounds; pip++)
        {
            var x = Middle + (pip - 2) * 12;
            Circle(handle, x, Bar + 8, 3.5f, pip <= round ? Color.Gold : Color.White.WithAlpha(0.25f));
        }

        if (!_resting && !(t >= RoundStart(CMUSports.BoxingRounds) + CMUSports.RoundLength && _ko < 0))
            return;

        var rest = t - RoundStart(round) - CMUSports.RoundLength;
        if (rest < 0.4f)
            return;

        var shown = _resting ? 1 : CMUSports.BoxingRounds;
        var y = Bar + 16;
        var height = 12 + shown * 12 + (_resting ? 0 : 12);
        Rect(handle, Middle - 64, y, 128, height, Color.Black.WithAlpha(0.7f));
        Frame(handle, Middle - 64, y, 128, height, Color.Gold.WithAlpha(0.6f));
        Centred(handle, Loc.GetString("cmu-game-boxing-cards"), Middle, y + 1, Color.Gold, true);
        var redTotal = 0;
        var blueTotal = 0;
        for (var line = 0; line < shown; line++)
        {
            var index = _resting ? round - 1 : line;
            var (boss, red, blue) = _cards[index];
            if (boss < 0)
                continue;

            redTotal += red;
            blueTotal += blue;
            var row = y + 13 + line * 12;
            Centred(handle, _signs[index + 1], Middle, row, Color.White.WithAlpha(0.8f), true);
            Centred(handle, Numbers[red - 1], Middle - 48, row, Plates[0], true);
            Centred(handle, Numbers[blue - 1], Middle + 48, row, Plates[1], true);
        }

        if (_resting)
            return;

        var total = y + 13 + shown * 12;
        Centred(handle, redTotal.ToString(), Middle - 48, total, Plates[0], true);
        Centred(handle, blueTotal.ToString(), Middle + 48, total, Plates[1], true);
    }

    /// <summary>The gloves the sprite cannot show: the guard in front of the face and the punching arm.</summary>
    private void DrawGloves(DrawingHandleScreen handle, int side)
    {
        var b = _boxers[side];
        if (!b.Visible || b.Down)
            return;

        var dir = Dir(side);
        if (b.Raised)
        {
            for (var hand = -1; hand <= 1; hand += 2)
            {
                var shoulder = new Vector2(b.X + hand * 5, b.Y - 8);
                var glove = new Vector2(b.X + hand * 9, b.Y - 30);
                Arm(handle, shoulder, glove, _sleeves[side]);
                Glove(handle, glove, side, hand);
            }

            return;
        }

        if (b.HandUp)
        {
            var glove = RaisedGlove(b, side);
            Arm(handle, new Vector2(glove.X, b.Y - 8), glove, _sleeves[side]);
            Glove(handle, glove, side, -dir);
            return;
        }

        if (!b.Arms)
            return;

        if (b.Guard)
        {
            Glove(handle, Offset(b, dir * 7, -12), side, dir);
            Glove(handle, Offset(b, dir * 11, -16), side, dir);
            return;
        }

        if (b.Punch < 0 || b.Lead)
            Glove(handle, Offset(b, dir * 6, -6), side, dir);

        if (b.Punch < 0 || !b.Lead)
            Glove(handle, Offset(b, dir * 11, -9), side, dir);

        if (b.Punch < 0)
            return;

        Arm(handle, b.Shoulder, b.Glove, _sleeves[side]);
        Glove(handle, b.Glove, side, dir);
    }

    private void Arm(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color sleeve)
    {
        var along = to - from;
        if (along.LengthSquared() < 1)
            return;

        var across = Vector2.Normalize(new Vector2(-along.Y, along.X)) * 1.8f;
        Quad(handle, from + across, from - across, to - across, to + across, sleeve);
        var wrist = from + along * 0.6f;
        Quad(handle, wrist + across * 0.8f, wrist - across * 0.8f, to - across * 0.8f, to + across * 0.8f, Skin);
    }

    private void Glove(DrawingHandleScreen handle, Vector2 at, int side, int facing)
    {
        Circle(handle, at.X, at.Y, 4.3f, Color.Black.WithAlpha(0.55f));
        Circle(handle, at.X, at.Y, 3.6f, Gloves[side]);
        Circle(handle, at.X + facing * 0.8f - 0.6f, at.Y - 1.3f, 1.2f, Color.White.WithAlpha(0.45f));
        Rect(handle, at.X - facing * 4 - 1, at.Y - 1.5f, 2, 3, Color.FromHex("#F4F4F4"));
    }

    /// <summary>Where a winner's glove is when the referee holds it up.</summary>
    private static Vector2 RaisedGlove(Boxer b, int side)
    {
        return new Vector2(b.X + Dir(side) * 7, b.Y - 32);
    }

    /// <summary>The cut over a boxer's eye, and the blood running from it; greased shut in the corner.</summary>
    private void DrawCut(DrawingHandleScreen handle, int side)
    {
        var b = _boxers[side];
        var level = _cutLevel[side];
        if (level == 0 || !b.Visible || b.Down || b.Facing == Direction.South)
            return;

        var dir = Dir(side);
        var cut = Offset(b, dir * 5, -21);
        Line(handle, cut - new Vector2(2, 1), cut + new Vector2(2, 1), DarkBlood);
        Line(handle, cut - new Vector2(2, 0), cut + new Vector2(2, 2), Blood);
        Rect(handle, cut.X - 0.5f, cut.Y, 1.6f, 2 + level * 3, Blood.WithAlpha(0.9f));
        if (level >= 2)
            Rect(handle, cut.X + dir * 1.5f, cut.Y + 1, 1.2f, level * 2, DarkBlood.WithAlpha(0.9f));

        if (_resting)
            Circle(handle, cut.X, cut.Y, 1.6f, Color.White.WithAlpha(0.5f));
    }

    private void DrawReferee(DrawingHandleScreen handle)
    {
        if (!_ref.Visible || _refArms == RefArms.None)
            return;

        var shirt = Color.FromHex("#E4E4E8");
        var dir = _refPointing;
        switch (_refArms)
        {
            case RefArms.Count:
            {
                // each count is a chop down towards the canvas
                var phase = _refPhase % 1;
                var chop = phase < 0.2f ? Ease(phase / 0.2f) : 1 - Ease((phase - 0.2f) / 0.8f);
                var angle = Lerp(-1.3f, 0.7f, chop);
                var shoulder = new Vector2(_ref.X + dir * 4, _ref.Y - 8);
                var hand = shoulder + new Vector2(dir * MathF.Cos(angle), MathF.Sin(angle)) * 12;
                RefArm(handle, shoulder, hand, shirt);
                break;
            }
            case RefArms.Wave:
            {
                var swing = MathF.Sin(_t * 12) * 0.8f;
                for (var hand = -1; hand <= 1; hand += 2)
                {
                    var shoulder = new Vector2(_ref.X + hand * 5, _ref.Y - 8);
                    var angle = -MathF.PI / 2 + hand * (0.5f + swing * hand);
                    RefArm(handle, shoulder, shoulder + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12, shirt);
                }

                break;
            }
            case RefArms.Break:
                for (var hand = -1; hand <= 1; hand += 2)
                {
                    var shoulder = new Vector2(_ref.X + hand * 5, _ref.Y - 7);
                    RefArm(handle, shoulder, shoulder + new Vector2(hand * 12, 2), shirt);
                }

                break;
            case RefArms.Raise:
                RefArm(handle, new Vector2(_ref.X + Math.Sign(_refTarget.X - _ref.X) * 5, _ref.Y - 8), _refTarget, shirt);
                break;
        }
    }

    private void RefArm(DrawingHandleScreen handle, Vector2 shoulder, Vector2 hand, Color shirt)
    {
        var along = hand - shoulder;
        if (along.LengthSquared() < 1)
            return;

        var across = Vector2.Normalize(new Vector2(-along.Y, along.X)) * 1.5f;
        Quad(handle, shoulder + across, shoulder - across, hand - across, hand + across, shirt);
        Circle(handle, hand.X, hand.Y, 2, Skin);
    }

    /// <summary>The impact stars on the punches landing now.</summary>
    private void DrawSparks(DrawingHandleScreen handle, float t)
    {
        if (_play == null || t < 4)
            return;

        _spark ??= Cache.GetResource<RSIResource>(new ResPath("/Textures/_RMC14/Effects/attacks.rsi")).RSI.TryGetState("punch", out var state)
            ? state.GetFrames(RsiDirection.South)
            : Array.Empty<Texture>();
        if (_spark.Length == 0)
            return;

        var events = _play.Events;
        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.Time > t)
                break;

            var a = t - ev.Time;
            if (ev.Action != CMUSportsAction.Punch || a >= 0.2f || ev.Value == CMUSports.BoxingSlipped)
                continue;

            var d = _boxers[1 - ev.Side];
            var dir = Dir(ev.Side);
            var blocked = ev.Value == CMUSports.BoxingBlocked;
            var at = blocked ? Offset(d, -dir * 11, -10)
                : ev.State == CMUSports.BoxingBody ? Offset(d, -dir * 7, 2)
                : Offset(d, -dir * 8, -17);
            var frame = _spark[Math.Min(_spark.Length - 1, (int) (a / 0.05f))];
            var size = blocked ? 20 : ev.Extra >= CMUSports.BoxingHook ? 36 : 28;
            Texture(handle, frame, at, new Vector2(size, size), i * 1.7f, blocked ? Color.FromHex("#CFE8FF") : (Color?) null);
        }
    }

    private void DrawTooth(DrawingHandleScreen handle, Vector2 at, float rotation)
    {
        var c = MathF.Cos(rotation);
        var s = MathF.Sin(rotation);
        Vector2 Turn(float x, float y) => at + new Vector2(x * c - y * s, x * s + y * c);
        Quad(handle, Turn(-2, -1.6f), Turn(2, -1.6f), Turn(1.5f, 2), Turn(-1.5f, 2), Color.FromHex("#F6F1E2"));
        Rect(handle, at.X - 0.6f, at.Y + 1, 1.2f, 1.2f, Blood);
    }

    private void Post(DrawingHandleScreen handle, float x, float floor, float height, int side, float width)
    {
        Rect(handle, x - width / 2, floor - height, width, height + 2, Color.FromHex("#98A2AE"));
        Rect(handle, x - width / 2 - 1, floor - height * 0.85f, width + 2, height * 0.66f, Gloves[side]);
        Rect(handle, x - width / 2, floor - height - 2, width, 3, Color.FromHex("#C9D0D8"));
    }

    private void Ellipse(DrawingHandleScreen handle, float x, float y, float rx, float ry, Color colour)
    {
        var origin = Box(0, 0, 0, 0).TopLeft;
        var scale = UIScale;
        _fan[0] = origin + new Vector2(x, y) * scale;
        for (var i = 1; i < _fan.Length; i++)
        {
            var angle = (i - 1) * MathF.Tau / (_fan.Length - 2);
            _fan[i] = origin + new Vector2(x + MathF.Cos(angle) * rx, y + MathF.Sin(angle) * ry) * scale;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _fan, colour);
    }

    private void Quad(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
    {
        var origin = Box(0, 0, 0, 0).TopLeft;
        var scale = UIScale;
        _quad[0] = origin + a * scale;
        _quad[1] = origin + b * scale;
        _quad[2] = origin + c * scale;
        _quad[3] = origin + d * scale;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _quad, colour);
    }

    /// <summary>Small text centred on a spot, shaken with the field.</summary>
    private void Centred(DrawingHandleScreen handle, string text, float x, float y, Color colour, bool outline = false)
    {
        var font = SmallFont;
        var scale = UIScale;
        var size = handle.GetDimensions(font, text, scale);
        var at = Box(x, y, 0, 0).TopLeft - new Vector2(size.X / 2, 0);
        handle.DrawString(font, at, text, scale, colour, outline ? new TextOutline(1, Color.Black.WithAlpha(0.6f)) : null);
    }

    #endregion

    #region Helpers

    private static Boxer Fresh(int side)
    {
        return new Boxer
        {
            X = Corners[side],
            Y = BoxerY,
            Facing = side == 0 ? Direction.East : Direction.West,
            Visible = true,
            Arms = true,
            Punch = -1,
        };
    }

    /// <summary>Where the referee hovers while they fight: off to one side, crossing behind them now and then.</summary>
    private static float RefSpot(Boxer[] boxers, float t)
    {
        return Math.Clamp((boxers[0].X + boxers[1].X) / 2 + Math.Clamp(MathF.Sin(t * 0.21f) * 3, -1, 1) * 58, 110, 410);
    }

    /// <summary>Which way a side faces: red to the right, blue to the left.</summary>
    private static int Dir(int side)
    {
        return side == 0 ? 1 : -1;
    }

    private static int RoundOf(float t)
    {
        return Math.Clamp((int) ((t - 4) / (CMUSports.RoundLength + CMUSports.RoundRest)) + 1, 1, CMUSports.BoxingRounds);
    }

    private static float RoundStart(int round)
    {
        return 4 + (round - 1) * (CMUSports.RoundLength + CMUSports.RoundRest);
    }

    private static float Windup(int kind)
    {
        return kind switch { CMUSports.BoxingJab => 0.16f, CMUSports.BoxingCross => 0.22f, _ => 0.28f };
    }

    private static float Recover(int kind)
    {
        return kind == CMUSports.BoxingJab ? 0.2f : 0.28f;
    }

    private static float Power(int kind)
    {
        return kind switch { CMUSports.BoxingJab => 1, CMUSports.BoxingCross => 2, CMUSports.BoxingHook => 2.5f, _ => 3 };
    }

    /// <summary>The worst cut a side has at a time.</summary>
    private int CutAt(int side, float t)
    {
        var level = 0;
        foreach (var (time, hurt, cut) in _cuts)
        {
            if (hurt == side && time <= t)
                level = Math.Max(level, cut);
        }

        return level;
    }

    /// <summary>A point on a boxer's sprite, from its centre, turned with the boxer.</summary>
    private static Vector2 Offset(Boxer b, float dx, float dy)
    {
        var r = b.Rot * MathF.PI / 180;
        var c = MathF.Cos(r);
        var s = MathF.Sin(r);
        return new Vector2(b.X + dx * c + dy * s, b.Y - dx * s + dy * c);
    }

    private static bool Female(CMUBetMatch match, int side)
    {
        return side < match.Entrants.Count && CMUSports.Female(match.Sport, match.Entrants[side]);
    }

    private static float Ease(float x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static Color Darker(Color colour)
    {
        return new Color(colour.R * 0.75f, colour.G * 0.75f, colour.B * 0.75f, colour.A);
    }

    /// <summary>A number from 0 to 1 that is always the same for the same event, for where things land.</summary>
    private static float Hash(int index)
    {
        var h = (uint) index * 2654435761u;
        return ((h >> 16) & 1023) / 1023f;
    }

    private static Seat[] MakeSeats()
    {
        var seats = new List<Seat>();
        var seed = 12345u;
        float Next()
        {
            seed = seed * 1664525u + 1013904223u;
            return (seed >> 8) / 16777216f;
        }

        for (var row = 0; row < RowY.Length; row++)
        {
            var spacing = 10.5f + row * 1.4f;
            for (var x = 4 + (row % 2) * spacing / 2; x < ArenaWidth; x += spacing)
            {
                Next();
                Next();
                seats.Add(new Seat(x + (Next() - 0.5f) * 2, row, Next(), Next() < 0.3f));
            }
        }

        return seats.ToArray();
    }

    /// <summary>The game's human skin tones (HumanTonedSkinColoration.FromUnary), 0 the lightest and 100 the darkest.</summary>
    private static Color SkinTone(float tone)
    {
        var offset = tone - 20;
        var hue = offset <= 0 ? 25 - offset : 25;
        var sat = offset <= 0 ? 20 : 20 + offset;
        var val = offset <= 0 ? 100 : 100 - offset;
        return Color.FromHsv(new Vector4(hue / 360f, sat / 100f, val / 100f, 1));
    }

    /// <summary>
    /// What the fan in a seat looks like in a match, the same for every client (and the web emulator) from the match
    /// id and the seat: their layers in drawing order, and the corner they came for, or -1.
    /// </summary>
    private static int FanLook(int matchId, int seat, List<FanLayer> layers, out Color skin)
    {
        var s = unchecked((uint) matchId * 2654435761u ^ (uint) (seat + 1) * 40503u);
        double Next()
        {
            s = unchecked(s * 1664525u + 1013904223u);
            return (s >> 8) / 16777216.0;
        }

        Next();
        var female = Next() < 0.45;
        skin = SkinTone((float) (Next() * 90));
        var outfitRoll = Next();
        var outfitPick = Next();
        var hairPick = Next();
        var hairColourPick = Next();
        var hatRoll = Next();
        var hatPick = Next();
        var corner = outfitRoll < 0.11 ? 0 : outfitRoll < 0.22 ? 1 : -1;
        var marine = corner < 0 && outfitRoll < 0.36;
        var sex = female ? "_f" : "_m";

        layers.Clear();
        layers.Add(new FanLayer(FanParts, "torso" + sex, skin, false));
        layers.Add(new FanLayer(FanParts, "head" + sex, skin, false));
        layers.Add(new FanLayer(FanEyes, "eyes", FanEyeColour, false));
        layers.Add(new FanLayer(FanParts, "r_arm", skin, true));
        layers.Add(new FanLayer(FanParts, "l_arm", skin, true));
        if (corner >= 0)
        {
            layers.Add(new FanLayer(FanColourSuit, "equipped-INNERCLOTHING", FanCorners[corner], false));
        }
        else
        {
            var outfits = marine ? FanMarine : FanCivilian;
            layers.Add(new FanLayer(FanClothes + outfits[(int) (outfitPick * outfits.Length)], "equipped-INNERCLOTHING", null, false));
        }

        layers.Add(new FanLayer(FanParts, "l_hand", skin, true));
        layers.Add(new FanLayer(FanParts, "r_hand", skin, true));
        var styles = female ? FanFemaleHair : FanMaleHair;
        layers.Add(new FanLayer(FanHair, styles[(int) (hairPick * styles.Length)], FanHairColours[(int) (hairColourPick * FanHairColours.Length)], false));
        if (marine && hatRoll < 0.5)
            layers.Add(new FanLayer(FanClothes + FanMarineCap, "equipped-HELMET", null, false));
        else if (!marine && hatRoll < 0.15)
            layers.Add(new FanLayer(FanClothes + FanHats[(int) (hatPick * FanHats.Length)], "equipped-HELMET", null, false));

        return corner;
    }

    /// <summary>
    /// Composites every fan of a match once, on the CPU, into one texture of 32 by 32 cells: arms down, then arms up
    /// with the arm layers left off and the sleeves painted over the head.
    /// </summary>
    private void BuildCrowd(int matchId)
    {
        _crowdId = matchId;
        _crowd?.Dispose();
        _crowd = null;
        var rows = (Seats.Length * 2 + FanColumns - 1) / FanColumns;
        using var atlas = new Image<Rgba32>(FanColumns * 32, rows * 32);
        var pixels = atlas.GetPixelSpan();
        var width = atlas.Width;
        var layers = new List<FanLayer>();
        for (var seat = 0; seat < Seats.Length; seat++)
        {
            _fanCorner[seat] = FanLook(matchId, seat, layers, out var skin);
            var light = FanLight[Seats[seat].Row];
            for (var pose = 0; pose < 2; pose++)
            {
                var cell = seat * 2 + pose;
                var ox = cell % FanColumns * 32;
                var oy = cell / FanColumns * 32;
                foreach (var layer in layers)
                {
                    if (layer.Arm && pose == 1 || FanImage(layer.Rsi, layer.State) is not { } image)
                        continue;

                    Composite(image, layer.Tint, pixels, width, ox, oy);
                }

                if (pose == 1)
                {
                    // arms thrown up: the sleeves from the shoulders past the head, the hands at the top
                    var sleeve = pixels[(oy + 15) * width + ox + 16];
                    sleeve.A = 255;
                    var hand = new Rgba32(skin.R, skin.G, skin.B, 1f);
                    Fill(pixels, width, ox + 8, oy + 6, 2, 8, sleeve);
                    Fill(pixels, width, ox + 22, oy + 6, 2, 8, sleeve);
                    Fill(pixels, width, ox + 8, oy + 4, 2, 2, hand);
                    Fill(pixels, width, ox + 22, oy + 4, 2, 2, hand);
                }

                // darker the further back the seat
                for (var y = 0; y < 32; y++)
                {
                    for (var x = 0; x < 32; x++)
                    {
                        ref var p = ref pixels[(oy + y) * width + ox + x];
                        p.R = (byte) (p.R * light);
                        p.G = (byte) (p.G * light);
                        p.B = (byte) (p.B * light);
                    }
                }
            }
        }

        _crowd = _clyde.LoadTextureFromImage(atlas, "cmu-boxing-crowd");
    }

    /// <summary>The south-facing first frame of a layer, read once, or null when it cannot be read.</summary>
    private Image<Rgba32>? FanImage(string rsi, string state)
    {
        var path = $"{rsi}/{state}.png";
        if (FanImages.TryGetValue(path, out var image))
            return image;

        try
        {
            using var stream = Cache.ContentFileRead(new ResPath(path));
            image = Image.Load<Rgba32>(stream);
        }
        catch (Exception e)
        {
            _logs.GetSawmill("cmu.boxing").Warning($"Cannot read {path}: {e.Message}");
            image = null;
        }

        FanImages[path] = image;
        return image;
    }

    /// <summary>Draws a layer's south frame (the top left 32 by 32 of its sheet) over a cell, tinted like a sprite layer.</summary>
    private static void Composite(Image<Rgba32> layer, Color? tint, Span<Rgba32> pixels, int width, int ox, int oy)
    {
        var source = layer.GetPixelSpan();
        var size = Math.Min(32, Math.Min(layer.Width, layer.Height));
        var (tr, tg, tb) = tint is { } t ? (t.R, t.G, t.B) : (1f, 1f, 1f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var src = source[y * layer.Width + x];
                if (src.A == 0)
                    continue;

                ref var dst = ref pixels[(oy + y) * width + ox + x];
                var a = src.A / 255f;
                var under = dst.A / 255f * (1 - a);
                var outA = a + under;
                dst.R = (byte) Math.Clamp((src.R * tr * a + dst.R * under) / outA, 0, 255);
                dst.G = (byte) Math.Clamp((src.G * tg * a + dst.G * under) / outA, 0, 255);
                dst.B = (byte) Math.Clamp((src.B * tb * a + dst.B * under) / outA, 0, 255);
                dst.A = (byte) Math.Clamp(outA * 255, 0, 255);
            }
        }
    }

    private static void Fill(Span<Rgba32> pixels, int width, int x, int y, int w, int h, Rgba32 colour)
    {
        for (var row = y; row < y + h; row++)
        {
            for (var column = x; column < x + w; column++)
            {
                pixels[row * width + column] = colour;
            }
        }
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _crowd?.Dispose();
        _crowd = null;
        _crowdId = -1;
    }

    #endregion
}
