using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Zero-g ball: three a side tumbling in a glass-fronted cage against the stars. Players drift between push-offs with
/// thruster puffs, perch on the cage walls, carry the ball and shoot it down a trail into the net. Body checks crack
/// visors and leave blood floating as globules that splat on the walls and the glass; the live cage wall shocks
/// anyone who drifts into it. Everything that moves is worked out from the match time, so a late joiner sees the
/// same match, and the marks left by what they missed.
/// </summary>
public sealed class CMUZeroGArena : CMUSportsArena
{
    private static readonly EntProtoId BallProto = "Basketball";

    private const int Dolls = CMUSports.Squad * 2;
    private const int Keeper = 2;
    private const float CageLeft = 16;
    private const float CageRight = ArenaWidth - 16;
    private const float CageTop = Bar + 8;
    private const float CageBottom = ArenaHeight - 8;
    private const float GoalY = (CageTop + CageBottom) / 2;
    private const float GoalHalf = 34;

    /// <summary>How far from a wall a player perched on it stands, centre to rail.</summary>
    private const float Perch = 18;

    private const float Kickoff = 2;
    private const float PassSpeed = 420;
    private const float KnockTime = 0.8f;

    /// <summary>How long before a shock the cage wall arms and crackles.</summary>
    private const float ArmTime = 0.9f;

    private const int MaxGlobules = 140;
    private const int MaxGlass = 48;

    /// <summary>Each role's reach along the cage, from its own goal (0) to the one it attacks (1): striker, mid, back.</summary>
    private static readonly float[] RoleNear = { 0.48f, 0.26f, 0.05f };
    private static readonly float[] RoleFar = { 0.88f, 0.68f, 0.3f };
    private static readonly float[] WallChance = { 0.24f, 0.24f, 0.1f };
    private static readonly Vector2[] Lineup = { new(0.4f, -46), new(0.28f, 42), new(0.1f, 0) };

    private static readonly Vector2 Centre = new(ArenaWidth / 2, GoalY);

    /// <summary>The hatch in the cage roof that serves the ball after a goal.</summary>
    private static readonly Vector2 Serve = new(ArenaWidth / 2, CageTop + 8);

    private static readonly Color CageColour = Color.FromHex("#3FD8FF");
    private static readonly Color Rail = Color.FromHex("#1A2433");
    private static readonly Color Hazard = Color.FromHex("#D8B020");
    private static readonly Color Spark = Color.FromHex("#FFF6A0");
    private static readonly Color Arc = Color.FromHex("#8FE6FF");
    private static readonly Color Puff = Color.FromHex("#DDE6F0").WithAlpha(0.55f);
    private static readonly Color Scorch = Color.FromHex("#1A1612").WithAlpha(0.8f);
    private static readonly Color Shard = Color.FromHex("#D8F6FF");
    private static readonly Color Lit = Color.FromHex("#9FE8FF");
    private static readonly Color Charred = Color.FromHex("#303848");
    private static readonly Color Singed = Color.FromHex("#8A8A8A");

    private static readonly string[] Swooshes =
    {
        "/Audio/CMU14/PersonalComputers/Games/swoosh_1.ogg", "/Audio/CMU14/PersonalComputers/Games/swoosh_2.ogg",
        "/Audio/CMU14/PersonalComputers/Games/swoosh_3.ogg",
    };

    private static readonly string[] Punches =
    {
        "/Audio/CMU14/PersonalComputers/Games/punch_1.ogg", "/Audio/CMU14/PersonalComputers/Games/punch_2.ogg",
    };

    private static readonly string[] Cracks =
    {
        "/Audio/Effects/glass_crack1.ogg", "/Audio/Effects/glass_crack2.ogg", "/Audio/Effects/glass_crack3.ogg",
        "/Audio/Effects/glass_crack4.ogg",
    };

    private static readonly string[] Gore =
    {
        "/Audio/Weapons/Guns/Hits/bullet_meat1.ogg", "/Audio/Weapons/Guns/Hits/bullet_meat2.ogg",
        "/Audio/Weapons/Guns/Hits/bullet_meat3.ogg", "/Audio/Weapons/Guns/Hits/bullet_meat4.ogg",
    };

    private static readonly string[] Sparks =
    {
        "/Audio/Effects/sparks1.ogg", "/Audio/Effects/sparks2.ogg", "/Audio/Effects/sparks3.ogg", "/Audio/Effects/sparks4.ogg",
    };

    private static readonly string[] Thuds =
    {
        "/Audio/Effects/metal_thud1.ogg", "/Audio/Effects/metal_thud2.ogg", "/Audio/Effects/metal_thud3.ogg",
    };

    private static readonly string[] Rattles =
    {
        "/Audio/Effects/fence_rattle1.ogg", "/Audio/Effects/fence_rattle2.ogg", "/Audio/Effects/fence_rattle3.ogg",
    };

    /// <summary>The big crowd cheers run for about fifteen seconds, so only one plays at a time.</summary>
    private static readonly string[] Cheers =
    {
        "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_1.ogg", "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_2.ogg",
    };

    private const string SmallCheer = "/Audio/CMU14/PersonalComputers/Games/crowd_cheer_small.ogg";

    /// <summary>How long a big cheer is left to run before another may start, in seconds.</summary>
    private const float CheerLength = 13;

    private static readonly string[] Splats =
    {
        "/Audio/Effects/Fluids/blood1.ogg", "/Audio/Effects/Fluids/blood2.ogg",
    };

    private static readonly string[] Digits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    private enum BallKind : byte
    {
        Hover,
        Held,
        Flight,
        Shot,
        Net,
        Loose,
        Gone,
    }

    private enum CueKind : byte
    {
        Catch,
        Post,
        Save,
        Arm,
        Final,
    }

    /// <summary>Draws a player towards a spot: in from <see cref="Start"/>, all the way from Peak to Release, back out by End.</summary>
    private readonly record struct Pull(float Start, float Peak, float Release, float End, Vector2 Target);

    /// <summary>A hit that sends a player drifting off with a spin, until their thrusters steady them.</summary>
    private readonly record struct Knock(float Time, Vector2 Push, float Spin, bool Shock);

    /// <summary>What the ball does from Start: sits in a hand, flies from From to To until End, drifts loose, rests in the net.</summary>
    private readonly record struct Leg(BallKind Kind, float Start, float End, Vector2 From, Vector2 To, Vector2 Velocity, int Holder);

    /// <summary>A sound or effect between events: a catch, a shot reaching the goal mouth, the cage wall arming.</summary>
    private readonly record struct Cue(float Time, CueKind Kind, Vector2 At, int Side);

    private readonly record struct Hit(int Index, float Time, int Doll, int Severity, Vector2 Contact, Vector2 Direction);

    private readonly record struct Zap(int Index, float Time, int Doll, Vector2 Contact, bool Top);

    private readonly record struct Glass(Vector2 Position, float Size, int State, float Rotation, Color Colour);

    /// <summary>A drop of blood with nothing to pull it down: it drifts, wobbles, and bursts on whatever it reaches.</summary>
    private struct Globule
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Phase;
        public float Age;
        public float Life;

        /// <summary>0 among the players, 1 on the glass in front, -1 on the cage's back mesh.</summary>
        public float Depth;

        public float DepthSpeed;
        public Color Colour;
    }

    private EntityPrototypeView _ball = default!;
    private CMUSportsPlay? _play;
    private int _id;
    private readonly string[] _names = { string.Empty, string.Empty };

    private readonly List<(float Time, int Side)> _turnovers = new();
    private readonly List<Pull>[] _pulls = new List<Pull>[Dolls];
    private readonly List<Knock>[] _knocks = new List<Knock>[Dolls];
    private readonly List<Leg> _legs = new();
    private readonly List<Cue> _cues = new();
    private readonly List<Hit> _hits = new();
    private readonly List<Zap> _zaps = new();
    private readonly List<(float Time, int Side)> _goals = new();

    private readonly Vector2[] _pos = new Vector2[Dolls];
    private readonly float[] _rot = new float[Dolls];
    private readonly int[] _segment = new int[Dolls];
    private readonly bool[] _braked = new bool[Dolls];
    private readonly int[] _crack = new int[Dolls];
    private readonly List<Globule> _globules = new();
    private readonly List<Glass> _glass = new();
    private readonly Vector2[] _bolt = new Vector2[7];
    private Texture?[]? _splatter;

    private float _t = -1;
    private float _lastT = float.NaN;
    private float _real;
    private float _delta;
    private float _after;
    private float _spin;
    private float _cheered = float.NegativeInfinity;
    private int _lastIndex = -1;
    private Vector2 _ballAt = Centre;
    private int _ballLeg = -1;
    private int _bounceLeg = -1;
    private int _bounces;
    private readonly int[] _score = new int[2];
    private int _fresh = -1;

    public CMUZeroGArena()
    {
        for (var doll = 0; doll < Dolls; doll++)
        {
            _pulls[doll] = new List<Pull>();
            _knocks[doll] = new List<Knock>();
        }
    }

    protected override void Build(CMUBetMatch match)
    {
        Prepare(match);
        for (var side = 0; side < 2; side++)
        {
            _names[side] = Short(match.Name(side));
            for (var i = 0; i < CMUSports.Squad; i++)
            {
                AddDoll(CMUSports.Kit("Spacer", CMUSports.Colour(match.Sport, side, match.Entrants[side])), 1.25f);
            }
        }

        _ball = AddItem(BallProto, 24);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        _delta = args.DeltaSeconds;
        _real += _delta;
        base.FrameUpdate(args);
    }

    #region The match, worked out

    /// <summary>
    /// Works out how everyone and the ball move for the whole match, once, from the same play every client sees:
    /// first who gets drawn where and knocked about, then the ball's path through their hands.
    /// </summary>
    private void Prepare(CMUBetMatch match)
    {
        _id = match.Id;
        _turnovers.Clear();
        _legs.Clear();
        _cues.Clear();
        _hits.Clear();
        _zaps.Clear();
        _goals.Clear();
        for (var doll = 0; doll < Dolls; doll++)
        {
            _pulls[doll].Clear();
            _knocks[doll].Clear();
            _segment[doll] = int.MinValue;
            _braked[doll] = false;
            _crack[doll] = 0;
        }

        ResetFloor();
        _lastIndex = -1;
        _lastT = float.NaN;
        _after = 0;
        _ballLeg = -1;
        _bounceLeg = -1;
        _play = match.Winner >= 0 ? CMUSports.Play(match.Sport, match.Seed, match.Entrants.Count, match.Winner) : null;
        if (_play == null)
            return;

        var events = _play.Events;
        var holding = -1;
        foreach (var ev in events)
        {
            if (ev.Action == CMUSportsAction.Pass && ev.Side != holding)
            {
                _turnovers.Add((ev.Time, ev.Side));
                holding = ev.Side;
            }
            else if (ev.Action == CMUSportsAction.Goal)
            {
                _goals.Add((ev.Time, ev.Side));
            }
        }

        var passed = 0f;
        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.Action == CMUSportsAction.Pass)
                passed = ev.Time;
            else if (ev.Action == CMUSportsAction.Injury)
                PrepareHit(i, ev);
            else if (ev.Action == CMUSportsAction.Special && ev.Value == CMUSports.ZeroGShock)
                PrepareZap(i, ev);
            else if (ev.Action == CMUSportsAction.Special && ev.Value == CMUSports.ZeroGShot)
                PrepareShot(i, ev, passed);
        }

        AddLeg(new Leg(BallKind.Hover, float.NegativeInfinity, 0, Centre, Centre, default, -1));
        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.Action == CMUSportsAction.Pass)
                PreparePass(ev);
            else if (ev.Action == CMUSportsAction.Special && ev.Value == CMUSports.ZeroGShot)
                PrepareShotBall(i, ev);
            else if (ev.Action == CMUSportsAction.Injury)
                KnockLoose(ev.Time, ev.Side * CMUSports.Squad + Squad(ev.Value), HitAt(i).Direction, i);
            else if (ev.Action == CMUSportsAction.Special && ev.Value == CMUSports.ZeroGShock)
                KnockLoose(ev.Time, ev.Side * CMUSports.Squad + Squad(ev.Extra), new Vector2(0, ev.State == 0 ? 1 : -1), i);
        }

        _cues.Add(new Cue(CMUSports.Length(match.Sport), CueKind.Final, Centre, match.Winner));
        _cues.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>The opponent closes in, they collide, and both drift apart spinning.</summary>
    private void PrepareHit(int index, CMUSportsEvent ev)
    {
        var victim = ev.Side * CMUSports.Squad + Squad(ev.Value);
        var attacker = (1 - ev.Side) * CMUSports.Squad + Squad(ev.State);
        var severity = Math.Clamp(ev.Extra, 1, 3);
        var target = Player(victim, ev.Time, out _);
        var start = Player(attacker, ev.Time - 0.7f, out _);
        var direction = target - start;
        direction = direction.LengthSquared() > 1 ? Vector2.Normalize(direction) : new Vector2(ev.Side == 0 ? -1 : 1, 0);
        var spin = Hash(_id, index, 5) < 0.5f ? 1 : -1;
        _pulls[attacker].Add(new Pull(ev.Time - 0.7f, ev.Time, ev.Time, ev.Time + 0.25f, target - direction * 14));
        _knocks[victim].Add(new Knock(ev.Time, direction * (26 + severity * 14), spin * 360 * (severity == 3 ? 2 : 1), false));
        _knocks[attacker].Add(new Knock(ev.Time, -direction * 16, -spin * 360, false));
        _hits.Add(new Hit(index, ev.Time, victim, severity, target - direction * 7, direction));
    }

    /// <summary>A player drifts onto the live wall, gets thrown off it twitching, and the wall keeps the scorch.</summary>
    private void PrepareZap(int index, CMUSportsEvent ev)
    {
        var victim = ev.Side * CMUSports.Squad + Squad(ev.Extra);
        var top = ev.State == 0;
        var near = Player(victim, ev.Time - 1, out _);
        var contact = new Vector2(Math.Clamp(near.X, CageLeft + 60, CageRight - 60), top ? CageTop : CageBottom);
        var spot = contact with { Y = top ? CageTop + 16 : CageBottom - 16 };
        _pulls[victim].Add(new Pull(ev.Time - 1, ev.Time, ev.Time, ev.Time + 0.2f, spot));
        var spin = Hash(_id, index, 7) < 0.5f ? 720 : -720;
        _knocks[victim].Add(new Knock(ev.Time, new Vector2((Hash(_id, index, 6) - 0.5f) * 40, top ? 62 : -62), spin, true));
        _zaps.Add(new Zap(index, ev.Time, victim, contact, top));
        _cues.Add(new Cue(ev.Time - ArmTime, CueKind.Arm, contact, ev.Side));
    }

    /// <summary>The shooter thrusts into a shooting spot; on a save the keeper dives across the goal mouth.</summary>
    private void PrepareShot(int index, CMUSportsEvent ev, float passed)
    {
        var shooter = ev.Side * CMUSports.Squad + Squad(ev.Extra);
        var forward = ev.Side == 0 ? 1 : -1;
        Vector2 spot;
        if (ev.State == CMUSports.ZeroGScored)
        {
            spot = Screen(ev.Side, 0.7f, GoalY + (Hash(_id, index, 8) - 0.5f) * 70);
        }
        else
        {
            var at = Player(shooter, passed, out _);
            spot = at + new Vector2(forward * 45, (GoalY - at.Y) * 0.3f);
        }

        _pulls[shooter].Add(new Pull(Math.Min(passed, ev.Time - 0.3f), ev.Time, ev.Time + 0.05f, ev.Time + 1.4f, spot));
        if (ev.State != CMUSports.ZeroGSaved)
            return;

        var keeper = (1 - ev.Side) * CMUSports.Squad + Keeper;
        var goal = GoalMouth(ev.Side);
        var dive = new Vector2(goal.X - forward * 30, GoalY + (Hash(_id, index, 9) - 0.5f) * 44);
        var arrive = ev.Time + CMUSports.ZeroGShotFlight;
        _pulls[keeper].Add(new Pull(ev.Time - 0.2f, arrive, arrive + 0.2f, arrive + 1.6f, dive));
    }

    private void PreparePass(CMUSportsEvent ev)
    {
        var from = Ball(ev.Time, out var current);
        if (_legs[current].Kind is BallKind.Net or BallKind.Gone)
            from = Serve;

        var receiver = ev.Side * CMUSports.Squad + Squad(ev.Value);
        var flight = Math.Clamp(Vector2.Distance(from, Hand(receiver, ev.Time)) / PassSpeed, 0.22f, 0.5f);
        var to = Hand(receiver, ev.Time + flight);
        AddLeg(new Leg(BallKind.Flight, ev.Time, ev.Time + flight, from, to, default, receiver));
        AddLeg(new Leg(BallKind.Held, ev.Time + flight, float.PositiveInfinity, to, to, default, receiver));
        _cues.Add(new Cue(ev.Time + flight, CueKind.Catch, to, ev.Side));
    }

    private void PrepareShotBall(int index, CMUSportsEvent ev)
    {
        var from = Ball(ev.Time, out _);
        var goal = GoalMouth(ev.Side);
        var inward = ev.Side == 0 ? -1 : 1;
        var end = ev.Time + CMUSports.ZeroGShotFlight;
        Vector2 to;
        switch (ev.State)
        {
            case CMUSports.ZeroGPost:
                var high = Hash(_id, index, 11) < 0.5f;
                to = new Vector2(goal.X + inward * 3, GoalY + (high ? -GoalHalf : GoalHalf));
                AddLeg(new Leg(BallKind.Shot, ev.Time, end, from, to, default, ev.Side));
                AddLeg(new Leg(BallKind.Loose, end, float.PositiveInfinity, to, to, new Vector2(inward * 140, high ? -70 : 70), -1));
                _cues.Add(new Cue(end, CueKind.Post, to, ev.Side));
                break;
            case CMUSports.ZeroGSaved:
                to = Player((1 - ev.Side) * CMUSports.Squad + Keeper, end, out _);
                AddLeg(new Leg(BallKind.Shot, ev.Time, end, from, to, default, ev.Side));
                AddLeg(new Leg(BallKind.Loose, end, float.PositiveInfinity, to, to,
                    new Vector2(inward * 150, (Hash(_id, index, 12) - 0.5f) * 180), -1));
                _cues.Add(new Cue(end, CueKind.Save, to, ev.Side));
                break;
            default:
                to = new Vector2(goal.X - inward * 5, GoalY + (Hash(_id, index, 10) - 0.5f) * 44);
                AddLeg(new Leg(BallKind.Shot, ev.Time, end, from, to, default, ev.Side));
                AddLeg(new Leg(BallKind.Net, end, end + 1.4f, to, to, default, -1));
                AddLeg(new Leg(BallKind.Gone, end + 1.4f, float.PositiveInfinity, to, to, default, -1));
                break;
        }
    }

    /// <summary>A hit player lets go of the ball, which drifts off until someone picks it up.</summary>
    private void KnockLoose(float time, int doll, Vector2 direction, int index)
    {
        var leg = _legs[LegAt(time)];
        if (leg.Kind != BallKind.Held || leg.Holder != doll)
            return;

        var from = Ball(time, out _);
        var velocity = Turn(direction, (Hash(_id, index, 13) - 0.5f) * 1.8f) * 130;
        AddLeg(new Leg(BallKind.Loose, time, float.PositiveInfinity, from, from, velocity, -1));
    }

    /// <summary>Legs stay in time order, so the ball's leg at any moment can be found by halving.</summary>
    private void AddLeg(Leg leg)
    {
        if (_legs.Count > 0 && leg.Start < _legs[^1].Start)
            leg = leg with { Start = _legs[^1].Start };

        _legs.Add(leg);
    }

    private Hit HitAt(int index)
    {
        foreach (var hit in _hits)
        {
            if (hit.Index == index)
                return hit;
        }

        return default;
    }

    private static int Squad(int player)
    {
        return Math.Clamp(player, 0, CMUSports.Squad - 1);
    }

    #endregion

    #region Motion

    /// <summary>Where a player is, and how far turned in degrees, at a moment of the match.</summary>
    private Vector2 Player(int doll, float t, out float rotation)
    {
        if (_play == null)
        {
            rotation = MathF.Sin(_real * 0.9f + doll) * 6;
            return Formation(doll, _real);
        }

        var at = Path(doll, t, out rotation, out _, out _, out _);
        if (t < Kickoff + 2)
        {
            var settle = Smooth((t - Kickoff) / 2);
            at = Vector2.Lerp(Formation(doll, t), at, settle);
            rotation *= settle;
        }

        foreach (var pull in _pulls[doll])
        {
            var weight = Weight(pull, t);
            if (weight > 0)
                at = Vector2.Lerp(at, pull.Target, weight);
        }

        foreach (var knock in _knocks[doll])
        {
            var age = t - knock.Time;
            if (age <= 0)
                continue;

            at += knock.Push * (age / KnockTime * MathF.Exp(1 - age / KnockTime));
            rotation += knock.Spin * (1 - MathF.Exp(-age / 0.6f));
            if (knock.Shock && age < 0.7f)
                at += new Vector2(MathF.Sin(age * 97) * 2.5f, MathF.Cos(age * 83) * 2.5f);
        }

        at.X = Math.Clamp(at.X, CageLeft + 14, CageRight - 14);
        at.Y = Math.Clamp(at.Y, CageTop + 16, CageBottom - 16);
        return at;
    }

    /// <summary>Lined up in formation before the kickoff, bobbing gently.</summary>
    private static Vector2 Formation(int doll, float t)
    {
        var side = doll / CMUSports.Squad;
        var spot = Lineup[doll % CMUSports.Squad];
        return Screen(side, spot.X, GoalY + spot.Y) + new Vector2(MathF.Sin(t * 1.3f + doll) * 3, MathF.Cos(t * 1.1f + doll * 2) * 4);
    }

    /// <summary>
    /// The player's own roaming: from one push-off point to the next, thrusting away, coasting and braking, each
    /// point inside their role's reach and leaning with whoever has the ball. Some points are perches on the walls.
    /// </summary>
    private Vector2 Path(int doll, float t, out float rotation, out int segment, out float progress, out Vector2 heading)
    {
        var side = doll / CMUSports.Squad;
        var player = doll % CMUSports.Squad;
        var period = 2.6f + player * 0.45f + side * 0.25f;
        var x = t / period + Hash(_id, doll, 77);
        segment = (int) MathF.Floor(x);
        progress = x - segment;
        var a = Waypoint(doll, segment, out _);
        var b = Waypoint(doll, segment + 1, out _);
        var eased = Drift(progress);
        var u = Math.Clamp(Lerp(a.X, b.X, eased) + Lean(side, t), 0.03f, 0.95f);
        rotation = Lerp(a.Z, b.Z, eased);
        heading = Screen(side, b.X, b.Y) - Screen(side, a.X, a.Y);
        return Screen(side, u, Lerp(a.Y, b.Y, eased));
    }

    /// <summary>A push-off point: X along the attack, Y on screen, Z the turn in degrees. Perches hang feet to the wall.</summary>
    private Vector3 Waypoint(int doll, int segment, out bool perch)
    {
        var player = doll % CMUSports.Squad;
        var u = Lerp(RoleNear[player], RoleFar[player], Hash(_id, doll * 4 + 1, segment));
        var across = Hash(_id, doll * 4 + 2, segment);
        var turn = Hash(_id, doll * 4 + 4, segment) - 0.5f;
        perch = Hash(_id, doll * 4 + 3, segment) < WallChance[player];
        if (!perch)
            return new Vector3(u, Lerp(CageTop + 30, CageBottom - 30, across), turn * 70);

        var top = across < 0.5f;
        return new Vector3(u, top ? CageTop + Perch : CageBottom - Perch, (top ? 180 : 0) + turn * 20);
    }

    /// <summary>Teams push up when they get the ball and drop back when they lose it, over a second and a half.</summary>
    private float Lean(int side, float t)
    {
        var i = _turnovers.Count - 1;
        while (i >= 0 && _turnovers[i].Time > t)
        {
            i--;
        }

        if (i < 0)
            return 0;

        var now = _turnovers[i].Side == side ? 0.1f : -0.08f;
        var before = i == 0 ? 0 : _turnovers[i - 1].Side == side ? 0.1f : -0.08f;
        return Lerp(before, now, Smooth((t - _turnovers[i].Time) / 1.5f));
    }

    private Vector2 Hand(int doll, float t)
    {
        return Player(doll, t, out _) + new Vector2(doll < CMUSports.Squad ? 12 : -12, -2);
    }

    /// <summary>The visor, at the front of the helmet, turned with the player.</summary>
    private static Vector2 Head(int doll, Vector2 at, float rotation)
    {
        var offset = new Vector2(doll < CMUSports.Squad ? 2 : -2, -9);
        return at + Turn(offset, -rotation * MathF.PI / 180);
    }

    private int LegAt(float t)
    {
        var low = 0;
        var high = _legs.Count - 1;
        var found = -1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (_legs[mid].Start <= t)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return found;
    }

    /// <summary>Where the ball is at a moment, and which of its legs it is on, or -1 before the match.</summary>
    private Vector2 Ball(float t, out int leg)
    {
        leg = LegAt(t);
        var hover = Centre + new Vector2(0, MathF.Sin((_play == null ? _real : t) * 2) * 4);
        if (leg < 0)
            return hover;

        var l = _legs[leg];
        switch (l.Kind)
        {
            case BallKind.Held:
                return Hand(l.Holder, t);
            case BallKind.Flight:
            case BallKind.Shot:
                return Vector2.Lerp(l.From, l.To, Math.Clamp((t - l.Start) / (l.End - l.Start), 0, 1));
            case BallKind.Net:
                return l.To + new Vector2(MathF.Sin(t * 3) * 1.5f, MathF.Cos(t * 2.3f) * 1.5f);
            case BallKind.Loose:
                return Fold(l.From + l.Velocity * (t - l.Start), out _);
            case BallKind.Gone:
                return l.To;
            default:
                return hover;
        }
    }

    /// <summary>A loose ball keeps its speed and bounces off the cage, like a light folded back into the box.</summary>
    private static Vector2 Fold(Vector2 at, out int bounces)
    {
        bounces = 0;
        return new Vector2(Fold(at.X, CageLeft + 8, CageRight - 8, ref bounces), Fold(at.Y, CageTop + 8, CageBottom - 8, ref bounces));
    }

    private static float Fold(float value, float min, float max, ref int bounces)
    {
        var range = max - min;
        var n = (int) MathF.Floor((value - min) / range);
        bounces += Math.Abs(n);
        var into = value - min - n * range;
        return (n & 1) == 0 ? min + into : max - into;
    }

    private static Vector2 GoalMouth(int shooting)
    {
        return new Vector2(shooting == 0 ? CageRight : CageLeft, GoalY);
    }

    private static Vector2 Screen(int side, float u, float y)
    {
        return new Vector2(side == 0 ? Lerp(CageLeft, CageRight, u) : Lerp(CageRight, CageLeft, u), y);
    }

    private static float Weight(Pull pull, float t)
    {
        if (t <= pull.Start || t >= pull.End)
            return 0;

        if (t < pull.Peak)
            return Smooth((t - pull.Start) / (pull.Peak - pull.Start));

        return t <= pull.Release ? 1 : 1 - Smooth((t - pull.Release) / (pull.End - pull.Release));
    }

    /// <summary>Thrust, coast, brake: speeds up over the first fifth, drifts steadily, slows over the last fifth.</summary>
    private static float Drift(float s)
    {
        const float ramp = 0.2f;
        const float k = 1 / (2 * ramp * (1 - ramp));
        if (s < ramp)
            return s * s * k;

        if (s > 1 - ramp)
            return 1 - (1 - s) * (1 - s) * k;

        return (s - ramp / 2) / (1 - ramp);
    }

    private static float Smooth(float x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static Vector2 Turn(Vector2 v, float radians)
    {
        var (sin, cos) = MathF.SinCos(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    /// <summary>A number from 0 to 1 that only depends on its inputs, for the look of a match: its paths and marks.</summary>
    private static float Hash(int a, int b, int c)
    {
        var h = (uint) a * 0x9E3779B1u ^ (uint) b * 0x85EBCA77u ^ (uint) c * 0xC2B2AE3Du;
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        h *= 0x297A2D39u;
        h ^= h >> 15;
        return (h >> 8) / 16777216f;
    }

    #endregion

    #region Events

    protected override void OnEvent(CMUBetMatch match, CMUSportsEvent ev, int index, bool live)
    {
        // the arena rewound to the start: the floor was wiped, so is the glass
        if (index <= _lastIndex)
            ResetFloor();

        _lastIndex = index;
        if (_play == null)
            return;

        switch (ev.Action)
        {
            case CMUSportsAction.Pass:
                if (index == 0)
                    Sound("/Audio/Items/Whistle/whistle_1.ogg", -2);
                else if (_play.Events[index - 1].Action == CMUSportsAction.Goal)
                    Sound("/Audio/Items/Whistle/whistle_2.ogg", -4);

                SoundAny(Swooshes, -12);
                break;
            case CMUSportsAction.Goal:
                OnGoal(match, ev);
                break;
            case CMUSportsAction.Injury:
                OnHit(match, HitAt(index), live);
                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.ZeroGShock:
                foreach (var zap in _zaps)
                {
                    if (zap.Index == index)
                        OnZap(match, zap, live);
                }

                break;
            case CMUSportsAction.Special when ev.Value == CMUSports.ZeroGShot:
                OnShot(match, ev);
                break;
        }
    }

    private void OnShot(CMUBetMatch match, CMUSportsEvent ev)
    {
        var shooter = ev.Side * CMUSports.Squad + Squad(ev.Extra);
        var at = Player(shooter, ev.Time, out _);
        var back = ev.Side == 0 ? MathF.PI : 0;
        Burst(at + new Vector2(ev.Side == 0 ? -10 : 10, 2), 16, Puff, 110, 0.6f, 3, back, 0.9f, drag: 3);
        Burst(Ball(ev.Time, out _), 8, KitColour(match, ev.Side), 90, 0.35f, 1.5f, back, 0.6f, streak: true);
        Sound("/Audio/Weapons/dodgeball.ogg");
        SoundAny(Swooshes, -2);
        Sound("/Audio/Effects/quick_exhale.ogg", -8, 0.6f);
    }

    private void OnGoal(CMUBetMatch match, CMUSportsEvent ev)
    {
        var mouth = GoalMouth(ev.Side);
        var inward = ev.Side == 0 ? MathF.PI : 0;
        var colour = KitColour(match, ev.Side);
        Burst(mouth, 40, colour, 190, 1.6f, 2.5f, inward, 2.4f, drag: 1.4f);
        Burst(mouth, 24, Color.White, 160, 1.4f, 2, inward, 2.2f, drag: 1.4f);
        Burst(mouth, 16, Color.Gold, 210, 1.2f, 1.5f, inward, 1.6f, streak: true, drag: 1.2f);
        Flash(colour.WithAlpha(0.3f), 0.6f);
        Shake(3, 0.5f);
        Sound("/Audio/Machines/warning_buzzer.ogg", -3);
        Sound("/Audio/Items/airhorn.ogg", -6);
        SoundAny(Rattles, -2);
        Cheer(false);
    }

    private void OnHit(CMUBetMatch match, Hit hit, bool live)
    {
        var side = hit.Doll / CMUSports.Squad;
        var at = Player(hit.Doll, hit.Time, out var rotation);
        var head = Head(hit.Doll, at, rotation);
        if (!live)
        {
            // what the globules left while nobody watched: blood on the nearest wall, and on the glass for the worst
            var wall = head.Y < GoalY ? CageTop : CageBottom;
            for (var i = 0; i <= hit.Severity; i++)
            {
                Splat(new Vector2(head.X + (FxFloat() - 0.5f) * 90, wall), 5 + FxFloat() * 7 + hit.Severity * 2);
            }

            if (hit.Severity >= 2)
                AddGlass(head + new Vector2((FxFloat() - 0.5f) * 50, (FxFloat() - 0.5f) * 30), 8 + hit.Severity * 4);

            return;
        }

        var angle = MathF.Atan2(hit.Direction.Y, hit.Direction.X);
        SoundAny(Punches, -1);
        Sound("/Audio/Weapons/genhit1.ogg", -6);
        switch (hit.Severity)
        {
            case 1:
                Sound("/Audio/Effects/glass_smack.ogg", -2);
                break;
            case 2:
                SoundAny(Cracks, -1);
                break;
            default:
                Sound("/Audio/_RMC14/Effects/hit_on_shattered_glass.ogg");
                SoundAny(Gore, -4);
                Flash(Color.White.WithAlpha(0.18f), 0.12f);
                break;
        }

        if (hit.Severity >= 2)
            Scream(CMUSports.Female(match.Sport, match.Entrants[side]));

        Shake(1.5f + hit.Severity, 0.3f + hit.Severity * 0.08f);
        Burst(hit.Contact, 6 + hit.Severity * 2, Color.White.WithAlpha(0.7f), 60, 0.3f, 2.5f, drag: 4);
        Burst(head, 4 + hit.Severity * 5, Shard, 130, 0.55f, 1.4f, angle, 2.4f, streak: true, drag: 1.5f);
        SpawnBlood(head, hit.Direction, 3 + hit.Severity * 4, 30 + hit.Severity * 25);
        var colour = hit.Severity switch
        {
            1 => Color.Yellow,
            2 => Color.Orange,
            _ => Color.FromHex("#FF4040"),
        };
        Pop(Loc.GetString($"cmu-game-zerog-hit-{hit.Severity}"), head - new Vector2(0, 18), colour, hit.Severity == 3);
    }

    private void OnZap(CMUBetMatch match, Zap zap, bool live)
    {
        Splat(zap.Contact, 18, Scorch);
        if (!live)
        {
            Splat(zap.Contact + new Vector2((FxFloat() - 0.5f) * 30, 0), 7);
            return;
        }

        var side = zap.Doll / CMUSports.Squad;
        var at = Player(zap.Doll, zap.Time, out var rotation);
        var head = Head(zap.Doll, at, rotation);
        var away = zap.Top ? MathF.PI / 2 : -MathF.PI / 2;
        Sound("/Audio/Effects/Lightning/lightningshock.ogg", -2);
        SoundAny(Sparks, -4);
        Sound("/Audio/Effects/PowerSink/electric.ogg", -8);
        Scream(CMUSports.Female(match.Sport, match.Entrants[side]), 1);
        Flash(Arc.WithAlpha(0.35f), 0.18f);
        Shake(4, 0.5f);
        Burst(zap.Contact, 30, Spark, 220, 0.45f, 1.5f, away, 2.6f, streak: true, drag: 2);
        Burst(zap.Contact, 16, Arc, 160, 0.35f, 1.5f, away, 2.2f, streak: true, drag: 2);
        Burst(head, 10, Color.FromHex("#5A5A60").WithAlpha(0.5f), 30, 1.2f, 4, away, 2, drag: 1.5f);
        SpawnBlood(head, new Vector2(0, zap.Top ? 1 : -1), 4, 40);
        Pop(Loc.GetString("cmu-game-zerog-zap"), head - new Vector2(0, 18), Arc, true);
    }

    private void PlayCue(CMUBetMatch match, Cue cue)
    {
        switch (cue.Kind)
        {
            case CueKind.Catch:
                Sound("/Audio/Effects/soft_thump.ogg", -14);
                break;
            case CueKind.Post:
                Sound("/Audio/_RMC14/Items/metal_chair_clang.ogg", -2);
                Burst(cue.At, 12, Spark, 150, 0.4f, 1.5f, streak: true, drag: 2);
                Shake(2);
                Cheer(true);
                Pop(Loc.GetString("cmu-game-zerog-post"), cue.At + new Vector2(cue.Side == 0 ? -50 : 50, -16), Color.White);
                break;
            case CueKind.Save:
                Sound("/Audio/Effects/soft_thump.ogg", -2);
                SoundAny(Punches, -6);
                Cheer(true);
                Burst(cue.At, 10, Color.White.WithAlpha(0.8f), 90, 0.35f, 2, drag: 3);
                Shake(1.5f);
                Pop(Loc.GetString("cmu-game-zerog-save"), cue.At + new Vector2(0, -22), KitColour(match, 1 - cue.Side), true);
                break;
            case CueKind.Arm:
                Sound("/Audio/Machines/airlock_electrify_on.ogg", -4);
                break;
            case CueKind.Final:
                Sound("/Audio/Items/Whistle/trench_whistle_1.ogg", -2);
                Sound("/Audio/Items/airhorn.ogg", -6);
                Cheer(false);
                var colour = KitColour(match, cue.Side);
                for (var side = 0; side < 2; side++)
                {
                    var mouth = GoalMouth(side);
                    var inward = side == 0 ? MathF.PI : 0;
                    Burst(mouth, 20, colour, 170, 1.8f, 2.5f, inward, 2.4f, drag: 1.2f);
                    Burst(mouth, 14, Color.Gold, 170, 1.8f, 2, inward, 2.4f, drag: 1.2f);
                    Burst(mouth, 14, Color.White, 150, 1.6f, 2, inward, 2.4f, drag: 1.2f);
                }

                break;
        }
    }

    /// <summary>
    /// The crowd behind the glass: a big cheer for a goal or the final whistle, unless one is still running, when
    /// the short one stands in; the short one alone for a save or a shot off the post.
    /// </summary>
    private void Cheer(bool small)
    {
        if (!Live)
            return;

        if (!small && _real - _cheered >= CheerLength)
        {
            _cheered = _real;
            SoundAny(Cheers, -6, 0.03f);
            return;
        }

        Sound(SmallCheer, -6);
    }

    #endregion

    #region Blood in zero-g

    private void SpawnBlood(Vector2 at, Vector2 towards, int count, float speed)
    {
        var angle = MathF.Atan2(towards.Y, towards.X);
        for (var i = 0; i < count && _globules.Count < MaxGlobules; i++)
        {
            var a = angle + (FxFloat() - 0.5f) * 2.2f;
            _globules.Add(new Globule
            {
                Position = at,
                Velocity = new Vector2(MathF.Cos(a), MathF.Sin(a)) * speed * (0.3f + FxFloat() * 0.9f),
                Radius = 1.2f + FxFloat() * 2.2f,
                Phase = FxFloat() * MathF.Tau,
                Life = 5 + FxFloat() * 3,
                DepthSpeed = (FxFloat() - 0.35f) * 0.9f,
                Colour = FxFloat() < 0.3f ? DarkBlood : Blood,
            });
        }
    }

    /// <summary>Globules drift until they reach a wall, the glass or the back mesh, and burst there as a stain.</summary>
    private void UpdateGlobules(float delta)
    {
        for (var i = _globules.Count - 1; i >= 0; i--)
        {
            var g = _globules[i];
            g.Age += delta;
            g.Position += g.Velocity * delta;
            g.Velocity *= MathF.Max(0, 1 - 0.08f * delta);
            g.Depth += g.DepthSpeed * delta;
            g.Phase += delta * 9;
            var x = Math.Clamp(g.Position.X, CageLeft, CageRight);
            var y = Math.Clamp(g.Position.Y, CageTop, CageBottom);
            if (g.Depth >= 1)
            {
                AddGlass(g.Position, g.Radius * 5);
                SoundAny(Splats, -16);
            }
            else if (x != g.Position.X || y != g.Position.Y)
            {
                Splat(new Vector2(x, y), g.Radius * 3.2f, g.Colour);
                SoundAny(Splats, -18);
            }
            else if (g.Age >= g.Life || g.Depth <= -1)
            {
                Splat(g.Position, g.Radius * 2.6f, g.Colour.WithAlpha(0.6f));
            }
            else
            {
                _globules[i] = g;
                continue;
            }

            _globules.RemoveAt(i);
        }
    }

    private void AddGlass(Vector2 at, float size)
    {
        if (_glass.Count >= MaxGlass)
            _glass.RemoveAt(0);

        var colour = (FxFloat() < 0.3f ? DarkBlood : Blood).WithAlpha(0.75f);
        _glass.Add(new Glass(at, size, FxRandom.Next(6), FxFloat() * MathF.Tau, colour));
    }

    private void ResetFloor()
    {
        _globules.Clear();
        _glass.Clear();
    }

    #endregion

    protected override void UpdateMatch(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        var length = CMUSports.Length(match.Sport);
        var over = _play != null && t >= length;
        _after = over ? _after + _delta : 0;
        var time = _play == null ? t : t + _after;
        _t = time;

        if (_play != null)
        {
            // cues between events: only for time that really passed on screen, not a jump back to the match
            if (!float.IsNaN(_lastT) && t > _lastT && t - _lastT < 0.5f)
            {
                foreach (var cue in _cues)
                {
                    if (cue.Time > _lastT && cue.Time <= t)
                        PlayCue(match, cue);
                }
            }

            _lastT = t;
        }

        for (var doll = 0; doll < Dolls; doll++)
        {
            var side = doll / CMUSports.Squad;
            var at = Player(doll, time, out var rotation);
            if (over && side == match.Winner)
                rotation += _after * 160;

            _pos[doll] = at;
            _rot[doll] = rotation;
            Thrusters(doll, time);
            Pose(doll, at.X, at.Y, side == 0 ? Direction.East : Direction.West, rotation);

            var crack = 0;
            foreach (var hit in _hits)
            {
                if (hit.Doll == doll && hit.Time <= time)
                    crack = Math.Max(crack, hit.Severity);
            }

            _crack[doll] = crack;
            if (crack >= 2 && !over && FxFloat() < _delta * (crack == 3 ? 1.6f : 0.7f))
                SpawnBlood(Head(doll, at, rotation), new Vector2(FxFloat() - 0.5f, FxFloat() - 0.5f), 1, 14);

            DollView(doll).Modulate = Tint(doll, time);
        }

        var ball = Ball(time, out var leg);
        _ballAt = ball;
        _ballLeg = leg;
        var kind = leg < 0 ? BallKind.Hover : _legs[leg].Kind;
        if (kind == BallKind.Loose)
        {
            Fold(_legs[leg].From + _legs[leg].Velocity * (time - _legs[leg].Start), out var bounces);
            if (leg == _bounceLeg && bounces > _bounces)
            {
                SoundAny(Thuds, -12);
                Burst(ball, 5, Color.White.WithAlpha(0.6f), 50, 0.3f, 1.5f, drag: 3);
            }

            _bounceLeg = leg;
            _bounces = bounces;
        }

        var flying = kind is BallKind.Flight or BallKind.Shot or BallKind.Loose;
        _spin = (_spin + _delta * (flying ? 720 : 90)) % 360;
        if (kind != BallKind.Gone)
        {
            _ball.WorldRotation = Angle.FromDegrees(_spin);
            Place(_ball, ball.X, ball.Y);
        }

        UpdateGlobules(_delta);

        _score[0] = _score[1] = 0;
        _fresh = -1;
        foreach (var (when, side) in _goals)
        {
            if (when > time)
                break;

            _score[side]++;
            if (time - when < 2.5f)
                _fresh = side;
        }

        if (_fresh >= 0)
            Say(Loc.GetString("cmu-sport-goal", ("team", match.Name(_fresh))), ArenaWidth / 2, ArenaHeight / 2 - 10, Color.Gold, true);
    }

    /// <summary>Puffs from the backpack as a player pushes off towards the next point, and as they brake into it.</summary>
    private void Thrusters(int doll, float time)
    {
        if (_play == null)
            return;

        Path(doll, time, out _, out var segment, out var progress, out var heading);
        var direction = heading.LengthSquared() > 1 ? Vector2.Normalize(heading) : Vector2.UnitX;
        var at = _pos[doll];
        if (segment != _segment[doll])
        {
            if (segment == _segment[doll] + 1 && time > Kickoff)
            {
                Waypoint(doll, segment, out var perch);
                var back = MathF.Atan2(-direction.Y, -direction.X);
                Burst(at - direction * 10, perch ? 14 : 6, Puff, perch ? 90 : 55, 0.5f, perch ? 3 : 2.2f, back, 0.8f, drag: 3);
                if (perch)
                {
                    SoundAny(Thuds, -16);
                    Sound("/Audio/Effects/quick_exhale.ogg", -14, 0.65f);
                }
            }

            _segment[doll] = segment;
            _braked[doll] = false;
        }
        else if (!_braked[doll] && progress > 0.8f)
        {
            _braked[doll] = true;
            if (time > Kickoff)
                Burst(at + direction * 10, 4, Puff, 40, 0.4f, 2, MathF.Atan2(direction.Y, direction.X), 0.7f, drag: 3);
        }
    }

    /// <summary>A shocked player flickers between lit up and skeleton dark, then stays a little singed.</summary>
    private Color Tint(int doll, float time)
    {
        foreach (var zap in _zaps)
        {
            if (zap.Doll != doll)
                continue;

            var age = time - zap.Time;
            if (age < 0 || age >= 4)
                continue;

            if (age < 0.7f)
                return ((int) (age * 24) & 1) == 0 ? Lit : Charred;

            return Color.InterpolateBetween(Singed, Color.White, (age - 0.7f) / 3.3f);
        }

        return Color.White;
    }

    #region Drawing

    protected override void DrawField(DrawingHandleScreen handle)
    {
        Rect(handle, 0, Bar, ArenaWidth, ArenaHeight - Bar, Color.FromHex("#03040A"));

        // a nebula far behind the cage
        Circle(handle, 440, 80, 96, Color.FromHex("#2A1A4A").WithAlpha(0.22f));
        Circle(handle, 470, 64, 60, Color.FromHex("#4A2470").WithAlpha(0.18f));
        Circle(handle, 410, 110, 40, Color.FromHex("#6A2A60").WithAlpha(0.12f));

        // stars drifting by, the near ones faster
        for (var star = 0; star < 84; star++)
        {
            var layer = star % 3;
            var x = Hash(star, 1, 31) * ArenaWidth - _real * (2 + layer * 4);
            x -= MathF.Floor(x / ArenaWidth) * ArenaWidth;
            var y = Bar + Hash(star, 2, 31) * (ArenaHeight - Bar);
            var twinkle = 0.25f + layer * 0.18f + MathF.Sin(_real * (1 + layer) + star) * 0.08f;
            var size = 1 + layer * 0.5f;
            Rect(handle, x, y, size, size, Color.White.WithAlpha(twinkle));
        }

        // a planet, lit from the top right: its thin crescent and the haze of its air
        Circle(handle, 64, 216, 56, Color.FromHex("#5FA8E0").WithAlpha(0.12f));
        Circle(handle, 64, 216, 52, Color.FromHex("#2A527C"));
        Circle(handle, 56, 222, 50, Color.FromHex("#0B1422"));

        // the cage: a back panel with its mesh, the halfway line and circle
        Rect(handle, CageLeft, CageTop, CageRight - CageLeft, CageBottom - CageTop, Color.FromHex("#0B1626").WithAlpha(0.55f));
        var mesh = CageColour.WithAlpha(0.06f);
        for (var x = CageLeft + 24; x < CageRight; x += 24)
        {
            Line(handle, new Vector2(x, CageTop), new Vector2(x, CageBottom), mesh);
        }

        for (var y = CageTop + 22; y < CageBottom; y += 22)
        {
            Line(handle, new Vector2(CageLeft, y), new Vector2(CageRight, y), mesh);
        }

        Rect(handle, ArenaWidth / 2 - 1, CageTop, 2, CageBottom - CageTop, CageColour.WithAlpha(0.2f));
        Circle(handle, ArenaWidth / 2, GoalY, 32, CageColour.WithAlpha(0.22f), false);
        Circle(handle, ArenaWidth / 2, GoalY, 3, CageColour.WithAlpha(0.35f));
        Rect(handle, Serve.X - 10, CageTop, 20, 4, Color.FromHex("#2E3E52"));

        if (Match is not { } match)
            return;

        // each team's own goal, a netted pocket in its end wall
        for (var side = 0; side < 2; side++)
        {
            var colour = KitColour(match, side);
            var left = side == 0;
            var x = left ? CageLeft - 12 : CageRight;
            Rect(handle, x, GoalY - GoalHalf, 12, GoalHalf * 2, colour.WithAlpha(0.18f));
            for (var y = GoalY - GoalHalf + 6; y < GoalY + GoalHalf; y += 6)
            {
                Line(handle, new Vector2(x, y), new Vector2(x + 12, y), Color.White.WithAlpha(0.18f));
            }

            Line(handle, new Vector2(x + 6, GoalY - GoalHalf), new Vector2(x + 6, GoalY + GoalHalf), Color.White.WithAlpha(0.18f));
        }

        // the walls: the end walls around the goal mouths, the live top and bottom rails with their hazard stripes
        var wallHeight = GoalY - GoalHalf - (CageTop - 4);
        Rect(handle, CageLeft - 4, CageTop - 4, 4, wallHeight, Rail);
        Rect(handle, CageLeft - 4, GoalY + GoalHalf, 4, wallHeight, Rail);
        Rect(handle, CageRight, CageTop - 4, 4, wallHeight, Rail);
        Rect(handle, CageRight, GoalY + GoalHalf, 4, wallHeight, Rail);
        for (var wall = 0; wall < 2; wall++)
        {
            var y = wall == 0 ? CageTop - 4 : CageBottom;
            Rect(handle, CageLeft - 4, y, CageRight - CageLeft + 8, 4, Rail);
            for (var x = CageLeft + 4; x < CageRight - 8; x += 20)
            {
                Rect(handle, x, y + 1, 9, 2, Hazard.WithAlpha(0.7f));
            }

            var hum = 0.18f + MathF.Sin(_real * 7 + wall * 2) * 0.05f;
            Rect(handle, CageLeft, wall == 0 ? CageTop : CageBottom - 1, CageRight - CageLeft, 1, CageColour.WithAlpha(hum));
        }

        // a wall arming under someone about to touch it
        foreach (var zap in _zaps)
        {
            var age = _t - zap.Time;
            if (age < -ArmTime || age > 0.6f)
                continue;

            var power = age < 0 ? Smooth((age + ArmTime) / ArmTime) : 1 - age / 0.6f;
            var y = zap.Top ? CageTop - 3 : CageBottom - 3;
            Rect(handle, zap.Contact.X - 50, y, 100, 6, Arc.WithAlpha(0.15f + power * 0.45f));
            Rect(handle, zap.Contact.X - 26, y + 1, 52, 4, Color.White.WithAlpha(power * 0.5f));
        }
    }

    protected override void DrawOverDecals(DrawingHandleScreen handle)
    {
        if (Match is not { } match)
            return;

        // a soft glow in each kit's colour behind the players, so the teams read against the dark
        for (var doll = 0; doll < Dolls; doll++)
        {
            var colour = KitColour(match, doll / CMUSports.Squad);
            Circle(handle, _pos[doll].X, _pos[doll].Y, 18, colour.WithAlpha(0.07f));
            Circle(handle, _pos[doll].X, _pos[doll].Y, 11, colour.WithAlpha(0.07f));
        }

        if (_ballLeg < 0 || _ballLeg >= _legs.Count)
            return;

        var leg = _legs[_ballLeg];
        if (leg.Kind is not (BallKind.Flight or BallKind.Shot or BallKind.Loose))
            return;

        // the ball's trail: long and in the shooter's colour on a shot, a faint wake on a pass
        var shot = leg.Kind == BallKind.Shot;
        var samples = shot ? 12 : 7;
        var trail = shot ? KitColour(match, leg.Holder) : Color.White;
        for (var i = samples; i >= 1; i--)
        {
            var when = _t - i * 0.022f;
            if (when < leg.Start)
                continue;

            var at = Ball(when, out _);
            var fade = 1 - (float) i / (samples + 1);
            Circle(handle, at.X, at.Y, (shot ? 7 : 5) * fade + 1, trail.WithAlpha((shot ? 0.45f : 0.18f) * fade));
        }

        if (shot)
            Circle(handle, _ballAt.X, _ballAt.Y, 11, Color.White.WithAlpha(0.15f));
    }

    protected override void DrawOverlay(DrawingHandleScreen handle)
    {
        if (Match is not { } match)
            return;

        foreach (var g in _globules)
        {
            var radius = g.Radius * (1 + g.Depth * 0.35f) * (1 + MathF.Sin(g.Phase) * 0.14f);
            Circle(handle, g.Position.X, g.Position.Y, radius + 0.6f, DarkBlood);
            Circle(handle, g.Position.X, g.Position.Y, radius, g.Colour);
            Circle(handle, g.Position.X - radius * 0.35f, g.Position.Y - radius * 0.35f, radius * 0.3f, Color.FromHex("#FF8A8A").WithAlpha(0.55f));
        }

        DrawGoalLights(handle, match);
        DrawImpacts(handle);
        DrawVisors(handle);

        // the glass in front: two soft reflections, the blood that hit it, the bolts of its frame
        var shimmer = 0.035f + MathF.Sin(_real * 0.7f) * 0.01f;
        Poly(handle, Color.White.WithAlpha(shimmer), new Vector2(120, CageTop), new Vector2(175, CageTop),
            new Vector2(95, CageBottom), new Vector2(40, CageBottom));
        Poly(handle, Color.White.WithAlpha(shimmer * 0.7f), new Vector2(350, CageTop), new Vector2(368, CageTop),
            new Vector2(288, CageBottom), new Vector2(270, CageBottom));

        _splatter ??= new Texture?[6];
        foreach (var glass in _glass)
        {
            var texture = _splatter[glass.State] ??= Sprite("/Textures/Fluids/splatter.rsi", $"splatter-{glass.State}");
            Texture(handle, texture, glass.Position, new Vector2(glass.Size, glass.Size), glass.Rotation, glass.Colour);
        }

        Frame(handle, CageLeft, CageTop, CageRight - CageLeft, CageBottom - CageTop, Color.White.WithAlpha(0.1f));
        var bolt = Color.FromHex("#8FA3B8");
        for (var corner = 0; corner < 4; corner++)
        {
            var x = corner % 2 == 0 ? CageLeft - 6 : CageRight - 4;
            var y = corner < 2 ? CageTop - 6 : CageBottom - 4;
            Rect(handle, x, y, 10, 10, Rail);
            Frame(handle, x, y, 10, 10, bolt);
            Circle(handle, x + 5, y + 5, 1.5f, bolt);
        }

        DrawScoreboard(handle, match);
    }

    /// <summary>The goal frame strobes in the scorer's colour, fading over three seconds.</summary>
    private void DrawGoalLights(DrawingHandleScreen handle, CMUBetMatch match)
    {
        for (var side = 0; side < 2; side++)
        {
            var mouth = GoalMouth(1 - side);
            var lit = 0f;
            var by = side;
            foreach (var (when, scorer) in _goals)
            {
                var age = _t - when;
                if (scorer == 1 - side && age >= 0 && age < 3)
                {
                    lit = ((int) (age * 8) & 1) == 0 ? 1 - age / 3 : 0.35f * (1 - age / 3);
                    by = scorer;
                }
            }

            var colour = KitColour(match, by);
            var x = side == 0 ? CageLeft - 4 : CageRight;
            var glow = 0.55f + lit * 0.45f;
            Rect(handle, x, mouth.Y - GoalHalf - 2, 4, 4, colour.WithAlpha(glow));
            Rect(handle, x, mouth.Y + GoalHalf - 2, 4, 4, colour.WithAlpha(glow));
            Line(handle, new Vector2(x + 2, mouth.Y - GoalHalf), new Vector2(x + 2, mouth.Y + GoalHalf), colour.WithAlpha(0.25f + lit * 0.6f));
            if (lit <= 0)
                continue;

            for (var ring = 1; ring <= 3; ring++)
            {
                var grow = ring * 5;
                Frame(handle, x - grow, mouth.Y - GoalHalf - grow, 4 + grow * 2, (GoalHalf + grow) * 2, colour.WithAlpha(lit * 0.5f / ring));
            }

            Circle(handle, x + 2, mouth.Y, 26 * lit + 6, colour.WithAlpha(lit * 0.18f));
        }
    }

    /// <summary>A white impact frame where a check lands, and bolts from the wall into a shocked player.</summary>
    private void DrawImpacts(DrawingHandleScreen handle)
    {
        foreach (var hit in _hits)
        {
            var age = _t - hit.Time;
            if (age < 0 || age >= 0.1f)
                continue;

            var size = 6 + hit.Severity * 3;
            Circle(handle, hit.Contact.X, hit.Contact.Y, size * (1 - age * 5), Color.White.WithAlpha(0.85f));
            for (var ray = 0; ray < 8; ray++)
            {
                var (sin, cos) = MathF.SinCos(ray * MathF.Tau / 8 + hit.Index);
                var reach = size * 2.2f;
                Line(handle, hit.Contact + new Vector2(cos, sin) * size, hit.Contact + new Vector2(cos, sin) * reach, Color.White);
            }
        }

        foreach (var zap in _zaps)
        {
            var age = _t - zap.Time;
            if (age < -ArmTime || age >= 0.6f)
                continue;

            if (age < 0)
            {
                // crackles along the arming rail
                if (FxFloat() < 0.5f)
                    Bolt(handle, zap.Contact + new Vector2(-30 + FxFloat() * 20, 0), zap.Contact + new Vector2(10 + FxFloat() * 20, 0), 3, Arc.WithAlpha(0.7f));

                continue;
            }

            var head = Head(zap.Doll, _pos[zap.Doll], _rot[zap.Doll]);
            Bolt(handle, zap.Contact, head, 6, Color.White);
            Bolt(handle, zap.Contact + new Vector2(-12, 0), _pos[zap.Doll], 5, Arc);
            Bolt(handle, zap.Contact + new Vector2(12, 0), _pos[zap.Doll] + new Vector2(0, 8), 5, Arc.WithAlpha(0.7f));
        }
    }

    private void Bolt(DrawingHandleScreen handle, Vector2 from, Vector2 to, float jitter, Color colour)
    {
        var normal = to - from;
        normal = normal.LengthSquared() > 0.01f ? Vector2.Normalize(new Vector2(-normal.Y, normal.X)) : Vector2.UnitY;
        for (var i = 0; i < _bolt.Length; i++)
        {
            var along = (float) i / (_bolt.Length - 1);
            var shake = i == 0 || i == _bolt.Length - 1 ? 0 : (FxFloat() - 0.5f) * 2 * jitter;
            _bolt[i] = Vector2.Lerp(from, to, along) + normal * shake;
        }

        for (var i = 1; i < _bolt.Length; i++)
        {
            Line(handle, _bolt[i - 1], _bolt[i], colour);
            Line(handle, _bolt[i - 1] + Vector2.UnitX * 0.6f, _bolt[i] + Vector2.UnitX * 0.6f, colour.WithAlpha(colour.A * 0.6f));
        }
    }

    /// <summary>Cracks across the visor of everyone who took a hard check, worse for harder ones; the worst fill with blood.</summary>
    private void DrawVisors(DrawingHandleScreen handle)
    {
        for (var doll = 0; doll < Dolls; doll++)
        {
            var crack = _crack[doll];
            if (crack == 0)
                continue;

            var head = Head(doll, _pos[doll], _rot[doll]);
            if (crack == 3)
                Circle(handle, head.X, head.Y, 3.5f, Blood.WithAlpha(0.55f));

            var turn = -_rot[doll] * MathF.PI / 180;
            for (var line = 0; line < crack * 2 + 1; line++)
            {
                var angle = Hash(_id, doll, line + 40) * MathF.Tau + turn;
                var reach = 2.5f + Hash(_id, doll, line + 60) * 2.5f;
                var (sin, cos) = MathF.SinCos(angle);
                var bend = Turn(new Vector2(cos, sin), 0.6f) * reach * 0.5f;
                var mid = head + new Vector2(cos, sin) * reach * 0.5f;
                Line(handle, head, mid, Color.White.WithAlpha(0.9f));
                Line(handle, mid, mid + bend, Color.White.WithAlpha(0.7f));
            }
        }
    }

    /// <summary>The score in the middle of the top bar, still while the cage shakes. The team with the ball gets a light.</summary>
    private void DrawScoreboard(DrawingHandleScreen handle, CMUBetMatch match)
    {
        var scale = UIScale;
        var centre = ArenaWidth / 2;
        var holder = -1;
        if (_ballLeg >= 0 && _ballLeg < _legs.Count && _legs[_ballLeg].Kind is BallKind.Held or BallKind.Flight)
            holder = _legs[_ballLeg].Holder / CMUSports.Squad;

        for (var side = 0; side < 2; side++)
        {
            var colour = KitColour(match, side);
            var box = side == 0 ? centre - 27 : centre + 5;
            var lit = _fresh == side && ((int) (_real * 6) & 1) == 0;
            handle.DrawRect(new UIBox2(box * scale, 4 * scale, (box + 22) * scale, 21 * scale), lit ? Color.Gold : Color.FromHex("#1B2230"));
            handle.DrawRect(new UIBox2(box * scale, 4 * scale, (box + 22) * scale, 21 * scale), colour.WithAlpha(0.8f), false);
            var digit = Digits[Math.Min(_score[side], 9)];
            var size = handle.GetDimensions(SmallFont, digit, scale);
            handle.DrawString(SmallFont, new Vector2((box + 11) * scale - size.X / 2, 6 * scale), digit, scale, lit ? Color.Black : Color.White);

            var name = _names[side];
            var width = handle.GetDimensions(SmallFont, name, scale).X;
            var chip = side == 0 ? centre - 40 : centre + 34;
            handle.DrawRect(new UIBox2(chip * scale, 7 * scale, (chip + 6) * scale, 18 * scale), colour);
            var nameX = side == 0 ? (chip - 5) * scale - width : (chip + 11) * scale;
            handle.DrawString(SmallFont, new Vector2(nameX, 7 * scale), name, scale, Color.White);
            if (holder == side)
                handle.DrawCircle(new Vector2(chip + 3, 22) * scale, 1.5f * scale, Color.FromHex("#7CFF00"));
        }

        handle.DrawRect(new UIBox2((centre - 2) * scale, 12 * scale, (centre + 2) * scale, 13.5f * scale), Color.White.WithAlpha(0.7f));
    }

    #endregion
}
