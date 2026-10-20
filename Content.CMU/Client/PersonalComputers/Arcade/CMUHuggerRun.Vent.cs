using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Stage 3: a calm crawl down a dark air duct with nothing in it but the larva, fans and dripping water, then the
/// larva stops, shudders inside a glowing cocoon and bursts out of its skin as a runner with a roar. Space skips
/// it once it has had a moment. Nothing here can kill you.
/// </summary>
public sealed class CMUHuggerVent : CMUHuggerStage
{
    private const float DuctTop = 62;
    private const float DuctBottom = 166;
    private const float CrawlSpeed = 70;
    private const float CrawlTime = 5;
    private const float SlowTime = 1.2f;
    private const float SkipAfter = 1;

    // the cutscene, in seconds after the larva stops
    private const float Shed = 1.7f;
    private const float Grown = 2.4f;
    private const float Done = 4.2f;
    private const int EvolveBonus = 1000;

    private const float LarvaSize = 40;
    private const float RunnerScale = 1.4f;

    private const string EvolveReady = "/Audio/_RMC14/Xeno/xeno_evolveready.ogg";
    private const string Squelch = "/Audio/Effects/Footsteps/meatslap.ogg";

    private static readonly string[] Crawl = { "/Audio/_RMC14/Xeno/alien_ventcrawl1.ogg", "/Audio/_RMC14/Xeno/alien_ventcrawl2.ogg" };

    private static readonly string[] Roars =
    {
        "/Audio/_RMC14/Xeno/alien_roar1.ogg", "/Audio/_RMC14/Xeno/alien_roar2.ogg", "/Audio/_RMC14/Xeno/alien_roar3.ogg",
        "/Audio/_RMC14/Xeno/alien_roar4.ogg", "/Audio/_RMC14/Xeno/alien_roar5.ogg", "/Audio/_RMC14/Xeno/alien_roar6.ogg",
    };


    private float _time;
    private float _scroll;
    private float _speed;
    private float _scene = -1;
    private float _crawlSound;
    private bool _shed;
    private bool _rewarded;
    private float _larvaX;

    public override string Title => Loc.GetString("cmu-game-hugger-vent-title");
    public override string Hint => Loc.GetString("cmu-game-hugger-vent-hint");

    public override void Enter()
    {
        _time = 0;
        _scroll = 0;
        _speed = CrawlSpeed;
        _scene = -1;
        _crawlSound = 0.4f;
        _shed = false;
        _rewarded = false;
        _larvaX = 90;
    }

    public override void OnKey(CMUArcadeInput input)
    {
        if (!input.Down || input.Key != CMUArcadeKey.Action || _time < SkipAfter)
            return;

        Finish();
    }

    /// <summary>Done or skipped: the runner gets its bonus and the run goes on.</summary>
    private void Finish()
    {
        if (!_rewarded)
        {
            _rewarded = true;
            Run.AddScore(EvolveBonus);
        }

        Run.NextStage();
    }

    public override void Update(float dt)
    {
        _time += dt;
        if (_time > CrawlTime)
            _speed = MathF.Max(0, _speed - CrawlSpeed / SlowTime * dt);

        _scroll += _speed * dt;
        Run.Camera = new Vector2(_scroll, 0);

        // drips from the duct roof
        if (Run.Fx() < dt * 3)
        {
            Run.Emit(new Vector2(_scroll + Run.Fx() * CMUHuggerRun.ScreenWidth, DuctTop + 4), 1, CMUHuggerRun.Bright.WithAlpha(0.5f), 5, 1.2f, 1.4f,
                MathF.PI / 2, 0.1f, 300, DuctBottom - 1);
        }

        if (_scene < 0)
        {
            // walk the larva to the middle as the crawl slows
            _larvaX = MathF.Min(160, _larvaX + (_time > CrawlTime ? 60 : 0) * dt);
            _crawlSound -= dt;
            if (_crawlSound <= 0 && _speed > 5)
            {
                _crawlSound = 1.1f + Run.Fx() * 0.6f;
                Run.PlayAny(Crawl, -8);
            }

            if (_time >= CrawlTime + SlowTime && _larvaX >= 160)
                StartScene();

            return;
        }

        _scene += dt;
        var at = new Vector2(_scroll + _larvaX, DuctBottom - 14);
        if (_scene < Shed && Run.Fx() < dt * 10)
            Run.Emit(at, 1, CMUHuggerRun.Acid.WithAlpha(0.4f), 20, 0.9f, 3, -MathF.PI / 2, 1.4f, -25, kind: CMUHuggerRun.Smoke);

        if (!_shed && _scene >= Shed)
        {
            _shed = true;
            Run.Emit(at, 40, CMUHuggerRun.Acid, 220, 1.1f, 2.4f, -MathF.PI / 2, 2.6f, 600, DuctBottom - 1, 4, stains: true, acid: true);
            Run.Emit(at, 12, CMUHuggerRun.Acid.WithAlpha(0.4f), 40, 1.2f, 4, -MathF.PI / 2, 2, -20, kind: CMUHuggerRun.Smoke);
            Run.Flash(Color.White.WithAlpha(0.6f), 0.4f);
            Run.Shake(4.5f, 0.5f);
            Run.Play(Squelch, -2, 0.8f);
            Run.PlayAny(Roars);
        }

        if (!_rewarded && _scene >= Grown)
        {
            _rewarded = true;
            Run.Bonus(EvolveBonus, at - new Vector2(0, 60), CMUHuggerRun.Acid);
        }

        if (_scene >= Done)
            Finish();
    }

    private void StartScene()
    {
        _scene = 0;
        Run.Play(EvolveReady);
    }

    /// <summary>
    /// The duct as one side-view piece in the corridor's metal palette: a corrugated far wall with the odd fan behind,
    /// a riveted roof and floor joined by flanged rings, and grates in the roof letting light fall to the floor.
    /// </summary>
    public override void DrawBack(DrawingHandleScreen handle)
    {
        const float w = CMUHuggerRun.ScreenWidth;
        Run.Fill(handle, 0, CMUHuggerRun.HudHeight, w, CMUHuggerRun.ScreenHeight - CMUHuggerRun.HudHeight, Color.Black);

        // the far wall: corrugated sheet, darker into its corners
        var back = _scroll * 0.6f;
        Run.Fill(handle, 0, DuctTop, w, DuctBottom - DuctTop, CMUHuggerCorridorStage.MetalMid);
        for (var c = (int) MathF.Floor(back / 10); c * 10 - back < w; c++)
        {
            var x = c * 10 - back;
            Run.Fill(handle, x, DuctTop, 4, DuctBottom - DuctTop, CMUHuggerCorridorStage.MetalDark);
            Run.Fill(handle, x + 4, DuctTop, 1, DuctBottom - DuctTop, CMUHuggerCorridorStage.MetalMain);
        }

        for (var c = (int) MathF.Floor(back / 52); c * 52 - back < w; c++)
        {
            if (CMUHuggerRun.Hash(c) % 5 != 0)
                continue;

            var centre = new Vector2(c * 52 - back + 26, (DuctTop + DuctBottom) / 2);
            Run.Circle(handle, centre, 30, CMUHuggerCorridorStage.MetalOutline);
            Run.Circle(handle, centre, 29, CMUHuggerCorridorStage.MetalHigh, false);
            Run.Circle(handle, centre, 26, Color.Black);
            var spin = Run.Clock * 5 + c;
            for (var blade = 0; blade < 4; blade++)
            {
                var angle = spin + blade * MathF.PI / 2;
                Run.Beam(handle, centre, centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 23, 7, CMUHuggerCorridorStage.MetalLight);
            }

            Run.Circle(handle, centre, 4, CMUHuggerCorridorStage.MetalHigh);
        }

        Run.Fill(handle, 0, DuctTop, w, 12, CMUHuggerCorridorStage.MetalOutline.WithAlpha(0.55f));
        Run.Fill(handle, 0, DuctBottom - 10, w, 10, CMUHuggerCorridorStage.MetalOutline.WithAlpha(0.45f));

        // the roof and floor plates, riveted, with a flanged ring at every joint and grates in the roof
        Plate(handle, DuctTop - 16, 16);
        Plate(handle, DuctBottom, 20);
        for (var c = (int) MathF.Floor(_scroll / 24); c * 24 - _scroll < w; c++)
        {
            var x = c * 24 - _scroll;
            Run.Fill(handle, x, DuctTop - 9, 1, 1, CMUHuggerCorridorStage.MetalHigh);
            Run.Fill(handle, x, DuctBottom + 9, 1, 1, CMUHuggerCorridorStage.MetalHigh);
        }

        for (var c = (int) MathF.Floor(_scroll / 120) - 1; c * 120 - _scroll < w + 40; c++)
        {
            var x = c * 120 - _scroll;

            // a joint ring: a flange on the roof and floor, and the seam down the far wall
            Run.Fill(handle, x, DuctTop - 18, 8, 20, CMUHuggerCorridorStage.MetalLight);
            Run.Fill(handle, x, DuctTop - 18, 8, 1, CMUHuggerCorridorStage.MetalRim);
            Run.Fill(handle, x, DuctBottom - 2, 8, 24, CMUHuggerCorridorStage.MetalLight);
            Run.Fill(handle, x, DuctBottom - 2, 8, 1, CMUHuggerCorridorStage.MetalRim);
            Run.Fill(handle, x + 2, DuctTop + 2, 4, DuctBottom - DuctTop - 4, CMUHuggerCorridorStage.MetalDark.WithAlpha(0.6f));
            if (CMUHuggerRun.Hash(c + 7) % 3 != 0)
                continue;

            // a grate in the roof, and the light falling through it
            var gx = x + 50;
            Run.Fill(handle, gx, DuctTop - 14, 30, 13, Color.Black);
            for (var k = 0; k < 6; k++)
            {
                Run.Fill(handle, gx + 2 + k * 5, DuctTop - 13, 2, 11, CMUHuggerCorridorStage.MetalLight);
            }

            Run.Quad(handle, new Vector2(gx, DuctTop), new Vector2(gx + 30, DuctTop), new Vector2(gx + 52, DuctBottom), new Vector2(gx - 22, DuctBottom),
                CMUHuggerRun.PhosphorGreen.WithAlpha(0.06f));
            Run.Ellipse(handle, new Vector2(gx + 15, DuctBottom + 3), 40, 5, CMUHuggerRun.PhosphorGreen.WithAlpha(0.1f));
        }

        // standing water on the floor here and there
        for (var c = (int) MathF.Floor(_scroll / 90); c * 90 - _scroll < w + 30; c++)
        {
            if (CMUHuggerRun.Hash(c + 31) % 4 == 0)
                Run.Ellipse(handle, new Vector2(c * 90 - _scroll + 30, DuctBottom + 4), 22, 2.5f, CMUHuggerRun.Bright.WithAlpha(0.12f));
        }

        Run.Fill(handle, 0, DuctBottom, w, 1.5f, CMUHuggerRun.PhosphorGreen.WithAlpha(0.6f));
    }

    private void Plate(DrawingHandleScreen handle, float top, float height)
    {
        Run.Fill(handle, 0, top, CMUHuggerRun.ScreenWidth, height, CMUHuggerCorridorStage.MetalMain);
        Run.Fill(handle, 0, top, CMUHuggerRun.ScreenWidth, 1, CMUHuggerCorridorStage.MetalHigh);
        Run.Fill(handle, 0, top + height - 1, CMUHuggerRun.ScreenWidth, 1, CMUHuggerCorridorStage.MetalOutline);
    }

    public override void DrawFront(DrawingHandleScreen handle)
    {
        var feet = DuctBottom;
        var x = _larvaX;
        if (_scene < 0)
        {
            var wriggle = MathF.Sin(_time * 14);
            var size = new Vector2(LarvaSize * (1 + 0.08f * wriggle), LarvaSize * (1 - 0.08f * wriggle));
            Run.Ellipse(handle, new Vector2(x, feet + 1), 13, 3, Color.Black.WithAlpha(0.5f));
            Run.DrawSprite(handle, Run.Frame(CMUHuggerRun.Larva, "alive", RsiDirection.East), new Vector2(x, feet + 3.75f - size.Y / 2), size,
                wriggle * 0.05f, CMUHuggerRun.Phosphor);
            return;
        }

        var t = _scene;
        var glow = 0.5f + 0.5f * MathF.Sin(t * (6 + t * 6));
        if (t < Shed)
        {
            // shuddering in a glowing cocoon, swelling
            var grow = 1 + 0.35f * t / Shed;
            var jitter = new Vector2((Run.Fx() - 0.5f) * 4 * t, (Run.Fx() - 0.5f) * 2 * t);
            Run.Ellipse(handle, new Vector2(x, feet - 14), 30 * grow, 22 * grow, CMUHuggerRun.Acid.WithAlpha(0.08f + 0.18f * glow * t / Shed));
            var size = new Vector2(LarvaSize, LarvaSize) * grow;
            Run.DrawSprite(handle, Run.Frame(CMUHuggerRun.Larva, "alive", RsiDirection.East), new Vector2(x, feet - size.Y / 2 + 3.75f) + jitter, size, 0,
                Color.InterpolateBetween(CMUHuggerRun.Phosphor, CMUHuggerRun.Acid, glow * t / Shed));
            return;
        }

        // the shed skin lies where the larva was, and the runner rises out of it
        Run.DrawSprite(handle, Run.Frame(CMUHuggerRun.Larva, "dead", RsiDirection.East), new Vector2(x - 26, feet - 18), new Vector2(LarvaSize, LarvaSize) * 1.3f, 0,
            Color.FromHex("#5A6A48"));
        var rise = Math.Clamp((t - Shed) / (Grown - Shed), 0, 1);
        var scale = RunnerScale * (0.55f + 0.45f * rise);
        var box = 64 * scale;
        var runner = Run.Frame(CMUHuggerRun.Runner, "alive", RsiDirection.East);
        Run.Ellipse(handle, new Vector2(x + 6, feet + 1), 34 * scale / RunnerScale, 4, Color.Black.WithAlpha(0.5f));

        // the runner sprite's feet sit 18 px above the bottom of its 64 px frame
        Run.DrawSprite(handle, runner, new Vector2(x + 6, feet + 18 * scale - box / 2), new Vector2(box, box), 0,
            Color.InterpolateBetween(Color.White, CMUHuggerRun.Phosphor, rise));
    }

    public override void DrawOverlay(DrawingHandleScreen handle)
    {
        if (_scene >= Grown && _scene < Done)
        {
            var alpha = Math.Clamp((_scene - Grown) / 0.3f, 0, 1);
            Run.CentreText(handle, Run.BigFont, Loc.GetString("cmu-game-hugger-vent-evolved"), CMUHuggerRun.ScreenWidth / 2, 28, CMUHuggerRun.Acid.WithAlpha(alpha));
        }

        if (_time >= SkipAfter && Run.Clock % 1.1f < 0.75f)
            Run.CentreText(handle, Run.SmallFont, Loc.GetString("cmu-game-hugger-vent-skip"), CMUHuggerRun.ScreenWidth / 2, 202, CMUHuggerRun.Bright);
    }
}
