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
    private const float VentWidth = 44;
    private const float DiveReach = 24;
    private const float DiveTime = 0.75f;
    private const int VentBonus = 500;

    private const string Cry = "/Audio/_RMC14/Xeno/alien_roar_larva1.ogg";
    private const string Squeal = "/Audio/_RMC14/Xeno/alien_roar_larva2.ogg";
    private const string Steam = "/Audio/Effects/extinguish.ogg";
    private const string Clang = "/Audio/Effects/clang.ogg";
    private const string VentIn = "/Audio/_RMC14/Xeno/alien_ventpass1.ogg";
    private const string Shutter = Structures + "Doors/Shutters/Almayer/shutter.rsi";
    private const string Pump = Structures + "Piping/Atmospherics/vent_pump.rsi";

    /// <summary>The shutter is a 32 px sprite at 1.44, its closed slats ending 28 px down.</summary>
    private const float ShutterSize = 46;
    private const float ShutterSlats = 28;

    private struct Obstacle
    {
        public bool Active;
        public bool Vent;
        public float X;
        public bool Heard;

        /// <summary>Seconds since the shutter slammed shut behind the larva, or -1.</summary>
        public float Slam;
    }

    private readonly Obstacle[] _obstacles = new Obstacle[3];
    private float _nextThreat;
    private float _nextShot;
    private float _nextVent;
    private float _dive = -1;
    private float _diveX;
    private string? _shotText;

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

    private void AddObstacle(bool vent)
    {
        for (var i = 0; i < _obstacles.Length; i++)
        {
            if (_obstacles[i].Active)
                continue;

            _obstacles[i] = new Obstacle { Active = true, Vent = vent, X = Scroll + CMUHuggerRun.ScreenWidth + 40, Slam = -1 };
            return;
        }
    }

    /// <summary>One threat at a time: a knifeman, an airlock or a fleeing colonist, with tracers between; the vent clears the way.</summary>
    protected override void SpawnThreats(float dt)
    {
        _nextThreat -= dt;
        _nextShot -= dt;
        _nextVent -= dt;
        var busy = HumanAhead() || ObstacleAhead(out _);
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

            if (!Overlap(left, right, top, bottom, x, x + AirlockWidth, CeilingY, FloorY - AirlockGap))
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

            // through: the shutter slams down behind the larva
            if (!obstacle.Vent && obstacle.Slam < 0 && x + AirlockWidth < PlayerX - 8)
            {
                obstacle.Slam = 0;
                Run.Play(Clang, -6, 0.9f);
                Run.Shake(1.5f, 0.15f);
            }
            else if (obstacle.Slam >= 0)
            {
                obstacle.Slam += dt;
            }

            // steam hisses out from under a closed airlock
            if (!obstacle.Vent && Run.Fx() < dt * 14)
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
    /// A bulkhead of reinforced wall with the Almayer shutter in it. Coming into view the shutter rolls down through
    /// its closing frames and jams just short of the deck; once the larva is through it slams the rest of the way.
    /// </summary>
    private void DrawAirlock(DrawingHandleScreen handle, Obstacle obstacle, float x)
    {
        var closing = Math.Clamp((230 - x) / 50, 0, 1);
        var slam = obstacle.Slam >= 0 ? MathF.Min(1, obstacle.Slam / 0.1f) * AirlockGap : 0;
        var bottom = FloorY - AirlockGap + slam;
        var top = bottom - ShutterSlats * ShutterSize / 32;
        var centre = x + AirlockWidth / 2;

        var wall = Run.Frame(ReinforcedWall, "full");
        for (var y = top - AirlockWidth; y > CeilingY - AirlockWidth * 2; y -= AirlockWidth)
        {
            Run.DrawSprite(handle, wall, new Vector2(centre, y + AirlockWidth / 2), new Vector2(AirlockWidth, AirlockWidth), 0,
                CMUHuggerCorridorStage.WallTint);
        }

        var state = closing <= 0 ? "open" : closing < 1 ? "closing" : "closed";
        var shutter = Run.Frame(Shutter, state, RsiDirection.South, (int) (closing * 5.99f));
        Run.DrawSprite(handle, shutter, new Vector2(centre, top + ShutterSize / 2), new Vector2(AirlockWidth, ShutterSize), 0,
            CMUHuggerCorridorStage.PropTint);
        Run.Fill(handle, x - 3, CeilingY, 3, FloorY - CeilingY, Color.Black.WithAlpha(0.6f));
        Run.Fill(handle, x + AirlockWidth, CeilingY, 3, FloorY - CeilingY, Color.Black.WithAlpha(0.6f));
        var blink = Run.Clock % 0.8f < 0.45f;
        Run.Circle(handle, new Vector2(centre, top - 8), 2.5f, CMUHuggerRun.Alarm.WithAlpha(blink ? 1 : 0.25f));

        // a hint for the gap while it is coming
        if (x > PlayerX + 10 && x < CMUHuggerRun.ScreenWidth - 20 && blink && obstacle.Slam < 0)
            Run.CentreText(handle, Run.SmallFont, Loc.GetString("cmu-game-hugger-larva-duck"), centre, top - 30, CMUHuggerRun.Amber);
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
