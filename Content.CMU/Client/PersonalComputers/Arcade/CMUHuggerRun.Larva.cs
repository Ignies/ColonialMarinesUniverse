using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Stage 2: the larva wriggles on down the corridor. Marines with bayonets close in and stab low in front of them
/// (jump the stab, or get past them before it lands; their legs are no obstacle to something this small), closed
/// airlocks leave only a gap at the bottom to duck through, and the tracers keep coming, a little less often. After a
/// while a floor vent shows up instead of the next threat: Down over it dives in and hands on to the vent crawl, and
/// a vent missed comes round again later.
/// </summary>
public sealed class CMUHuggerLarva : CMUHuggerCorridorStage
{
    // The larva's 32 px sprite at 1.25, low to the floor.
    private const float Size = 40;
    private const float FootPad = 3.75f;

    private const float FirstThreat = 2.5f;
    private const float FirstVent = 28;
    private const float VentAgain = 11;
    private const float FireRate = 0.7f;

    // An airlock: a door slab across the corridor with this gap under it, too low for anything not ducking.
    private const float AirlockWidth = 40;
    private const float AirlockGap = 14;

    // the airlock's beacon starts this many seconds before the larva would reach it, and the slab drops at this
    private const float AirlockWarn = 1.3f;
    private const float AirlockDrop = 0.8f;
    private const float AirlockFall = 2600;

    // locker traps in the larva's corridor: more marines, fewer people to run from
    private const float LockerMarines = 0.5f;
    private const float LockerColonists = 0.2f;
    private const float VentWidth = 44;
    private const float DiveReach = 24;
    private const float DiveTime = 0.75f;
    private const int VentBonus = 500;

    private const string Cry = "/Audio/_RMC14/Xeno/alien_roar_larva1.ogg";
    private const string Squeal = "/Audio/_RMC14/Xeno/alien_roar_larva2.ogg";
    private const string Steam = "/Audio/Effects/extinguish.ogg";
    private const string Clang = "/Audio/Effects/clang.ogg";
    private const string VentIn = "/Audio/_RMC14/Xeno/alien_ventpass1.ogg";
    private const string Alert = "/Audio/Effects/alert.ogg";
    private const string Drop = "/Audio/Effects/thudswoosh.ogg";
    private const string BlastDoor = Structures + "Doors/Shutters/Almayer/poddoor.rsi";
    private const string Pump = Structures + "Piping/Atmospherics/vent_pump.rsi";

    private static readonly string[] Thuds = { "/Audio/Effects/metal_thud1.ogg", "/Audio/Effects/metal_thud2.ogg", "/Audio/Effects/metal_thud3.ogg" };

    private static readonly string[] SparkSounds =
    {
        "/Audio/Effects/sparks1.ogg", "/Audio/Effects/sparks2.ogg", "/Audio/Effects/sparks3.ogg", "/Audio/Effects/sparks4.ogg",
    };

    private static readonly Color DarkAlarm = Color.FromHex("#4A1210");

    private enum AirlockPhase : byte
    {
        Raised,
        Warning,
        Falling,
        Jammed,
        Shut,
    }

    private struct Obstacle
    {
        public bool Active;
        public bool Vent;
        public float X;
        public bool Heard;
        public AirlockPhase Phase;

        /// <summary>Where the slab's bottom edge is on screen, and how fast it is falling.</summary>
        public float Bottom;

        public float Speed;
        public float Timer;

        /// <summary>The stage's first airlock, which says what to do in big letters.</summary>
        public bool First;

        /// <summary>The demo bot's random draw for this obstacle.</summary>
        public float Seed;
    }

    private readonly Obstacle[] _obstacles = new Obstacle[3];
    private float _nextThreat;
    private float _nextShot;
    private float _nextVent;
    private float _dive = -1;
    private float _diveX;
    private string? _shotText;
    private int _airlocks;

    protected override float HalfWidth => 13;
    protected override float Height => 20;
    protected override float DuckHeight => 10;
    protected override float JumpVelocity => 360;
    protected override float StartSpeed => 170;
    protected override float LaneLow => FloorY - 6;
    protected override float LaneMid => FloorY - 16;
    protected override float LaneHigh => FloorY - 44;

    public override string Title => Loc.GetString("cmu-game-hugger-larva-title");
    public override string Hint => Loc.GetString("cmu-game-hugger-larva-hint");

    public override void Enter()
    {
        base.Enter();
        _nextThreat = FirstThreat;
        _nextShot = 2;
        _nextVent = FirstVent;
        _dive = -1;
        _airlocks = 0;
        for (var i = 0; i < _obstacles.Length; i++)
        {
            _obstacles[i] = default;
        }

        Run.Play(Squeal, -4, 1.2f);
    }

    private bool ObstacleAhead(out bool vent)
    {
        vent = false;
        foreach (var obstacle in _obstacles)
        {
            if (!obstacle.Active || obstacle.X + AirlockWidth - Scroll < PlayerX - 30)
                continue;

            vent |= obstacle.Vent;
            return true;
        }

        return false;
    }

    /// <summary>An airlock or the vent, on the next bare stretch of wall so it never lands on a door or a locker.</summary>
    private void AddObstacle(bool vent)
    {
        for (var i = 0; i < _obstacles.Length; i++)
        {
            if (_obstacles[i].Active)
                continue;

            _obstacles[i] = new Obstacle
            {
                Active = true,
                Vent = vent,
                X = BareSlot(Scroll + CMUHuggerRun.ScreenWidth + 40) - (vent ? 0 : AirlockWidth / 2),
                Phase = AirlockPhase.Raised,
                Bottom = CeilingY + 6,
                First = !vent && _airlocks++ == 0,
                Seed = Run.Fx(),
            };
            return;
        }
    }

    /// <summary>One threat at a time: a knifeman, an airlock or a fleeing colonist, with tracers between; the vent clears the way.</summary>
    protected override void SpawnThreats(float dt)
    {
        _nextThreat -= dt;
        _nextShot -= dt;
        _nextVent -= dt;
        var busy = HumanAhead() || ObstacleAhead(out _) || LockerAhead();
        if (_nextVent <= 0 && !busy && !ShotsQueued && !BulletsAhead())
        {
            AddObstacle(true);
            _nextVent = VentAgain;
            _nextThreat = MathF.Max(_nextThreat, 2.5f);
            _nextShot = MathF.Max(_nextShot, 2.5f);
            return;
        }

        if (busy || _nextVent <= 0)
        {
            _nextShot = MathF.Max(_nextShot, 0.7f);
            return;
        }

        if (_nextThreat <= 0 && !ShotsQueued && !BulletsAhead())
        {
            var roll = Run.Fx();
            if (Time > 6 && roll < 0.35f)
                AddObstacle(false);
            else if (Time > 4 && roll < 0.55f)
                SpawnLocker(LockerMarines, LockerColonists);
            else if (Time > 18 && roll < 0.5f)
                SpawnHuman(CMUHuggerOutfit.Rifleman, true);
            else if (roll < 0.62f)
                SpawnHuman(CMUHuggerOutfit.Colonist, false);
            else
                SpawnHuman(CMUHuggerOutfit.Knifeman, false);

            _nextThreat = 3.5f + Run.Fx() * 2.5f;
            _nextShot = MathF.Max(_nextShot, 1.4f);
            return;
        }

        if (_nextShot > 0)
            return;

        FirePattern();
        _nextShot = FireGap(FireRate);
    }

    /// <summary>
    /// The demo bot jumps a knifeman's stab once it sees the wind-up, ducks under a dropping airlock, and dives into
    /// most vents (it lets one go by now and then, and once in a while it forgets to duck).
    /// </summary>
    protected override void BotStage(ref bool jump, ref float hold, ref bool duck)
    {
        foreach (var human in Humans)
        {
            if (!human.Active || !human.Knifeman || human.BotDone || human.State != CMUHuggerHumanState.WindUp)
                continue;

            if (human.Timer < 0.08f + human.BotSeed * 0.1f || human.X - Scroll - PlayerX > StabReachFar + 40 || !Grounded)
                continue;

            human.BotDone = true;
            if (human.BotSeed < 0.05f)
                continue;

            jump = true;
            hold = 0.3f;
        }

        foreach (var obstacle in _obstacles)
        {
            if (!obstacle.Active)
                continue;

            var x = obstacle.X - Scroll;
            if (obstacle.Vent)
            {
                if (obstacle.Seed >= 0.2f && MathF.Abs(x - PlayerX) < 16)
                    duck = true;

                continue;
            }

            if (obstacle.Phase >= AirlockPhase.Falling && obstacle.Seed >= 0.03f && x - PlayerX <= 30 + Speed * 0.2f
                && x + AirlockWidth > PlayerX - HalfWidth - 4)
                duck = true;
        }
    }

    /// <summary>Something this small goes between the legs.</summary>
    protected override void OnTouch(CMUHuggerHuman human, bool head)
    {
    }

    protected override void OnStabbed(CMUHuggerHuman human)
    {
        var tip = new Vector2(PlayerX, FloorY - Y - 8);
        Run.PlayAny(Flesh);
        KillPlayer(tip, new Vector2(-0.6f, 0.8f), new Vector2(-60, 160), -8, Cry, Loc.GetString("cmu-game-hugger-larva-stabbed"), 30);
    }

    protected override void OnShot(Vector2 hit, Vector2 dir)
    {
        ShotDown(hit, dir, Cry, _shotText ??= Loc.GetString("cmu-game-hugger-shot"));
    }

    protected override void CollideStage(float left, float right, float top, float bottom)
    {
        foreach (var obstacle in _obstacles)
        {
            if (!obstacle.Active)
                continue;

            var x = obstacle.X - Scroll;
            if (obstacle.Vent)
            {
                // Down over the grate dives in
                if (Grounded && Run.Held(CMUArcadeKey.Down) && MathF.Abs(x - PlayerX) < DiveReach)
                    Dive(x);

                continue;
            }

            if (!Overlap(left, right, top, bottom, x, x + AirlockWidth, CeilingY, obstacle.Bottom))
                continue;

            // smacked into the door face
            Run.Play(Clang, -2, 1.2f);
            Run.PlayAny(Flesh);
            Run.Splat(new Vector2(obstacle.X - 2, FloorY - 16), 26, CMUHuggerRun.Acid.WithAlpha(0.85f), 1, true);
            KillPlayer(new Vector2(x, (top + bottom) / 2), new Vector2(-1, 0), new Vector2(-110, 180), 10, Cry, Loc.GetString("cmu-game-hugger-larva-splat"), 30);
            return;
        }
    }

    private void Dive(float x)
    {
        Halt();
        _dive = 0;
        _diveX = x;
        Run.Bonus(VentBonus, World(x, FloorY - 40), CMUHuggerRun.Acid);
        Run.Play(VentIn, -2);
        Run.Play(Clang, -8, 1.4f);
    }

    protected override void UpdateStage(float dt)
    {
        for (var i = 0; i < _obstacles.Length; i++)
        {
            ref var obstacle = ref _obstacles[i];
            if (!obstacle.Active)
                continue;

            var x = obstacle.X - Scroll;
            if (x < -AirlockWidth - 40)
            {
                obstacle.Active = false;
                continue;
            }

            if (!obstacle.Vent && !obstacle.Heard && x < CMUHuggerRun.ScreenWidth - 40)
            {
                obstacle.Heard = true;
                Run.Play(Steam, -10, 0.8f);
            }

            if (!obstacle.Vent)
                MoveAirlock(ref obstacle, x, dt);

            // steam hisses out from under a closed airlock
            if (!obstacle.Vent && obstacle.Phase >= AirlockPhase.Jammed && Run.Fx() < dt * 14)
            {
                Run.Emit(new Vector2(obstacle.X + Run.Fx() * AirlockWidth, FloorY - 4), 1, Color.White.WithAlpha(0.25f), 40, 0.8f, 3,
                    MathF.PI + (Run.Fx() < 0.5f ? 0 : MathF.PI), 0.6f, -30, kind: CMUHuggerRun.Smoke);
            }
        }

        if (_dive < 0 || Run.Over)
            return;

        _dive += dt;
        if (_dive >= DiveTime)
            Run.NextStage();
    }

    public override void DrawBack(DrawingHandleScreen handle)
    {
        base.DrawBack(handle);
        foreach (var obstacle in _obstacles)
        {
            if (obstacle.Active && obstacle.Vent)
                DrawVent(handle, obstacle.X - Scroll);
        }
    }

    public override void DrawFront(DrawingHandleScreen handle)
    {
        base.DrawFront(handle);
        foreach (var obstacle in _obstacles)
        {
            if (obstacle.Active && !obstacle.Vent)
                DrawAirlock(handle, obstacle, obstacle.X - Scroll);
        }

        if (_dive < 0)
            return;

        // down the grate it goes
        var t = Math.Clamp(_dive / DiveTime, 0, 1);
        var larva = Run.Frame(CMUHuggerRun.Larva, "alive", RsiDirection.South);
        Run.DrawSprite(handle, larva, new Vector2(_diveX, FloorY - 12 + 22 * t), new Vector2(Size, Size) * (1 - 0.6f * t), 0,
            CMUHuggerRun.Phosphor.WithAlpha(1 - t));
    }

    private void DrawVent(DrawingHandleScreen handle, float x)
    {
        // the game's vent pump, laid flat in the deck, with a pulsing arrow and the key to press over it
        var left = x - VentWidth / 2;
        Run.Fill(handle, left - 2, FloorY + 1, VentWidth + 4, 12, Color.Black.WithAlpha(0.6f));
        Run.DrawSprite(handle, Run.Frame(Pump, "on", RsiDirection.South, (int) (Run.Clock / 0.1f)), new Vector2(x, FloorY + 8),
            new Vector2(VentWidth + 8, 22), 0, CMUHuggerRun.Phosphor);
        var pulse = 0.5f + 0.5f * MathF.Sin(Run.Clock * 8);
        Run.Ellipse(handle, new Vector2(x, FloorY + 8), VentWidth * 0.7f, 7, CMUHuggerRun.Acid.WithAlpha(0.1f + 0.12f * pulse));
        var bob = MathF.Sin(Run.Clock * 6) * 3;
        Run.Quad(handle, new Vector2(x - 7, FloorY - 40 + bob), new Vector2(x + 7, FloorY - 40 + bob), new Vector2(x, FloorY - 30 + bob),
            new Vector2(x, FloorY - 30 + bob), CMUHuggerRun.Acid.WithAlpha(0.6f + 0.4f * pulse));
        Run.CentreText(handle, Run.TextFont, Loc.GetString("cmu-game-hugger-larva-dive"), x, FloorY - 58 + bob, CMUHuggerRun.Acid);
    }

    /// <summary>
    /// A full airlock: a frame of posts and a lintel with a warning beacon, and the Almayer blast door as its slab. As
    /// the larva closes in the beacon flashes, then the slab falls from the lintel and jams just short of the deck,
    /// leaving the gap to duck through; once the larva is through it slams the rest of the way in sparks.
    /// </summary>
    private void MoveAirlock(ref Obstacle obstacle, float x, float dt)
    {
        obstacle.Timer += dt;

        // timed by when the larva will get there, so the drop always lands first
        var eta = (x - PlayerX) / MathF.Max(1, Speed);
        switch (obstacle.Phase)
        {
            case AirlockPhase.Raised:
                if (eta > AirlockWarn || x > CMUHuggerRun.ScreenWidth + 10)
                    return;

                obstacle.Phase = AirlockPhase.Warning;
                obstacle.Timer = 0;
                Run.Play(Alert, -6, 1.1f);
                return;
            case AirlockPhase.Warning:
                if (eta > AirlockDrop)
                    return;

                obstacle.Phase = AirlockPhase.Falling;
                obstacle.Speed = 0;
                Run.Play(Drop, -4, 0.8f);
                return;
            case AirlockPhase.Falling:
                obstacle.Speed += AirlockFall * dt;
                obstacle.Bottom = MathF.Min(FloorY - AirlockGap, obstacle.Bottom + obstacle.Speed * dt);
                if (obstacle.Bottom < FloorY - AirlockGap)
                    return;

                obstacle.Phase = AirlockPhase.Jammed;
                Impact(obstacle, x, 1.2f);
                return;
            case AirlockPhase.Jammed:
                // stuck, grinding against something in the track
                if (Run.Fx() < dt * 6)
                    Sparks(obstacle, x, 3);

                // only once the larva's whole body is through
                if (x + AirlockWidth >= PlayerX - HalfWidth - 6)
                    return;

                obstacle.Phase = AirlockPhase.Shut;
                obstacle.Speed = 600;
                return;
            case AirlockPhase.Shut:
                if (obstacle.Bottom >= FloorY)
                    return;

                obstacle.Speed += AirlockFall * dt;
                obstacle.Bottom = MathF.Min(FloorY, obstacle.Bottom + obstacle.Speed * dt);
                if (obstacle.Bottom >= FloorY)
                    Impact(obstacle, x, 1.6f);
                return;
        }
    }

    private void Impact(Obstacle obstacle, float x, float strength)
    {
        Run.PlayAny(Thuds, -1 + strength * 2, 0.9f);
        Run.Play(Clang, -6, 0.8f);
        Run.Shake(1.5f * strength, 0.2f);
        Sparks(obstacle, x, 10);
        Run.Emit(World(x + AirlockWidth / 2, FloorY - 2), 6, CMUHuggerRun.Dust.WithAlpha(0.5f), 60, 0.6f, 3, -MathF.PI / 2, MathF.PI, -10,
            kind: CMUHuggerRun.Smoke);
    }

    private void Sparks(Obstacle obstacle, float x, int count)
    {
        for (var side = 0; side < 2; side++)
        {
            var sx = side == 0 ? x + 1 : x + AirlockWidth - 1;
            Run.Emit(World(sx, obstacle.Bottom - 2), count, CMUHuggerRun.Amber, 170, 0.35f, 1, -MathF.PI / 2, 2.4f, 600, floor: FloorY + 2,
                kind: CMUHuggerRun.Spark);
        }

        if (count > 4)
            Run.PlayAny(SparkSounds, -8);
    }

    private void DrawAirlock(DrawingHandleScreen handle, Obstacle obstacle, float x)
    {
        var jitter = obstacle.Phase == AirlockPhase.Jammed ? MathF.Sin(Run.Clock * 70) * 0.6f : 0;
        var bottom = obstacle.Bottom + jitter;
        const float lintel = CeilingY + 6;

        // the slab: the blast door sprite stacked up from its bottom edge, the top one cut off at the lintel
        if (bottom > lintel)
        {
            var slab = Run.Frame(BlastDoor, "closed");
            for (var y = bottom; y > lintel; y -= AirlockWidth)
            {
                var visible = MathF.Min(AirlockWidth, y - lintel);
                var cut = 32 * visible / AirlockWidth;
                Run.DrawRegion(handle, slab, x, y - visible, AirlockWidth, visible, new UIBox2(0, 32 - cut, 32, 32), CMUHuggerCorridorStage.PropTint);
            }

            Run.Fill(handle, x, bottom - 2, AirlockWidth, 2, MetalOutline);
            Run.Fill(handle, x, lintel, 1, bottom - lintel, MetalHigh);
        }

        // the frame: two posts striped at the foot and a lintel carrying the beacon
        for (var side = 0; side < 2; side++)
        {
            var px = side == 0 ? x - 8 : x + AirlockWidth;
            Run.Fill(handle, px, CeilingY - 6, 8, FloorY - CeilingY + 6, MetalMid);
            Run.Fill(handle, px, CeilingY - 6, 1, FloorY - CeilingY + 6, MetalHigh);
            Run.Fill(handle, px + 7, CeilingY - 6, 1, FloorY - CeilingY + 6, MetalOutline);
            for (var k = 0; k < 6; k++)
            {
                var sy = FloorY - 8 - k * 8;
                Run.Quad(handle, new Vector2(px + 1, sy), new Vector2(px + 7, sy - 4), new Vector2(px + 7, sy), new Vector2(px + 1, sy + 4),
                    CMUHuggerRun.Amber.WithAlpha(0.55f));
            }
        }

        Run.Fill(handle, x - 12, CeilingY - 10, AirlockWidth + 24, 16, MetalDark);
        Run.Fill(handle, x - 12, CeilingY - 10, AirlockWidth + 24, 1, MetalHigh);
        Run.Fill(handle, x - 12, CeilingY + 5, AirlockWidth + 24, 1, MetalOutline);
        var alarm = obstacle.Phase is AirlockPhase.Warning or AirlockPhase.Falling or AirlockPhase.Jammed;
        var on = alarm && MathF.Sin(Run.Clock * 22) > 0;
        Run.Circle(handle, new Vector2(x + AirlockWidth / 2, CeilingY - 2), 3.5f, on ? CMUHuggerRun.Alarm : DarkAlarm);
        if (on)
        {
            Run.Ellipse(handle, new Vector2(x + AirlockWidth / 2, CeilingY + 2), 40, 14, CMUHuggerRun.Alarm.WithAlpha(0.12f));
            Run.Quad(handle, new Vector2(x - 6, CeilingY + 6), new Vector2(x + AirlockWidth + 6, CeilingY + 6), new Vector2(x + AirlockWidth + 30, FloorY),
                new Vector2(x - 30, FloorY), CMUHuggerRun.Alarm.WithAlpha(0.04f));
        }

        // say what to do, big the first time
        if (obstacle.Phase != AirlockPhase.Jammed || x <= PlayerX + 10 || Run.Clock % 0.8f >= 0.55f)
            return;

        var centre = x + AirlockWidth / 2;
        Run.CentreText(handle, obstacle.First ? Run.TextFont : Run.SmallFont, Loc.GetString("cmu-game-hugger-larva-duck"), centre,
            bottom - (obstacle.First ? 34 : 24), CMUHuggerRun.Amber);
        if (obstacle.First)
        {
            Run.Quad(handle, new Vector2(centre - 6, bottom - 18), new Vector2(centre + 6, bottom - 18), new Vector2(centre, bottom - 9),
                new Vector2(centre, bottom - 9), CMUHuggerRun.Amber);
        }
    }

    protected override void DrawPlayer(DrawingHandleScreen handle)
    {
        if (Halted)
            return;

        // a single coiled sprite, made to wriggle: it stretches and squeezes as it goes
        var texture = Run.Frame(CMUHuggerRun.Larva, "alive", RsiDirection.East);
        var size = PlayerSize(Size, 0.6f);
        var wriggle = MathF.Sin(Anim * 16);
        if (Grounded)
            size *= new Vector2(1 + 0.08f * wriggle, 1 - 0.08f * wriggle);

        var feet = FloorY - Y;
        Run.DrawSprite(handle, texture, new Vector2(PlayerX, feet + FootPad * size.Y / Size - size.Y / 2), size,
            JumpTilt() + (Grounded ? wriggle * 0.05f : 0), CMUHuggerRun.Phosphor);
    }

    protected override Texture CorpseTexture(bool landed)
    {
        return Run.Frame(CMUHuggerRun.Larva, landed ? "dead" : "alive", RsiDirection.East);
    }

    protected override float CorpseSize => Size;
}
