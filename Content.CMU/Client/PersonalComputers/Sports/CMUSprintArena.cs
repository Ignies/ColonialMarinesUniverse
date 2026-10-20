using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The 100 metre dash under the colony dome: five lanes, crouched starts and the starter's pistol, dust and sweat at
/// speed, the crowd on its feet as the leader pulls away, a photo-finish snapshot and a split board in the infield.
/// A runner who goes down skids face first and leaves a smear down the lane, and still crosses the line at their
/// real time: every motion curve is planned around the times the match drew.
/// </summary>
public sealed class CMUSprintArena : CMUSportsArena
{
    private const float TrackTop = 70;
    private const float Lane = 26;

    /// <summary>Where the feet meet the track, from the top of a lane.</summary>
    private const float Ground = 20;

    private const float StartLine = 116;
    private const float Blocks = StartLine - 22;
    private const float Behind = StartLine - 28;
    private const float FinishLine = 470;

    /// <summary>Feet at the line when the chest is on it.</summary>
    private const float FinishFeet = FinishLine - 8;

    /// <summary>From a doll's centre to its feet, at scale 1.</summary>
    private const float Body = 15;

    private const float Stride = 11;

    /// <summary>How long a sprinter takes to reach top speed, as a share of the race.</summary>
    private const float Accel = 0.13f;

    private const float Stumble = 0.2f;
    private const float GetUp = 0.5f;
    private const float Brake = 1.15f;

    private const float BoardX = 84;
    private const float BoardY = 203;
    private const float Column = 66;
    private const float ScreenX = 198;
    private const float ScreenY = 27;
    private const float ScreenW = 124;
    private const float ScreenH = 38;
    private const float PhotoX = 140;
    private const float PhotoY = 74;
    private const float PhotoW = 170;
    private const float PhotoH = 124;
    private const float PhotoShow = 2.8f;
    private const float CloseFinish = 0.1f;
    private const int RecordTime = 970;
    private const float StarterX = 44;
    private const float StarterY = 216;

    private static readonly float CurveNorm = 1 - Accel * (1 - MathF.Exp(-1 / Accel));

    private const string Pistol = "/Textures/Objects/Weapons/Guns/Pistols/viper.rsi";

    /// <summary>The barrel's end on the pistol sprite, from its centre, at the size it is drawn.</summary>
    private static readonly Vector2 Barrel = new(5.6f, -3.4f);
    private const string Gunshot = "/Audio/_RMC14/Weapons/Guns/Gunshots/gun_m1984_1.ogg";
    private const string Recall = "/Audio/_RMC14/Weapons/Guns/Gunshots/gun_m1984_2.ogg";
    private const string Cock = "/Audio/_RMC14/Weapons/Guns/Cock/gun_pistol_cocked.ogg";
    private const string Marks = "/Audio/Items/Whistle/whistle_1.ogg";
    private const string Beam = "/Audio/Machines/quickbeep.ogg";
    private const string Camera = "/Audio/Misc/camera_snap.ogg";
    private const string CheerSmall = "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_small.ogg";
    private const string Fall = "/Audio/CMU14/PersonalComputers/Games/fall.ogg";
    private const string Slap = "/Audio/Effects/Footsteps/meatslap.ogg";

    private static readonly string[] Claps =
    {
        "/Audio/Effects/Emotes/clap1.ogg", "/Audio/Effects/Emotes/clap2.ogg", "/Audio/Effects/Emotes/clap3.ogg",
        "/Audio/Effects/Emotes/clap4.ogg",
    };

    private static readonly string[] Whistles =
    {
        "/Audio/Voice/Human/whistle_1.ogg", "/Audio/Voice/Human/whistle_2.ogg", "/Audio/Voice/Human/whistle_3.ogg",
        "/Audio/Voice/Human/whistle_4.ogg",
    };

    private static readonly string[] Gasps =
    {
        "/Audio/Effects/Gasp/gasp_male1.ogg", "/Audio/Effects/Gasp/gasp_male2.ogg", "/Audio/Effects/Gasp/gasp_female1.ogg",
        "/Audio/Effects/Gasp/gasp_female2.ogg", "/Audio/Effects/Gasp/gasp_female3.ogg",
    };

    private static readonly string[] Steps =
    {
        "/Audio/Effects/Footsteps/grass1.ogg", "/Audio/Effects/Footsteps/grass2.ogg", "/Audio/Effects/Footsteps/grass3.ogg",
        "/Audio/Effects/Footsteps/grass4.ogg",
    };

    private static readonly string[] Splashes = { "/Audio/Effects/Fluids/blood1.ogg", "/Audio/Effects/Fluids/blood2.ogg" };

    /// <summary>The big cheers are long: one plays per race, at the winner, and runs out by itself.</summary>
    private static readonly string[] Cheers =
    {
        "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_1.ogg", "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_2.ogg",
    };

    private static readonly string[] Swooshes =
    {
        "/Audio/CMU14/PersonalComputers/Games/swoosh_1.ogg", "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg",
        "/Audio/CMU14/PersonalComputers/Games/swoosh_3.ogg",
    };

    private static readonly string[] Pops = { "/Audio/Effects/Emotes/snap1.ogg", "/Audio/Effects/Emotes/snap2.ogg", "/Audio/Effects/Emotes/snap3.ogg" };

    private static readonly Color Track = Color.FromHex("#B5482F");
    private static readonly Color TrackGrain = Color.FromHex("#9F3C26");
    private static readonly Color Grass = Color.FromHex("#2E5A2A");
    private static readonly Color GrassStripe = Color.FromHex("#346732");
    private static readonly Color Dust = Color.FromHex("#D08A68");
    private static readonly Color Sweat = Color.FromHex("#CFEFFF");
    private static readonly Color Smoke = Color.FromHex("#C8C8C8");
    private static readonly Color Amber = Color.FromHex("#FFB000");
    private static readonly Color Gold = Color.FromHex("#FFD24A");
    private static readonly Color Silver = Color.FromHex("#D6DCE4");
    private static readonly Color Bronze = Color.FromHex("#D9944A");
    private static readonly Color Alarm = Color.FromHex("#FF4A3A");

    private const string Parts = "/Textures/Mobs/Species/Human/parts.rsi";
    private const string Eyes = "/Textures/Mobs/Customization/eyes.rsi";
    private const string Hair = "/Textures/Mobs/Customization/human_hair.rsi";
    private const string ColourJumpsuit = "/Textures/Clothing/Uniforms/Jumpsuits/Color/color.rsi";
    private const string Clothing = "/Textures/_RMC14/Objects/Clothing/";
    private const string MarineUniform = Clothing + "Uniforms/Marine/standard.rsi";
    private const string MarineCap = Clothing + "Head/Hats/cap.rsi";
    private const int FanCell = 16;
    private const int FanHeight = 12;
    private const int AtlasColumns = 16;

    private static readonly Color[] Skins =
    {
        Color.FromHex("#FFE1CC"), Color.FromHex("#EDC2A0"), Color.FromHex("#D9A577"), Color.FromHex("#C08A62"),
        Color.FromHex("#8A5634"), Color.FromHex("#5C3A21"),
    };

    private static readonly Color[] HairColours =
    {
        Color.FromHex("#2A1A10"), Color.FromHex("#5A3A20"), Color.FromHex("#C89A50"), Color.FromHex("#1A1A1A"),
        Color.FromHex("#8A3A1A"), Color.FromHex("#D8C8A8"), Color.FromHex("#6A6A6A"),
    };

    private static readonly Color EyeColour = Color.FromHex("#2B1A10");

    /// <summary>The colony jumpsuit's colours, as the game's coloured jumpsuits tint it.</summary>
    private static readonly Color[] JumpsuitTints =
    {
        Color.FromHex("#EB0C07"), Color.FromHex("#52AECC"), Color.FromHex("#9ED63A"), Color.FromHex("#FFE14D"),
        Color.FromHex("#FF8C19"), Color.FromHex("#9F70CC"), Color.FromHex("#A17229"),
    };

    private static readonly string[] Civilian =
    {
        "blue_jumpsuit", "brown_utility", "khaki_workwear", "grey_jumpsuit", "green_utility", "blue_workwear",
        "lightbrown_jumpsuit", "red_utility", "yellow_jumpsuit", "white_jumpsuit_alt", "tartarus_mining", "weyamart",
        "pizza_galaxy", "sanitation", "scientist_jumpsuit", "doctor", "emt", "steward", "daily_grind",
    };

    private static readonly string[] Hats =
    {
        "Hats/blue_trucker_cap", "Hats/red_trucker_cap", "Hats/beret_red", "Hats/cargo_cap", "Hats/weyamart_cap",
        "Hats/pizza_galaxy_cap", "Hats/ushanka", "Hats/durag", "Beanies/beanie", "Beanies/gray", "Beanies/green", "Beanies/tan",
    };

    private static readonly string[] MaleHair = { "crewcut", "business", "bedhead", "buzzcut", "afro", "combover", "cornrows", "dreads", "emo", "curls" };
    private static readonly string[] FemaleHair = { "ponytail", "bob", "bun", "longstraightponytail", "curls", "doublebun", "braid", "beehive", "bobcurl" };

    /// <summary>The stands get darker towards the back.</summary>
    private static readonly float[] RowShade = { 0.55f, 0.68f, 0.8f };

    /// <summary>Each sprite state's south-facing first frame, read once from its PNG for the crowd's compositing.</summary>
    private static readonly Dictionary<string, Rgba32[]?> SouthFrames = new();

    private static readonly Fan[] Crowd = SeatCrowd();
    private static readonly float[] Flags = { 34, 150, 372, 478 };
    private static readonly Vector2[] Grain = SpreadGrain();

    private enum Mishap : byte
    {
        None,
        Fall,
        Hamstring,
        Stagger,
    }

    /// <summary>One lane's race, worked out once from the match's events.</summary>
    private sealed class Runner
    {
        public int Score;
        public float Reaction = 0.15f;
        public float Finish;

        /// <summary>The time the runner was on for before anything went wrong.</summary>
        public float Base;

        public Mishap Mishap;
        public float At;
        public int Severity = 1;
        public int Into = -1;
        public float Slide = 1.1f;
        public float AtS;
        public float AtV;
        public float UpAt;
        public float RecoverAt;
        public float RecoverS;
        public float RecoverV;
        public float LimpSpeed;
        public float LimpPeriod = 1;
        public float EndSpeed;
        public float Split = float.NaN;
        public int Rank;
        public bool Female;
    }

    /// <summary>How a runner stands at a moment: feet, height off the track, forward lean in degrees.</summary>
    private struct Stance
    {
        public float X;
        public float Lift;
        public float Lean;
        public float Swerve;
        public float Speed;
        public Direction Dir;
        public bool Prone;
    }

    /// <summary>A seat in the stands. Who sits there changes with the match; see <see cref="Seat"/>.</summary>
    private readonly record struct Fan(float X, float Y, int Row);

    /// <summary>The fan in a seat for this match: how they move, and the lane flag they wave if any.</summary>
    private struct Seat
    {
        public Color Skin;
        public float Phase;
        public float Speed;
        public float Arms;
        public int Flag;
    }

    private Runner[] _runners = Array.Empty<Runner>();
    private Stance[] _stance = Array.Empty<Stance>();
    private Stance[] _snap = Array.Empty<Stance>();
    private Vector2[] _centre = Array.Empty<Vector2>();
    private float[] _dust = Array.Empty<float>();
    private float[] _sweat = Array.Empty<float>();
    private float[] _drip = Array.Empty<float>();
    private float[] _smear = Array.Empty<float>();
    private int[] _pool = Array.Empty<int>();
    private string[] _names = Array.Empty<string>();
    private string[] _board = Array.Empty<string>();
    private int[] _boardStage = Array.Empty<int>();
    private string[] _ranks = Array.Empty<string>();
    private (int Id, int Seed, int Winner)? _planned;
    private SpriteSystem? _sprites;
    [Dependency] private readonly IClyde _clyde = default!;
    private OwnedTexture? _crowd;
    private int _crowdFor = int.MinValue;
    private readonly Seat[] _seats = new Seat[Crowd.Length];
    private readonly float[] _canvas = new float[32 * 32 * 4];

    private int _lanes;
    private bool _racing;
    private int _winner;
    private float _winAt = float.MaxValue;
    private float _roarAt = float.MaxValue;
    private bool _photoClose;
    private bool _record;
    private int _falseLane = -1;
    private float _falseAt = float.NaN;
    private float _time = -1;
    private float _prev = float.NaN;
    private float _clock;
    private float _excite = 0.3f;
    private float _step;
    private float _boardAlpha = 1;
    private float _photoAge = -1;

    private SpriteSystem Sprites => _sprites ??= Entities.System<SpriteSystem>();

    protected override void Build(CMUBetMatch match)
    {
        _lanes = match.Entrants.Count;
        _runners = new Runner[_lanes];
        _stance = new Stance[_lanes];
        _snap = new Stance[_lanes];
        _centre = new Vector2[_lanes];
        _dust = new float[_lanes];
        _sweat = new float[_lanes];
        _drip = new float[_lanes];
        _smear = new float[_lanes];
        _pool = new int[_lanes];
        _names = new string[_lanes];
        _board = new string[_lanes];
        _boardStage = new int[_lanes];
        _ranks = new string[_lanes];
        _planned = null;
        _racing = false;
        _falseLane = -1;
        _falseAt = float.NaN;
        _winAt = float.MaxValue;
        _roarAt = float.MaxValue;
        _prev = float.NaN;
        _photoAge = -1;
        for (var lane = 0; lane < _lanes; lane++)
        {
            _runners[lane] = new Runner();
            _smear[lane] = float.NaN;
            _boardStage[lane] = -1;
            _names[lane] = Short(match.Name(lane));
            if (_names[lane].Length > 9)
                _names[lane] = _names[lane][..9];

            // the dolls lean and fall by their own sprite rotation, so they keep their size at any angle
            AddDoll(CMUSports.Kit("Runner", CMUSports.Colour(match.Sport, lane, match.Entrants[lane])), 1f);
            DollView(lane).Stretch = SpriteView.StretchMode.None;
        }

        // the starter, in the officials' white
        AddDoll(CMUSports.Kit("Runner", 6), 1f);
        DollView(_lanes).Stretch = SpriteView.StretchMode.None;
    }

    #region The plan

    /// <summary>Reads the match's events into each lane's race: reactions, the false start, falls, the order home.</summary>
    private void Plan(CMUBetMatch match, CMUSportsPlay play)
    {
        _planned = (match.Id, match.Seed, match.Winner);
        _racing = true;
        _falseLane = -1;
        _falseAt = float.NaN;
        for (var lane = 0; lane < _lanes; lane++)
        {
            var score = lane < play.Score.Length ? play.Score[lane] : 1000;
            _runners[lane] = new Runner
            {
                Score = score,
                Finish = score / 100f,
                Base = score / 100f,
                Female = CMUSports.Female(match.Sport, match.Entrants[lane]),
            };
        }

        foreach (var ev in play.Events)
        {
            if (ev.Side < 0 || ev.Side >= _lanes)
                continue;

            var runner = _runners[ev.Side];
            var at = ev.Time - CMUSports.SprintStart;
            if (ev.Action == CMUSportsAction.Injury)
            {
                runner.Mishap = Mishap.Fall;
                runner.At = at;
                runner.Severity = Math.Clamp(ev.Extra, 1, 3);
                runner.Base = ev.State / 100f;
                continue;
            }

            if (ev.Action != CMUSportsAction.Special)
                continue;

            switch (ev.Value)
            {
                case CMUSports.SprintReaction:
                    runner.Reaction = ev.Extra / 1000f;
                    break;
                case CMUSports.SprintFalseStart:
                    _falseLane = ev.Side;
                    _falseAt = ev.Time;
                    break;
                case CMUSports.SprintHamstring:
                    runner.Mishap = Mishap.Hamstring;
                    runner.At = at;
                    runner.Base = ev.State / 100f;
                    break;
                case CMUSports.SprintCollision:
                    runner.Mishap = Mishap.Stagger;
                    runner.At = at;
                    runner.Base = ev.State / 100f;
                    runner.Into = ev.Extra;
                    break;
            }
        }

        foreach (var runner in _runners)
        {
            Settle(runner);
        }

        _winner = Math.Clamp(match.Winner, 0, _lanes - 1);
        _winAt = _runners[_winner].Finish;
        var second = float.MaxValue;
        for (var lane = 0; lane < _lanes; lane++)
        {
            var runner = _runners[lane];
            if (lane != _winner)
                second = MathF.Min(second, runner.Finish);

            runner.Rank = 1;
            for (var other = 0; other < _lanes; other++)
            {
                var them = _runners[other];
                if (them.Score < runner.Score || them.Score == runner.Score && other < lane)
                    runner.Rank++;
            }

            runner.Split = Reach(runner, 0.5f);
            _ranks[lane] = Loc.GetString("cmu-game-sprint-rank", ("place", runner.Rank));
        }

        _photoClose = second - _winAt <= CloseFinish;
        _record = _runners[_winner].Score <= RecordTime;

        // the crowd gets up when someone opens a gap, or anyway as the line comes
        _roarAt = _winAt - 1.2f;
        for (var run = 0.5f; run < _winAt; run += 0.05f)
        {
            float first = 0, next = 0;
            foreach (var runner in _runners)
            {
                var s = Progress(runner, run);
                if (s > first)
                {
                    next = first;
                    first = s;
                }
                else if (s > next)
                {
                    next = s;
                }
            }

            if (first >= 0.35f && first - next >= 0.045f)
            {
                _roarAt = run;
                break;
            }
        }

        for (var lane = 0; lane < _lanes; lane++)
        {
            _snap[lane] = StanceAt(lane, CMUSports.SprintStart + _winAt);
        }
    }

    /// <summary>Works out the phases of a runner's mishap so they get back up in time to finish at their score.</summary>
    private static void Settle(Runner runner)
    {
        if (runner.Mishap != Mishap.None)
        {
            var span = MathF.Max(1, runner.Base - runner.Reaction);
            var into = Math.Clamp((runner.At - runner.Reaction) / span, 0, 1);
            runner.AtS = Curve(into);
            runner.AtV = Slope(into) / span;
            switch (runner.Mishap)
            {
                case Mishap.Fall:
                {
                    runner.Slide = 1.2f + 0.2f * runner.Severity;
                    runner.RecoverS = runner.AtS + runner.AtV * (Stumble + 2 * runner.Slide / 3);
                    // no faster than a fresh sprinter once back up: lie there for what is left over
                    var need = (1 - runner.RecoverS) * span / 1.12f;
                    var down = runner.At + Stumble + runner.Slide;
                    var lie = Math.Clamp(runner.Finish - need - GetUp - down, 0.35f, 3f);
                    runner.RecoverAt = MathF.Min(down + lie + GetUp, runner.Finish - 0.5f);
                    runner.UpAt = runner.RecoverAt - GetUp;
                    break;
                }
                case Mishap.Hamstring:
                {
                    runner.RecoverAt = runner.At + 0.5f;
                    runner.RecoverS = runner.AtS + runner.AtV * 0.3375f;
                    var limp = MathF.Max(0.5f, runner.Finish - runner.RecoverAt);
                    var steps = Math.Max(1, (int) MathF.Round(limp / 0.42f));
                    runner.LimpPeriod = limp / steps;
                    runner.LimpSpeed = (1 - runner.RecoverS) / limp;
                    break;
                }
                case Mishap.Stagger:
                {
                    runner.RecoverAt = runner.At + 0.4f;
                    runner.RecoverS = runner.AtS + runner.AtV * 0.29f;
                    runner.RecoverV = 0.45f * runner.AtV;
                    var rest = 1 - runner.RecoverS;
                    var left = MathF.Max(0.5f, runner.Finish - runner.RecoverAt);
                    if (rest - runner.RecoverV * left < 0.1f * rest)
                        runner.RecoverV = 0.9f * rest / left;
                    break;
                }
            }
        }

        runner.EndSpeed = (FeetX(runner, runner.Finish - 0.001f) - FeetX(runner, runner.Finish - 0.051f)) / 0.05f;
    }

    /// <summary>The sprinter's distance curve: from nothing in the blocks to top speed, then holding it.</summary>
    private static float Curve(float u)
    {
        u = Math.Clamp(u, 0, 1);
        return (u - Accel * (1 - MathF.Exp(-u / Accel))) / CurveNorm;
    }

    private static float Slope(float u)
    {
        u = Math.Clamp(u, 0, 1);
        return (1 - MathF.Exp(-u / Accel)) / CurveNorm;
    }

    /// <summary>How far down the track a runner is, 0 to 1, <paramref name="run"/> seconds after the gun.</summary>
    private static float Progress(Runner r, float run)
    {
        if (run <= r.Reaction)
            return 0;

        if (run >= r.Finish)
            return 1;

        if (r.Mishap == Mishap.None || run < r.At)
            return Curve((run - r.Reaction) / MathF.Max(0.5f, r.Base - r.Reaction));

        var u = run - r.At;
        float s;
        switch (r.Mishap)
        {
            case Mishap.Fall:
                if (u < Stumble)
                {
                    s = r.AtS + r.AtV * u;
                }
                else if (u < Stumble + r.Slide)
                {
                    var w = u - Stumble;
                    s = r.AtS + r.AtV * (Stumble + w - w * w * w / (3 * r.Slide * r.Slide));
                }
                else if (run < r.RecoverAt)
                {
                    s = r.RecoverS;
                }
                else
                {
                    s = r.RecoverS + (1 - r.RecoverS) * Curve((run - r.RecoverAt) / MathF.Max(0.1f, r.Finish - r.RecoverAt));
                }

                break;
            case Mishap.Hamstring:
                if (run < r.RecoverAt)
                {
                    s = r.AtS + r.AtV * (u - 0.65f * u * u);
                }
                else
                {
                    var limp = run - r.RecoverAt;
                    s = r.RecoverS + r.LimpSpeed * (limp - 0.12f * r.LimpPeriod / MathF.Tau * MathF.Sin(MathF.Tau * limp / r.LimpPeriod));
                }

                break;
            default:
                if (run < r.RecoverAt)
                {
                    s = r.AtS + r.AtV * (u - 0.6875f * u * u);
                }
                else
                {
                    var w = run - r.RecoverAt;
                    var span = MathF.Max(0.1f, r.Finish - r.RecoverAt);
                    var rest = 1 - r.RecoverS;
                    s = r.RecoverS + r.RecoverV * w + (rest - r.RecoverV * span) * Curve(w / span);
                }

                break;
        }

        return Math.Clamp(s, 0, 1);
    }

    /// <summary>Where a runner's feet are, carrying on past the line and slowing down after it.</summary>
    private static float FeetX(Runner r, float run)
    {
        if (run >= r.Finish)
            return FinishFeet + r.EndSpeed / Brake * (1 - MathF.Exp(-Brake * (run - r.Finish)));

        return Blocks + Progress(r, run) * (FinishFeet - Blocks);
    }

    /// <summary>When a runner gets <paramref name="share"/> of the way down the track, after the gun.</summary>
    private static float Reach(Runner r, float share)
    {
        float low = r.Reaction, high = r.Finish;
        for (var i = 0; i < 24; i++)
        {
            var mid = (low + high) / 2;
            if (Progress(r, mid) >= share)
                high = mid;
            else
                low = mid;
        }

        return high;
    }

    #endregion

    #region Poses

    /// <summary>A lane's stance <paramref name="t"/> seconds from kickoff.</summary>
    private Stance StanceAt(int lane, float t)
    {
        var r = _runners[lane];
        var run = t - CMUSports.SprintStart;
        var stance = new Stance { Dir = Direction.East };
        if (run < r.Reaction)
        {
            Waiting(lane, t, ref stance);
            return stance;
        }

        var x = FeetX(r, run);
        stance.X = x;
        stance.Speed = (x - FeetX(r, run - 0.04f)) / 0.04f;
        var phase = x / Stride * MathF.PI;
        var pace = Math.Clamp(stance.Speed / 36f, 0, 1);
        var since = run - r.Reaction;
        stance.Lean = Lerp(58, 11, EaseOut(since / 1.6f)) + 3 * MathF.Sin(phase) * pace;
        stance.Lift = MathF.Abs(MathF.Sin(phase)) * 2.2f * pace;
        if (run >= r.Finish)
        {
            Finished(lane, run - r.Finish, ref stance);
            return stance;
        }

        // the dip for the line
        var left = r.Finish - run;
        if (left < 0.15f)
            stance.Lean += 24 * (1 - left / 0.15f);

        if (run < r.At - 0.3f || r.Mishap == Mishap.None)
            return stance;

        var u = run - r.At;
        switch (r.Mishap)
        {
            case Mishap.Fall when u >= 0:
                if (u < Stumble)
                {
                    var k = u / Stumble;
                    stance.Lean = 12 + 78 * k * k;
                    stance.Lift = MathF.Sin(k * MathF.PI) * 3;
                }
                else if (run < r.UpAt)
                {
                    stance.Lean = 90;
                    stance.Prone = true;
                    var w = u - Stumble;
                    stance.Lift = w < 0.2f ? MathF.Sin(w / 0.2f * MathF.PI) * 2.5f : 0;
                }
                else if (run < r.RecoverAt)
                {
                    var k = Smooth((run - r.UpAt) / GetUp);
                    stance.Lean = Lerp(90, 48, k);
                    stance.Lift = 0;
                    stance.Prone = k < 0.5f;
                }
                else
                {
                    stance.Lean = Lerp(48, 11, EaseOut((run - r.RecoverAt) / 1.1f)) + 3 * MathF.Sin(phase) * pace;
                }

                break;
            case Mishap.Hamstring when u >= 0:
                if (run < r.RecoverAt)
                {
                    stance.Lean = 11 + 22 * MathF.Sin(MathF.PI * u / 0.5f);
                    stance.Lift = 0;
                }
                else
                {
                    // a hop on the good leg, a shuffle on the bad one
                    var step = (run - r.RecoverAt) / r.LimpPeriod;
                    var hop = MathF.Abs(MathF.Sin(MathF.PI * step));
                    stance.Lean = 6 + 7 * hop;
                    stance.Lift = hop * ((int) step % 2 == 0 ? 3.5f : 1.2f);
                }

                break;
            case Mishap.Stagger:
                var sign = r.Into > lane ? 1 : -1;
                var drift = u < 0 ? Smooth((u + 0.3f) / 0.3f) : 1 - Smooth(u / 0.7f);
                stance.Swerve = sign * 13 * drift;
                if (u >= 0)
                    stance.Lean += 16 * MathF.Sin(u * 22) * MathF.Exp(-3 * u);

                break;
        }

        return stance;
    }

    /// <summary>Before the gun: behind the blocks, down on the marks, hips up at set, and the false start if there is one.</summary>
    private void Waiting(int lane, float t, ref Stance stance)
    {
        var bounce = MathF.Abs(MathF.Sin(t * 4 + lane * 1.7f)) * 1.4f;
        var stepIn = Smooth((t - 2.8f) / 0.4f);
        var x = Lerp(Behind, Blocks, stepIn);
        var lift = bounce * (1 - stepIn);
        var crouch = Smooth((t - 3.2f) / 0.5f) + Smooth((t - 4f) / 0.35f);
        var lean = float.NaN;
        if (!float.IsNaN(_falseAt) && t >= _falseAt)
        {
            var u = t - _falseAt;
            if (u < 1.3f)
            {
                if (lane == _falseLane)
                {
                    crouch = 0;
                    if (u < 0.6f)
                    {
                        // out of the blocks before the gun, then pulling up at the recall
                        x = Blocks + 16 * EaseOut(u / 0.45f);
                        lean = Lerp(50, 0, Smooth(u / 0.6f));
                        lift = 0;
                    }
                    else
                    {
                        var back = Smooth((u - 0.6f) / 0.7f);
                        x = Lerp(Blocks + 16, Behind, back);
                        lift = MathF.Abs(MathF.Sin(u * 10)) * 1.2f;
                        stance.Dir = back < 0.95f ? Direction.West : Direction.East;
                    }
                }
                else
                {
                    crouch = 2 * (1 - Smooth((u - 0.35f) / 0.3f));
                    x = Lerp(Blocks, Behind, Smooth((u - 0.45f) / 0.5f));
                    lift = 0;
                }
            }
            else
            {
                var again = Smooth((u - 1.3f) / 0.25f);
                x = Lerp(Behind, Blocks, again);
                lift = bounce * (1 - again);
                crouch = Smooth((u - 1.5f) / 0.3f) + Smooth((u - 1.9f) / 0.3f);
            }
        }

        if (float.IsNaN(lean))
        {
            if (crouch <= 1)
            {
                lean = 72 * crouch;
                lift = Lerp(lift, -3, crouch);
            }
            else
            {
                lean = 72 - 14 * (crouch - 1);
                lift = Lerp(-3, 0, crouch - 1);
            }

            // nerves in the set position
            if (crouch > 1.9f)
                lift += MathF.Sin(t * 47 + lane * 2.3f) * 0.35f;
        }

        stance.X = x;
        stance.Lean = lean;
        stance.Lift = lift;
    }

    /// <summary>Past the line: the winner jumps and turns to the crowd, the rest bend over, hands on knees.</summary>
    private void Finished(int lane, float u, ref Stance stance)
    {
        stance.Lean = MathF.Max(0, 35 * (1 - u / 0.35f));
        stance.Lift *= MathF.Max(0, 1 - u);
        if (lane == _winner)
        {
            if (u > 0.9f && u < 2.7f)
                stance.Lift = MathF.Abs(MathF.Sin((u - 0.9f) * 6.5f)) * 6;

            if (u >= 2.9f)
            {
                stance.Dir = Direction.South;
                stance.Lift = MathF.Abs(MathF.Sin(u * 3)) * 1.5f;
            }

            return;
        }

        if (u > 1.1f)
        {
            stance.Lean = 26 * Smooth((u - 1.1f) / 0.5f) + MathF.Sin(u * 5 + lane) * 1.2f;
            stance.Lift = 0;
        }
    }

    /// <summary>The doll's centre for a stance: the body pivots on its feet, so a lean tips it forward.</summary>
    private static Vector2 Centre(Stance stance, float ground)
    {
        var angle = stance.Lean * MathF.PI / 180;
        return new Vector2(stance.X + Body * MathF.Sin(angle), ground + stance.Swerve - Body * MathF.Cos(angle) - stance.Lift);
    }

    private static float GroundY(int lane)
    {
        return TrackTop + Lane * lane + Ground;
    }

    /// <summary>Tips a doll forward by its sprite's own rotation, which a directional sprite does not snap back.</summary>
    private void Tilt(int doll, float lean)
    {
        if (DollView(doll).Entity is { } entity)
            Sprites.SetRotation((entity.Owner, entity.Comp1), Angle.FromDegrees(-lean));
    }

    #endregion

    protected override void UpdateMatch(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        if (play != null && _planned != (match.Id, match.Seed, match.Winner))
            Plan(match, play);

        var delta = Math.Clamp((float) Timing.FrameTime.TotalSeconds, 0, 0.1f);
        _clock += delta;
        _time = t;
        if (float.IsNaN(_prev) || t < _prev)
            _prev = t;

        var racing = play != null && _racing;
        var run = t - CMUSports.SprintStart;
        var intro = (int) (_clock / 1.6f) % Math.Max(1, _lanes);
        for (var lane = 0; lane < _lanes; lane++)
        {
            Stance stance;
            if (racing)
            {
                stance = StanceAt(lane, t);
            }
            else
            {
                // the lineup, waiting for the off: each runner in turn waves to the camera
                stance = new Stance
                {
                    X = Behind,
                    Lift = MathF.Abs(MathF.Sin(_clock * 4 + lane * 1.7f)) * 1.4f,
                    Dir = lane == intro ? Direction.South : Direction.East,
                };
            }

            _stance[lane] = stance;
            var ground = GroundY(lane);
            var centre = Centre(stance, ground);
            _centre[lane] = centre;
            Pose(lane, centre.X, centre.Y, stance.Dir);
            Tilt(lane, stance.Lean);
            Say(match.Name(lane), 16, TrackTop + Lane * lane + 6, Color.White.WithAlpha(0.85f));
        }

        Pose(_lanes, StarterX, StarterY, Direction.East);
        Tilt(_lanes, 0);
        _excite = racing ? Excitement(t, run) : 0.3f + 0.05f * MathF.Sin(_clock * 0.7f);

        Calls(t, run, racing);
        Screen(match, t, run, racing, intro);
        Board(match, run, racing);
        if (racing)
        {
            Cues(t, run);
            Effects(run, delta);
            if (run >= 0 && run < _winAt)
                Clock = $"{run:0.00}";
            else if (run >= _winAt && t < CMUSports.Length(match.Sport))
                Clock = $"{_winAt:0.00}";

            for (var lane = 0; lane < _lanes; lane++)
            {
                var r = _runners[lane];
                if (run >= r.Finish + 0.3f)
                    Say(_ranks[lane], FinishLine - 4, TrackTop + Lane * lane + 2, RankColour(r.Rank), right: true);
            }
        }

        _photoAge = racing ? run - _winAt - 0.05f : -1;
        if (_photoAge >= 0 && _photoAge < PhotoShow)
            PhotoText(match);

        _prev = t;
    }

    #region Calls, screens and the board

    private void Calls(float t, float run, bool racing)
    {
        const float x = ArenaWidth / 2;
        const float y = 136;
        if (!racing)
            return;

        var falseStart = !float.IsNaN(_falseAt);
        if (t >= 0.3f && t < 3.9f)
            Say(Loc.GetString("cmu-sport-marks"), x, y, Color.White, true);
        else if (t >= 4f && t < 5.6f && (!falseStart || t < _falseAt))
            Say(Loc.GetString("cmu-sport-set"), x, y, Color.White, true);
        else if (falseStart && t >= _falseAt && t < _falseAt + 1.5f)
            Say(Loc.GetString("cmu-game-sprint-false-start"), x, y, Alarm, true);
        else if (falseStart && t >= _falseAt + 1.9f && t < MathF.Min(_falseAt + 3f, 7.6f))
            Say(Loc.GetString("cmu-sport-set"), x, y, Color.White, true);
        else if (run >= 0 && run < 0.7f)
            Say(Loc.GetString("cmu-sport-go"), x, y, Color.Lime, true);
    }

    /// <summary>The big screen in the stands: the field before the off, the race clock, the split, the winning time.</summary>
    private void Screen(CMUBetMatch match, float t, float run, bool racing, int intro)
    {
        const float middle = ScreenX + ScreenW / 2;
        const float big = ScreenY + 12;
        const float small = ScreenY + 24;
        if (!racing || run < 0)
        {
            Say(Loc.GetString("cmu-game-sprint-title"), middle, big, Amber, true);
            var lane = racing ? (int) (Math.Max(0, t) / 1.6f) % Math.Max(1, _lanes) : intro;
            Say(Loc.GetString("cmu-game-sprint-intro", ("lane", lane + 1), ("name", _names[lane])), ScreenX + 5, small, KitColour(match, lane));
            return;
        }

        if (run < _winAt)
        {
            Say($"{run:0.00}", middle, big, Amber, true);
            var leader = 0;
            var split = float.MaxValue;
            for (var lane = 0; lane < _lanes; lane++)
            {
                if (_stance[lane].X > _stance[leader].X)
                    leader = lane;

                split = MathF.Min(split, _runners[lane].Split);
            }

            if (run >= split && run < split + 2.5f)
                Say(Loc.GetString("cmu-game-sprint-split", ("time", $"{split:0.00}")), ScreenX + 5, small, Color.White);
            else
                Say(Loc.GetString("cmu-game-sprint-lead", ("name", _names[leader])), ScreenX + 5, small, KitColour(match, leader));

            return;
        }

        var blink = run - _winAt < 3 && _clock % 0.5f < 0.18f;
        if (!blink)
            Say($"{_winAt:0.00}", middle, big, Gold, true);

        var record = _record && _clock % 1.6f < 0.8f;
        Say(record ? Loc.GetString("cmu-game-sprint-record") : _names[_winner], ScreenX + 5, small,
            record ? Alarm : KitColour(match, _winner));
    }

    /// <summary>The split board in the infield: each lane's reaction, then its 50 m split, then its place and time.</summary>
    private void Board(CMUBetMatch match, float run, bool racing)
    {
        var length = CMUSports.Length(match.Sport);
        _boardAlpha = racing ? Math.Clamp((length - CMUSports.SprintStart - run) / 0.6f, 0, 1) : 1;
        if (_boardAlpha <= 0)
            return;

        for (var lane = 0; lane < _lanes; lane++)
        {
            var x = BoardX + lane * Column + 6;
            Say(_names[lane], x, BoardY + 2, KitColour(match, lane).WithAlpha(_boardAlpha));
            var r = _runners[lane];
            int stage;
            if (!racing || run < r.Reaction)
                stage = 0;
            else if (run >= r.Finish)
                stage = 4;
            else if (r.Mishap is Mishap.Fall or Mishap.Hamstring && run >= r.At && run < r.RecoverAt + 0.6f)
                stage = 3;
            else if (run >= r.Split)
                stage = 2;
            else
                stage = 1;

            if (stage != _boardStage[lane])
            {
                _boardStage[lane] = stage;
                _board[lane] = stage switch
                {
                    0 => Loc.GetString("cmu-game-sprint-lane", ("lane", lane + 1)),
                    1 => Loc.GetString("cmu-game-sprint-reaction", ("time", $"{r.Reaction:0.000}")),
                    2 => Loc.GetString("cmu-game-sprint-split", ("time", $"{r.Split:0.00}")),
                    3 => Loc.GetString(r.Mishap == Mishap.Fall ? "cmu-game-sprint-down" : "cmu-game-sprint-hurt"),
                    _ => Loc.GetString("cmu-game-sprint-place", ("place", r.Rank), ("time", $"{r.Finish:0.00}")),
                };
            }

            var colour = stage switch
            {
                3 => Alarm.WithAlpha(_clock % 0.6f < 0.4f ? 1 : 0.35f),
                4 => RankColour(r.Rank),
                0 => Color.White.WithAlpha(0.5f),
                _ => Color.White,
            };
            Say(_board[lane], x, BoardY + 17, colour.WithAlpha(colour.A * _boardAlpha));
        }
    }

    private void PhotoText(CMUBetMatch match)
    {
        Say(Loc.GetString(_photoClose ? "cmu-game-sprint-photo" : "cmu-game-sprint-finish-cam"), PhotoX + 6, PhotoY + 3,
            _photoClose ? Gold : Color.White);
        var row = (PhotoH - 22) / Math.Max(1, _lanes);
        for (var lane = 0; lane < _lanes; lane++)
        {
            var y = PhotoY + 20 + row * lane + row / 2 - 7;
            Say($"{lane + 1}", PhotoX + 5, y, KitColour(match, lane));
            var gap = _runners[lane].Finish - _winAt;
            if (lane == _winner)
                Say($"{_winAt:0.00}", PhotoX + 16, y, Gold);
            else if (gap < 1.5f)
                Say($"+{gap:0.00}", PhotoX + 16, y, Color.White);
        }
    }

    private static Color RankColour(int rank)
    {
        return rank switch
        {
            1 => Gold,
            2 => Silver,
            3 => Bronze,
            _ => Color.White,
        };
    }

    #endregion

    #region Sound and effects

    /// <summary>True once, on the frame a moment of the match passes while someone is watching.</summary>
    private bool Cue(float at)
    {
        return Live && _prev < at && at <= _time && _time - at < 0.5f;
    }

    private void Cues(float t, float run)
    {
        if (Cue(0.5f))
            Sound(Marks, -10);

        if (Cue(4f))
            Sound(Cock, -3);

        if (!float.IsNaN(_falseAt))
        {
            if (Cue(_falseAt + 0.3f))
                Shot(Recall);

            if (Cue(_falseAt + 0.5f))
                Shot(Gunshot);

            if (Cue(_falseAt + 1f) && _falseLane >= 0)
                Sound(_runners[_falseLane].Female ? "/Audio/Voice/Human/female_sigh.ogg" : "/Audio/Voice/Human/male_sigh.ogg", -2);

            if (Cue(_falseAt + 1.9f))
                Sound(Cock, -3);
        }

        if (Cue(CMUSports.SprintStart))
        {
            Shot(Gunshot);
            Shake(1.2f, 0.2f);
        }

        if (Cue(CMUSports.SprintStart + 0.5f))
            SoundAny(Claps, -8);

        if (Cue(CMUSports.SprintStart + _roarAt))
            Sound(CheerSmall, -2);

        var won = CMUSports.SprintStart + _winAt;
        if (Cue(won + 0.15f))
            SoundAny(Cheers, 0, 0);

        if (Cue(won + 0.3f))
            Sound(Camera, -2, 1.15f);

        if (Cue(won + 0.9f))
            SoundAny(Whistles, -5);

        if (_record && Cue(won + 0.8f))
            Pop(Loc.GetString("cmu-game-sprint-record"), new Vector2(ScreenX + ScreenW / 2, ScreenY + ScreenH + 14), Alarm, true, 1.6f, 10);

        for (var lane = 0; lane < _lanes; lane++)
        {
            var r = _runners[lane];
            var at = CMUSports.SprintStart + r.At;
            // the whoosh of the dip for the line
            if (Cue(CMUSports.SprintStart + r.Finish - 0.12f))
                SoundAny(Swooshes, -7);

            if (r.Mishap == Mishap.Fall)
            {
                if (Cue(at + Stumble))
                    Impact(lane, r);

                if (Cue(at + Stumble + 0.12f))
                    SoundAny(Swooshes, -1);

                if (Cue(at + 0.45f))
                    SoundAny(Gasps, -6);
            }
            else if (r.Mishap == Mishap.Hamstring && Cue(at + 0.35f))
            {
                SoundAny(Gasps, -6);
            }
        }
    }

    /// <summary>The pistol goes off: a crack, a flash at the muzzle and a curl of smoke.</summary>
    private void Shot(string sound)
    {
        Sound(sound);
        var (muzzle, _) = Gun(_time);
        Burst(muzzle, 10, Smoke.WithAlpha(0.55f), 22, 1.3f, 3.2f, -MathF.PI / 2, 1.3f, gravity: -30, drag: 1.4f);
        Burst(muzzle, 6, Color.FromHex("#FFE08A"), 90, 0.12f, 1.4f, -1.2f, 0.9f, streak: true);
    }

    /// <summary>The face meets the track: the thud, the spray, the camera shake.</summary>
    private void Impact(int lane, Runner r)
    {
        var ground = GroundY(lane);
        var head = Head(lane);
        Sound(Fall);
        Sound(Slap, -2);
        SoundAny(Splashes, -4);
        Bleed(head, ground + 1, r.Severity, -MathF.PI / 2 + 0.5f, 1.4f);
        Burst(new Vector2(head.X - 6, ground), 14, Dust, 60, 0.6f, 2.6f, MathF.PI + 0.4f, 1.6f, gravity: 140, floor: ground + 2);
        Shake(1.5f + r.Severity * 0.8f, 0.35f);
        Flash(Blood.WithAlpha(0.15f), 0.2f);
    }

    /// <summary>Where the head of a lane's doll is now, whichever way it leans.</summary>
    private Vector2 Head(int lane)
    {
        var angle = _stance[lane].Lean * MathF.PI / 180;
        return _centre[lane] + new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * 11;
    }

    private void Effects(float run, float delta)
    {
        var moving = 0;
        for (var lane = 0; lane < _lanes; lane++)
        {
            var r = _runners[lane];
            var stance = _stance[lane];
            var ground = GroundY(lane) + stance.Swerve;
            var pace = Math.Clamp(stance.Speed / 36f, 0, 1);
            if (stance.Speed > 12 && !stance.Prone)
                moving++;

            // dust off the spikes, thickest in the drive out of the blocks
            var since = run - r.Reaction;
            var drive = since >= 0 && since < 1.2f ? 1 - since / 1.2f : 0;
            _dust[lane] += delta * (pace * 9 + drive * 22) * (stance.Prone ? 0 : 1);
            while (_dust[lane] >= 1)
            {
                _dust[lane] -= 1;
                Burst(new Vector2(stance.X - 2, ground), 1, Dust.WithAlpha(0.8f), 40, 0.5f, 2.4f, MathF.PI + 0.45f, 0.9f,
                    gravity: 120, floor: ground + 2);
            }

            // sweat flicked off at speed
            _sweat[lane] += delta * pace * 1.6f;
            if (_sweat[lane] >= 1)
            {
                _sweat[lane] -= 1;
                Burst(Head(lane), 2, Sweat.WithAlpha(0.8f), 45, 0.45f, 1.3f, MathF.PI + 0.9f, 0.8f, gravity: 280, floor: ground);
            }

            if (r.Mishap != Mishap.Fall)
                continue;

            Smear(lane, r, run);

            // a bloody nose on the way home, worse the harder the fall
            if (run >= r.UpAt && run < r.Finish + 1)
            {
                _drip[lane] += delta * (1.5f + 1.5f * r.Severity);
                if (_drip[lane] >= 1)
                {
                    _drip[lane] -= 1;
                    var head = Head(lane);
                    Burst(head + new Vector2(3, 2), 1, Blood, 25, 1.2f, 1.6f + r.Severity * 0.3f, MathF.PI / 2, 1f,
                        gravity: 320, floor: ground, stains: true);
                }
            }
        }

        _step += delta * moving * 1.4f;
        if (_step >= 1)
        {
            _step = 0;
            SoundAny(Steps, -15, 0.15f);
        }

        // camera flashes popping in the stands when the crowd is up
        if (_excite > 0.75f && FxFloat() < delta * 14 * (_excite - 0.6f))
            CameraFlash();
    }

    private void CameraFlash()
    {
        var fan = Crowd[FxRandom.Next(Crowd.Length)];
        Burst(new Vector2(fan.X, fan.Y + 2), 1, Color.White, 0, 0.1f, 3.5f);
    }

    /// <summary>
    /// Paints the smear behind a face skidding down the track while it skids, then the pool where it stops. While
    /// catching up the whole lot is stamped at once.
    /// </summary>
    private void Smear(int lane, Runner r, float run)
    {
        if (float.IsNaN(_smear[lane]))
            return;

        var ground = GroundY(lane);
        var start = FeetX(r, r.At + Stumble) + 27;
        var end = FeetX(r, r.At + Stumble + r.Slide) + 27;
        var face = run >= r.At + Stumble + r.Slide ? end : FeetX(r, run) + 27;
        while (_smear[lane] <= face && _smear[lane] <= end)
        {
            StampSmear(r, start, end, _smear[lane], ground);
            _smear[lane] += 3.2f;
        }

        var lying = run - (r.At + Stumble + r.Slide);
        while (_pool[lane] < 3 && lying >= 0.1f + _pool[lane] * 0.4f)
        {
            Pool(lane, r, end, ground);
        }

        if (_pool[lane] >= 3 && _smear[lane] > end)
            _smear[lane] = float.NaN;
    }

    /// <summary>One step of the smear: the face's, and the knees' and hands' scrape behind it.</summary>
    private void StampSmear(Runner r, float start, float end, float x, float ground)
    {
        var along = Math.Clamp((x - start) / MathF.Max(1, end - start), 0, 1);
        Splat(new Vector2(x, ground - 1 + (FxFloat() - 0.5f) * 2), Lerp(8, 4, along) + r.Severity * 1.5f, FxFloat() < 0.3f ? DarkBlood : Blood);
        Splat(new Vector2(x - 19, ground + (FxFloat() - 0.5f) * 2), Lerp(5, 3, along) + r.Severity);
    }

    private void Pool(int lane, Runner r, float face, float ground)
    {
        var size = 11 + r.Severity * 3 - _pool[lane] * 2;
        Splat(new Vector2(face + (FxFloat() - 0.5f) * 5, ground - 1 + (FxFloat() - 0.5f) * 2), size, _pool[lane] == 0 ? Blood : DarkBlood);
        _pool[lane]++;
    }

    protected override void OnEvent(CMUBetMatch match, CMUSportsEvent ev, int index, bool live)
    {
        if (_planned != (match.Id, match.Seed, match.Winner) && match.Winner >= 0)
            Plan(match, CMUSports.Play(match.Sport, match.Seed, match.Entrants.Count, match.Winner));

        if (ev.Side < 0 || ev.Side >= _lanes)
            return;

        var lane = ev.Side;
        var r = _runners[lane];
        var ground = GroundY(lane);
        var head = new Vector2(FeetX(r, ev.Time - CMUSports.SprintStart) + 4, ground - 26);
        switch (ev.Action)
        {
            case CMUSportsAction.Injury:
            {
                var start = FeetX(r, r.At + Stumble) + 27;
                _smear[lane] = start;
                _pool[lane] = 0;
                if (!live)
                {
                    // nobody saw it go down, but the track did
                    var end = FeetX(r, r.At + Stumble + r.Slide) + 27;
                    for (var x = start; x <= end; x += 3.2f)
                    {
                        StampSmear(r, start, end, x, ground);
                    }

                    while (_pool[lane] < 3)
                    {
                        Pool(lane, r, end, ground);
                    }

                    _smear[lane] = float.NaN;
                    return;
                }

                Scream(r.Female);
                Pop(Loc.GetString("cmu-game-sprint-faceplant"), new Vector2(head.X + 14, ground - 12), Alarm, true, 1.1f, 18);
                break;
            }
            case CMUSportsAction.Special when ev.Value == CMUSports.SprintReaction && live:
                Burst(new Vector2(Blocks - 2, ground), 10, Dust.WithAlpha(0.85f), 70, 0.6f, 2.5f, MathF.PI + 0.3f, 1.2f,
                    gravity: 160, floor: ground + 2);
                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.SprintFalseStart && live:
                SoundAny(Gasps, -5);
                Shake(0.6f, 0.15f);
                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.SprintHamstring && live:
                SoundAny(Pops, 2, 0.05f);
                Scream(r.Female);
                Shake(1f, 0.2f);
                Pop(Loc.GetString("cmu-game-sprint-hamstring"), head, Color.FromHex("#FF9A3C"), false, 1.1f, 18);
                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.SprintCollision && live:
                Sound(Fall, -2);
                Shake(1.6f, 0.25f);
                Burst(head + new Vector2(6, 6), 8, Sweat.WithAlpha(0.8f), 60, 0.4f, 1.5f);
                Pop(Loc.GetString("cmu-game-sprint-tangled"), head + new Vector2(-20, -8), Color.Yellow, false, 1.1f, 18);
                break;
            case CMUSportsAction.Finish when live:
                Sound(Beam, -7);
                if (lane == match.Winner)
                    Won(match, lane, ground);

                break;
        }
    }

    /// <summary>The winner breaks the tape: the photo flash, streamers off the stands, the crowd's cameras.</summary>
    private void Won(CMUBetMatch match, int lane, float ground)
    {
        Sound(Camera);
        Flash(Color.White.WithAlpha(0.5f), 0.3f);
        Burst(new Vector2(FinishLine, ground - 16), 14, Color.White, 80, 0.5f, 1.6f, 0, 1.6f, gravity: 60);
        var kit = KitColour(match, lane);
        foreach (var colour in new[] { kit, Gold, Color.White })
        {
            Burst(new Vector2(FinishLine - 40, TrackTop - 8), 16, colour, 120, 1.6f, 2.2f, MathF.PI / 2, 2.6f, gravity: 90, drag: 1.2f);
            Burst(new Vector2(FinishLine + 30, TrackTop - 8), 12, colour, 110, 1.6f, 2.2f, MathF.PI / 2, 2.6f, gravity: 90, drag: 1.2f);
        }

        for (var i = 0; i < 10; i++)
        {
            CameraFlash();
        }
    }

    /// <summary>How loud the stands are: chatter, a hush for the set, up with the gun, on their feet at the end.</summary>
    private float Excitement(float t, float run)
    {
        float level;
        if (t < 4)
            level = 0.35f;
        else if (run < 0)
            level = !float.IsNaN(_falseAt) && t >= _falseAt && t < _falseAt + 1.2f ? 0.6f : 0.06f;
        else if (run < _roarAt)
            level = 0.5f + 0.2f * Math.Clamp(run / MathF.Max(1, _roarAt), 0, 1);
        else if (run < _winAt)
            level = 0.9f;
        else
            level = 1 - 0.4f * Math.Clamp((run - _winAt - 3) / 6, 0, 1);

        foreach (var r in _runners)
        {
            if (r.Mishap is Mishap.Fall or Mishap.Hamstring && run >= r.At && run < r.At + 1.5f)
                level = MathF.Max(level, 0.85f);
        }

        return level;
    }

    /// <summary>The starter's pistol: its muzzle and its angle, down at the hip until set, up at the sky for the gun.</summary>
    private (Vector2 Muzzle, float Rotation) Gun(float t)
    {
        var up = Smooth((t - 3.9f) / 0.25f) * (1 - Smooth((t - CMUSports.SprintStart - 1.6f) / 0.4f));
        var hand = new Vector2(StarterX + 6, StarterY + 3) + (new Vector2(StarterX + 7, StarterY - 15) - new Vector2(StarterX + 6, StarterY + 3)) * up;
        var rotation = Lerp(1f, -1.3f, up);
        // the barrel's end on the sprite, from its centre
        var muzzle = hand + Rotate(Barrel, rotation);
        return (muzzle, rotation);
    }

    #endregion

    #region Drawing

    protected override void DrawField(DrawingHandleScreen handle)
    {
        var lanes = Math.Max(1, _lanes);
        var bottom = TrackTop + Lane * lanes;
        DrawStands(handle);

        Rect(handle, 0, TrackTop, ArenaWidth, Lane * lanes, Track);
        foreach (var speck in Grain)
        {
            if (speck.Y < Lane * lanes)
                Rect(handle, speck.X, TrackTop + speck.Y, 2, 1, TrackGrain);
        }

        for (var i = 0; i <= lanes; i++)
        {
            Rect(handle, 0, TrackTop + Lane * i - 1, ArenaWidth, 2, Color.White.WithAlpha(0.8f));
        }

        for (var lane = 0; lane < lanes; lane++)
        {
            var top = TrackTop + Lane * lane;
            Rect(handle, 2, top + 7, 11, 13, Match != null ? KitColour(Match, lane) : Color.Gray);
            // the 50 m mark
            Rect(handle, (Blocks + FinishFeet) / 2 + 4, top + 1, 2, 5, Color.White.WithAlpha(0.7f));
        }

        Rect(handle, StartLine - 1, TrackTop, 3, Lane * lanes, Color.White);
        for (var y = 0; y < Lane * lanes / 5; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                Rect(handle, FinishLine + x * 5, TrackTop + y * 5, 5, 5, (x + y) % 2 == 0 ? Color.White : Color.Black);
            }
        }

        Rect(handle, 0, bottom + 1, ArenaWidth, ArenaHeight - bottom, Grass);
        for (var x = 0; x < ArenaWidth; x += 52)
        {
            Rect(handle, x, bottom + 1, 26, ArenaHeight - bottom, GrassStripe);
        }

        if (_boardAlpha > 0)
        {
            var width = Column * lanes;
            Rect(handle, BoardX - 3, BoardY - 2, width + 6, 35, Color.FromHex("#0B0D10").WithAlpha(0.95f * _boardAlpha));
            Frame(handle, BoardX - 3, BoardY - 2, width + 6, 35, Color.FromHex("#3A4250").WithAlpha(_boardAlpha));
            for (var lane = 0; lane < lanes && Match != null; lane++)
            {
                Rect(handle, BoardX + lane * Column + 1, BoardY + 2, 2, 27, KitColour(Match, lane).WithAlpha(_boardAlpha));
            }
        }
    }

    /// <summary>The dome's stands: lights, three rows of fans who stand and cheer with the race, flags, the screen, the boards.</summary>
    private void DrawStands(DrawingHandleScreen handle)
    {
        Rect(handle, 0, Bar, ArenaWidth, TrackTop - Bar, Color.FromHex("#171A22"));
        Rect(handle, 0, Bar, ArenaWidth, 6, Color.FromHex("#0E1015"));
        for (var i = 0; i < 6; i++)
        {
            var x = 40 + i * 88;
            Circle(handle, x, Bar + 3, 12, Color.FromHex("#FFF4C8").WithAlpha(0.06f));
            Rect(handle, x - 8, Bar + 1, 16, 4, Color.FromHex("#FFF4C8"));
        }

        if (Match is { } seated && (_crowd == null || _crowdFor != seated.Id))
            SeatFans(seated);

        for (var i = 0; i < Crowd.Length && _crowd != null; i++)
        {
            var fan = Crowd[i];
            var seat = _seats[i];
            var bob = MathF.Max(0, MathF.Sin(_clock * (4 + seat.Speed) + seat.Phase)) * 3f * _excite;
            var y = fan.Y - bob;
            var cellX = i % AtlasColumns * FanCell;
            var cellY = i / AtlasColumns * FanCell;
            handle.DrawTextureRectRegion(_crowd, Box(fan.X - FanCell / 2f, y - 3, FanCell, FanHeight),
                new UIBox2(cellX, cellY, cellX + FanCell, cellY + FanHeight));
            var wave = MathF.Sin(_clock * 9 + seat.Phase) * 1.5f;
            if (seat.Flag >= 0 && Match != null)
            {
                // a little flag in a lane's colour, waved harder as the crowd gets up
                var tip = new Vector2(fan.X + 5 + wave * _excite, y - 6);
                Line(handle, new Vector2(fan.X + 4, y + 5), tip, Color.FromHex("#8A8F99"));
                Rect(handle, tip.X, tip.Y + MathF.Sin(_clock * 12 + seat.Phase) * _excite, 4, 3,
                    Dim(KitColour(Match, seat.Flag), RowShade[fan.Row] + 0.15f));
            }

            if (_excite <= 0.55f || seat.Arms >= (_excite - 0.55f) * 2.2f)
                continue;

            Line(handle, new Vector2(fan.X - 3, y + 4), new Vector2(fan.X - 5 + wave, y - 4), seat.Skin);
            Line(handle, new Vector2(fan.X + 3, y + 4), new Vector2(fan.X + 5 + wave, y - 4), seat.Skin);
        }

        if (Match is { } match)
        {
            for (var i = 0; i < Flags.Length; i++)
            {
                var x = Flags[i];
                var colour = KitColour(match, i % Math.Max(1, _lanes));
                Line(handle, new Vector2(x, 62), new Vector2(x, 36), Color.FromHex("#9AA0A8"));
                for (var part = 0; part < 4; part++)
                {
                    var flutter = MathF.Sin(_clock * (5 + _excite * 4) + part * 0.9f + i) * (0.6f + part * 0.5f) * (0.4f + _excite);
                    Rect(handle, x + 1 + part * 3, 36 + flutter, 3, 7, colour);
                }
            }
        }

        // the screen
        Rect(handle, ScreenX - 3, ScreenY - 1, ScreenW + 6, ScreenH + 2, Color.FromHex("#2B2F38"));
        Rect(handle, ScreenX, ScreenY, ScreenW, ScreenH, Color.FromHex("#06080A"));
        for (var y = 0f; y < ScreenH; y += 2)
        {
            Rect(handle, ScreenX, ScreenY + y, ScreenW, 1, Color.FromHex("#1A2230").WithAlpha(0.35f));
        }

        // advertising boards along the front of the stands
        Rect(handle, 0, TrackTop - 7, ArenaWidth, 7, Color.FromHex("#0F2A44"));
        for (var i = 0; i < 8; i++)
        {
            var colour = i % 2 == 0 ? Color.FromHex("#1E5F8C") : Color.FromHex("#3E7D2A");
            Rect(handle, i * 65 + 2, TrackTop - 6, 61, 5, colour);
            Rect(handle, i * 65 + 8, TrackTop - 4, 20 + i * 7 % 24, 1, Color.White.WithAlpha(0.6f));
        }
    }

    protected override void DrawOverDecals(DrawingHandleScreen handle)
    {
        for (var lane = 0; lane < _lanes; lane++)
        {
            var ground = GroundY(lane);
            var stance = _stance[lane];
            // the blocks
            Rect(handle, Blocks - 6, ground - 4, 3, 5, Color.FromHex("#8A8F99"));
            Rect(handle, Blocks - 1, ground - 2, 3, 3, Color.FromHex("#8A8F99"));
            Rect(handle, Blocks - 7, ground, 11, 1, Color.FromHex("#4A4F59"));

            var shadow = Color.Black.WithAlpha(0.28f);
            var y = ground + stance.Swerve;
            if (stance.Prone)
                Rect(handle, stance.X - 2, y - 1, 32, 3, shadow);
            else
                Rect(handle, stance.X - 7 + stance.Lean * 0.12f, y - 1, 14 - stance.Lift, 3, shadow);

            // speed lines at full tilt
            var tilt = Math.Clamp((stance.Speed - 32) / 12, 0, 1);
            if (tilt <= 0 || stance.Prone || Match == null)
                continue;

            var kit = KitColour(Match, lane);
            var centre = _centre[lane];
            for (var i = 0; i < 3; i++)
            {
                var length = (10 + i % 2 * 8) * tilt;
                var line = new Vector2(centre.X - 8, centre.Y - 7 + i * 6);
                Line(handle, line - new Vector2(length, 0), line, (i == 1 ? Color.White : kit).WithAlpha(0.4f * tilt));
            }
        }

        DrawTape(handle);
    }

    /// <summary>The finish tape: straight across until the winner breaks it, then two halves blowing out from the posts.</summary>
    private void DrawTape(DrawingHandleScreen handle)
    {
        var top = new Vector2(FinishLine + 11, TrackTop - 3);
        var bottom = new Vector2(FinishLine + 11, TrackTop + Lane * Math.Max(1, _lanes) + 3);
        var tape = Color.White.WithAlpha(0.85f);
        var since = _racing ? _time - CMUSports.SprintStart - _winAt : -1;
        if (since < 0 || _lanes == 0)
        {
            Line(handle, top, bottom, tape);
            return;
        }

        var breakAt = GroundY(_winner) - 14;
        var fade = Math.Clamp(1 - (since - 6) / 2, 0, 1);
        if (fade <= 0)
            return;

        var blow = MathF.Min(1, since * 3);
        for (var half = 0; half < 2; half++)
        {
            var from = half == 0 ? top : bottom;
            var length = half == 0 ? breakAt - top.Y : bottom.Y - breakAt;
            var sign = half == 0 ? 1 : -1;
            var last = from;
            for (var i = 1; i <= 6; i++)
            {
                var k = i / 6f;
                var flutter = MathF.Sin(_clock * 11 + i * 1.3f + half) * 2.5f * k * MathF.Exp(-since * 0.5f);
                var point = from + new Vector2(blow * 16 * k * k + flutter, sign * length * k * (1 - 0.35f * blow));
                Line(handle, last, point, tape.WithAlpha(0.85f * fade));
                last = point;
            }
        }
    }

    protected override void DrawOverlay(DrawingHandleScreen handle)
    {
        // the finish posts with their timing eyes
        var bottom = TrackTop + Lane * Math.Max(1, _lanes);
        Rect(handle, FinishLine + 10, TrackTop - 8, 3, 8, Color.FromHex("#C9CED6"));
        Rect(handle, FinishLine + 10, bottom, 3, 8, Color.FromHex("#C9CED6"));
        var eye = Color.FromHex("#FF3030").WithAlpha(0.6f + 0.4f * MathF.Sin(_clock * 8));
        Rect(handle, FinishLine + 10, TrackTop - 6, 3, 2, eye);
        Rect(handle, FinishLine + 10, bottom + 2, 3, 2, eye);

        DrawGun(handle);

        if (_falseLane >= 0 && _time >= _falseAt + 0.6f && _boardAlpha > 0)
        {
            var x = BoardX + _falseLane * Column + Column - 10;
            Rect(handle, x, BoardY + 3, 6, 8, Color.FromHex("#F2D21B").WithAlpha(_boardAlpha));
        }

        if (_photoAge >= 0 && _photoAge < PhotoShow)
            DrawPhoto(handle);
    }

    private void DrawGun(DrawingHandleScreen handle)
    {
        var (muzzle, rotation) = Gun(_time);
        var centre = muzzle - Rotate(Barrel, rotation);
        Texture(handle, Sprite(Pistol, "icon"), centre, new Vector2(18, 18), rotation);
        if (!_racing)
            return;

        var shots = !float.IsNaN(_falseAt)
            ? MathF.Min(Since(_falseAt + 0.3f), MathF.Min(Since(_falseAt + 0.5f), Since(CMUSports.SprintStart)))
            : Since(CMUSports.SprintStart);
        if (shots < 0.08f)
        {
            Circle(handle, muzzle.X, muzzle.Y, 6, Color.FromHex("#FFC845").WithAlpha(0.7f));
            Circle(handle, muzzle.X, muzzle.Y, 3, Color.White);
        }
    }

    /// <summary>Seconds since a moment, or a long time if it is still to come.</summary>
    private float Since(float at)
    {
        return _time >= at ? _time - at : float.MaxValue;
    }

    /// <summary>The slit camera's frame of the moment the winner hit the line, every lane frozen where it was.</summary>
    private void DrawPhoto(DrawingHandleScreen handle)
    {
        if (Match is not { } match)
            return;

        Rect(handle, PhotoX + 3, PhotoY + 3, PhotoW, PhotoH, Color.Black.WithAlpha(0.35f));
        Rect(handle, PhotoX, PhotoY, PhotoW, PhotoH, Color.FromHex("#E7DFC8"));
        for (var x = 2f; x < PhotoW; x += 6)
        {
            Rect(handle, PhotoX + x, PhotoY + 18, 1, PhotoH - 18, Color.FromHex("#6B6152").WithAlpha(0.12f));
        }

        Rect(handle, PhotoX, PhotoY, PhotoW, 18, Color.FromHex("#1B1F27"));
        var row = (PhotoH - 22) / Math.Max(1, _lanes);
        var line = PhotoX + PhotoW - 44;
        for (var lane = 0; lane < _lanes; lane++)
        {
            var floor = PhotoY + 20 + row * (lane + 1) - 3;
            Rect(handle, PhotoX + 2, floor + 2, PhotoW - 4, 1, Color.FromHex("#8E846F").WithAlpha(0.5f));
            var snap = _snap[lane];
            var angle = snap.Lean * MathF.PI / 180;
            var chest = snap.X + Body * MathF.Sin(angle);
            var x = Math.Clamp(line + (chest - FinishLine) * 2.2f, PhotoX + 52, PhotoX + PhotoW - 6);
            Silhouette(handle, new Vector2(x - Body * 0.9f * MathF.Sin(angle), floor), snap.Lean, KitColour(match, lane), 0.9f);
        }

        Rect(handle, line, PhotoY + 18, 2, PhotoH - 18, Color.FromHex("#D02020"));
        Frame(handle, PhotoX, PhotoY, PhotoW, PhotoH, Color.White);
        var flash = 1 - _photoAge / 0.3f;
        if (flash > 0)
            Rect(handle, PhotoX, PhotoY, PhotoW, PhotoH, Color.White.WithAlpha(flash));
    }

    /// <summary>A runner's silhouette for the photo: legs, a leaning torso and a head, in the lane's colour.</summary>
    private void Silhouette(DrawingHandleScreen handle, Vector2 feet, float lean, Color colour, float scale)
    {
        var angle = lean * MathF.PI / 180;
        var up = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var side = new Vector2(-up.Y, up.X);
        var hip = feet + up * 7 * scale;
        var shoulder = feet + up * 14 * scale;
        var head = feet + up * 17.5f * scale;
        var dark = Color.FromHex("#2A241C");
        Line(handle, hip, feet + side * 3.5f * scale, dark);
        Line(handle, hip, feet - side * 3.5f * scale, dark);
        Poly(handle, colour, hip + side * 2 * scale, shoulder + side * 2 * scale, shoulder - side * 2 * scale, hip - side * 2 * scale);
        Line(handle, shoulder, shoulder + (up * -4 + side * 4) * scale, dark);
        Line(handle, shoulder, shoulder + (up * -3 - side * 4) * scale, dark);
        Circle(handle, head.X, head.Y, 2.6f * scale, dark);
    }

    #endregion

    #region Helpers

    private static Fan[] SeatCrowd()
    {
        var fans = new List<Fan>();
        var seed = 0x5EEDu;
        float Next()
        {
            seed = seed * 1664525u + 1013904223u;
            return (seed >> 8) / 16777216f;
        }

        for (var row = 0; row < 3; row++)
        {
            for (var x = 4f + row % 2 * 4; x < ArenaWidth; x += 8.5f + Next() * 2)
            {
                if (x > ScreenX - 6 && x < ScreenX + ScreenW + 6)
                    continue;

                fans.Add(new Fan(x, 41 + row * 8 + Next(), row));
            }
        }

        return fans.ToArray();
    }

    /// <summary>
    /// Seats this match's crowd: who sits where comes from the match id and the seat, so every client sees the same
    /// faces. Each fan is dressed once from the game's body, hair and clothing sprites into one small texture.
    /// </summary>
    private void SeatFans(CMUBetMatch match)
    {
        var rows = (Crowd.Length + AtlasColumns - 1) / AtlasColumns;
        using var image = new Image<Rgba32>(AtlasColumns * FanCell, rows * FanCell);
        for (var i = 0; i < Crowd.Length; i++)
        {
            var female = Roll(match.Id, i, 1) % 100 < 45;
            var skin = Skins[Roll(match.Id, i, 2) % Skins.Length];
            var outfit = Roll(match.Id, i, 3) % 100;
            var marine = outfit >= 68 && outfit < 84;
            var sex = female ? "_f" : "_m";
            Array.Clear(_canvas);
            Layer(Parts, "torso" + sex, skin);
            Layer(Parts, "head" + sex, skin);
            Layer(Eyes, "eyes", EyeColour);
            Layer(Parts, "r_arm", skin);
            Layer(Parts, "l_arm", skin);
            if (marine)
                Layer(MarineUniform, "equipped-INNERCLOTHING", Color.White);
            else if (outfit >= 50)
                Layer(ColourJumpsuit, "equipped-INNERCLOTHING", JumpsuitTints[Roll(match.Id, i, 4) % JumpsuitTints.Length]);
            else
                Layer(Clothing + "Uniforms/Civilian/" + Civilian[Roll(match.Id, i, 4) % Civilian.Length] + ".rsi", "equipped-INNERCLOTHING", Color.White);

            Layer(Parts, "l_hand", skin);
            Layer(Parts, "r_hand", skin);
            var styles = female ? FemaleHair : MaleHair;
            var style = marine ? (Roll(match.Id, i, 7) % 2 == 0 ? "buzzcut" : "crewcut") : styles[Roll(match.Id, i, 7) % styles.Length];
            Layer(Hair, style, HairColours[Roll(match.Id, i, 8) % HairColours.Length]);
            var hat = Roll(match.Id, i, 5) % 100;
            if (marine && hat < 65)
                Layer(MarineCap, "equipped-HELMET", Color.White);
            else if (!marine && hat < 22)
                Layer(Clothing + "Head/" + Hats[Roll(match.Id, i, 6) % Hats.Length] + ".rsi", "equipped-HELMET", Color.White);

            var shade = RowShade[Crowd[i].Row];
            Shrink(image.GetPixelSpan(), image.Width, i % AtlasColumns * FanCell, i / AtlasColumns * FanCell, shade);
            _seats[i] = new Seat
            {
                Skin = Dim(skin, shade),
                Phase = (Roll(match.Id, i, 11) & 0xFFFF) / 65535f * MathF.Tau,
                Speed = (Roll(match.Id, i, 12) & 0xFFFF) / 65535f * 3,
                Arms = (Roll(match.Id, i, 13) & 0xFFFF) / 65535f,
                Flag = Roll(match.Id, i, 9) % 100 < 10 ? (int) (Roll(match.Id, i, 10) % (uint) Math.Max(1, _lanes)) : -1,
            };
        }

        _crowd?.Dispose();
        _crowd = _clyde.LoadTextureFromImage(image, "cmu-sprint-crowd");
        _crowdFor = match.Id;
    }

    /// <summary>Lays a tinted sprite layer over the fan being dressed, the way a sprite's layers multiply their colour.</summary>
    private void Layer(string rsi, string state, Color tint)
    {
        if (SouthFrame(rsi, state) is not { } frame)
            return;

        for (var p = 0; p < frame.Length; p++)
        {
            var src = frame[p];
            if (src.A == 0)
                continue;

            var a = src.A / 255f * tint.A;
            var o = p * 4;
            var keep = 1 - a;
            _canvas[o] = src.R / 255f * tint.R * a + _canvas[o] * keep;
            _canvas[o + 1] = src.G / 255f * tint.G * a + _canvas[o + 1] * keep;
            _canvas[o + 2] = src.B / 255f * tint.B * a + _canvas[o + 2] * keep;
            _canvas[o + 3] = a + _canvas[o + 3] * keep;
        }
    }

    /// <summary>Halves the dressed fan into its atlas cell, averaging each 2 by 2, darkened for its row.</summary>
    private void Shrink(Span<Rgba32> atlas, int width, int cellX, int cellY, float shade)
    {
        for (var y = 0; y < FanCell; y++)
        {
            for (var x = 0; x < FanCell; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (var k = 0; k < 4; k++)
                {
                    var o = ((y * 2 + k / 2) * 32 + x * 2 + k % 2) * 4;
                    r += _canvas[o];
                    g += _canvas[o + 1];
                    b += _canvas[o + 2];
                    a += _canvas[o + 3];
                }

                var pixel = default(Rgba32);
                if (a > 0)
                {
                    var scale = shade / a * 255;
                    pixel = new Rgba32((byte) MathF.Min(255, r * scale), (byte) MathF.Min(255, g * scale),
                        (byte) MathF.Min(255, b * scale), (byte) MathF.Min(255, a / 4 * 255));
                }

                atlas[(cellY + y) * width + cellX + x] = pixel;
            }
        }
    }

    /// <summary>A sprite state's south-facing first frame (the top left of its sheet), or null if it cannot be read.</summary>
    private Rgba32[]? SouthFrame(string rsi, string state)
    {
        var key = rsi + "/" + state;
        if (SouthFrames.TryGetValue(key, out var frame))
            return frame;

        using (var stream = Cache.ContentFileReadOrNull(new ResPath(key + ".png")))
        {
            if (stream != null)
            {
                using var sheet = Image.Load<Rgba32>(stream);
                if (sheet.Width >= 32 && sheet.Height >= 32)
                {
                    var pixels = sheet.GetPixelSpan();
                    frame = new Rgba32[32 * 32];
                    for (var y = 0; y < 32; y++)
                    {
                        pixels.Slice(y * sheet.Width, 32).CopyTo(frame.AsSpan(y * 32, 32));
                    }
                }
            }
        }

        SouthFrames[key] = frame;
        return frame;
    }

    /// <summary>A steady pseudo-random number for a seat of a match, the same in the JS port.</summary>
    private static uint Roll(int match, int seat, int salt)
    {
        var h = unchecked((uint) (match * 73856093 ^ seat * 19349663 ^ salt * 83492791) * 0x9E3779B1u);
        h ^= h >> 15;
        h = unchecked(h * 0x85EBCA77u);
        h ^= h >> 13;
        h = unchecked(h * 0xC2B2AE3Du);
        h ^= h >> 16;
        return h;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _crowd?.Dispose();
        _crowd = null;
    }

    private static Vector2[] SpreadGrain()
    {
        var grain = new Vector2[90];
        var seed = 0x7A11u;
        for (var i = 0; i < grain.Length; i++)
        {
            seed = seed * 1664525u + 1013904223u;
            var x = (seed >> 8) % (uint) ArenaWidth;
            seed = seed * 1664525u + 1013904223u;
            var y = (seed >> 8) % 130u;
            grain[i] = new Vector2(x, y);
        }

        return grain;
    }

    private static Color Dim(Color colour, float shade)
    {
        return new Color(colour.R * shade, colour.G * shade, colour.B * shade, colour.A);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static float Lerp(float a, float b, float k)
    {
        return a + (b - a) * k;
    }

    private static float Smooth(float k)
    {
        k = Math.Clamp(k, 0, 1);
        return k * k * (3 - 2 * k);
    }

    private static float EaseOut(float k)
    {
        k = Math.Clamp(k, 0, 1);
        return 1 - (1 - k) * (1 - k);
    }

    #endregion
}
