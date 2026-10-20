using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Stage 1: the facehugger runs the corridor, jumping tracers and ducking high shots, until a human walks in. A held
/// jump that comes down on the face latches on: the host screams, goes over backwards and convulses, a preburst
/// scream, and the chest bursts in a fountain of blood as the larva tumbles out, which hands the run to stage 2.
/// Running into a human's legs gets the hugger kicked flat; a tracer bursts it.
/// </summary>
public sealed class CMUHuggerFacehugger : CMUHuggerCorridorStage
{
    // The parasite's 32 px sprite at 1.4: knee-high to a human, with 3 px of air under its feet in the sprite.
    private const float Size = 44.8f;
    private const float FootPad = 4.2f;

    /// <summary>Feet at least this high when the hugger meets a head: it lands on the face, not the chest.</summary>
    private const float LatchHeight = 56;

    private const float FirstHuman = 12;
    private const float HumanEveryMin = 9;
    private const float HumanEveryMax = 15;
    private const float MarinesFrom = 30;
    private const int ColonistBonus = 1000;
    private const int MarineBonus = 1500;

    // the infection, in seconds from the latch
    private const float Preburst = 1.7f;
    private const float Burst = 2.5f;
    private const float HandOver = 3.7f;

    private const string Pounce = "/Audio/_RMC14/Xeno/alien_pounce.ogg";
    private const string Dies = "/Audio/_RMC14/Xeno/alien_parasite_dies.ogg";
    private const string InfectedMale = "/Audio/_RMC14/Voice/Human/infected_male.ogg";
    private const string InfectedFemale = "/Audio/_RMC14/Voice/Human/infected_female.ogg";

    private static readonly string[] Punches =
    {
        "/Audio/CMU14/PersonalComputers/Games/punch_1.ogg", "/Audio/CMU14/PersonalComputers/Games/punch_2.ogg",
    };

    private static readonly string[] ChestBursts = { "/Audio/_RMC14/Xeno/alien_chestburst.ogg", "/Audio/_RMC14/Xeno/alien_chestburst2.ogg" };

    private static readonly string[] MalePreburst =
    {
        "/Audio/_RMC14/Voice/Human/male_preburst1.ogg", "/Audio/_RMC14/Voice/Human/male_preburst2.ogg",
        "/Audio/_RMC14/Voice/Human/male_preburst3.ogg", "/Audio/_RMC14/Voice/Human/male_preburst4.ogg",
        "/Audio/_RMC14/Voice/Human/male_preburst5.ogg", "/Audio/_RMC14/Voice/Human/male_preburst6.ogg",
        "/Audio/_RMC14/Voice/Human/male_preburst7.ogg", "/Audio/_RMC14/Voice/Human/male_preburst8.ogg",
        "/Audio/_RMC14/Voice/Human/male_preburst9.ogg",
    };

    private static readonly string[] FemalePreburst =
    {
        "/Audio/_RMC14/Voice/Human/female_preburst1.ogg", "/Audio/_RMC14/Voice/Human/female_preburst2.ogg",
        "/Audio/_RMC14/Voice/Human/female_preburst3.ogg", "/Audio/_RMC14/Voice/Human/female_preburst4.ogg",
        "/Audio/_RMC14/Voice/Human/female_preburst5.ogg", "/Audio/_RMC14/Voice/Human/female_preburst6.ogg",
        "/Audio/_RMC14/Voice/Human/female_preburst7.ogg",
    };

    private float _nextShot;
    private float _nextHuman;
    private CMUHuggerHuman? _host;
    private float _scene;
    private bool _preburst;
    private bool _burst;
    private Vector2 _larvaFrom;
    private string? _shotText;

    protected override float HalfWidth => 13;
    protected override float Height => 25;
    protected override float DuckHeight => 13;
    protected override float JumpVelocity => 400;
    protected override float LaneLow => FloorY - 7;
    protected override float LaneMid => FloorY - 21;
    protected override float LaneHigh => FloorY - 48;

    public override string Title => Loc.GetString("cmu-game-hugger-facehugger-title");
    public override string Hint => Loc.GetString("cmu-game-hugger-facehugger-hint");

    public override void Enter()
    {
        base.Enter();
        _nextShot = 1.4f;
        _nextHuman = FirstHuman;
        _host = null;
        _scene = 0;
        _preburst = false;
        _burst = false;
    }

    /// <summary>Bullets in patterns; a human every so often, and while one is ahead the marines hold their fire.</summary>
    protected override void SpawnThreats(float dt)
    {
        _nextShot -= dt;
        _nextHuman -= dt;
        var blocked = HumanAhead();
        if (_nextHuman <= 0 && !blocked && !ShotsQueued && !BulletsAhead())
        {
            var marine = Time >= MarinesFrom && Run.Rng.NextDouble() < 0.4;
            if (SpawnHuman(marine ? CMUHuggerOutfit.Rifleman : CMUHuggerOutfit.Colonist, Run.Fx() < 0.3f) != null)
                Run.PlayAny(Hisses, -12, 1.45f);

            _nextHuman = HumanEveryMin + Run.Fx() * (HumanEveryMax - HumanEveryMin);
            _nextShot = MathF.Max(_nextShot, 1.2f);
            return;
        }

        if (blocked)
        {
            _nextShot = MathF.Max(_nextShot, 0.8f);
            return;
        }

        // let the lane clear for the human that is due
        if (_nextShot > 0 || _nextHuman <= 0)
            return;

        FirePattern();
        _nextShot = FireGap();
    }

    protected override void OnTouch(CMUHuggerHuman human, bool head)
    {
        if (head && Y >= LatchHeight)
        {
            Infect(human);
            return;
        }

        // ran into the legs: punted down the corridor
        human.State = CMUHuggerHumanState.Kick;
        human.Timer = 0;
        Run.PlayAny(Punches);
        Run.Flash(Color.White.WithAlpha(0.2f), 0.12f);
        KillPlayer(new Vector2(PlayerX + 8, FloorY - Y - 12), new Vector2(-1, -0.4f), new Vector2(-40, 420), -14, Dies,
            Loc.GetString("cmu-game-hugger-kicked"), 16);
    }

    protected override void OnShot(Vector2 hit, Vector2 dir)
    {
        ShotDown(hit, dir, Dies, _shotText ??= Loc.GetString("cmu-game-hugger-shot"));
    }

    /// <summary>Came down on a face: the hugger latches on with the game's own infection cry, and the run stops for the scene.</summary>
    private void Infect(CMUHuggerHuman human)
    {
        _host = human;
        _scene = 0;
        human.State = CMUHuggerHumanState.Latched;
        human.Timer = 0;
        human.Infected = true;
        human.Fall = human.East ? -1 : 1;
        Halt();
        var face = Face(human);
        Run.Bonus(human.Marine ? MarineBonus : ColonistBonus, World(face.X, face.Y - 26), CMUHuggerRun.Acid);
        Run.Emit(World(face.X, face.Y), 14, CMUHuggerRun.Blood, 130, 0.9f, 2, -MathF.PI / 2, 2.2f, 520, FloorY + 1, 14, stains: true);
        Run.Play(Pounce);
        Run.Pend(0.1f, human.Person.Female ? InfectedFemale : InfectedMale);
        Run.Flash(Color.White.WithAlpha(0.3f), 0.15f);
        Run.Shake(3, 0.3f);
        Run.SlowMotion(0.3f);
    }

    protected override void UpdateStage(float dt)
    {
        if (_host == null || Run.Over)
            return;

        _scene += dt;
        if (!_preburst && _scene >= Preburst)
        {
            _preburst = true;
            Run.PlayAny(_host.Person.Female ? FemalePreburst : MalePreburst, -3);
        }

        if (!_burst && _scene >= Burst)
            ChestBurst();

        if (_scene >= HandOver)
            Run.NextStage();
    }

    /// <summary>Where the host's chest is once it lies on its back.</summary>
    private Vector2 Chest(CMUHuggerHuman host)
    {
        return new Vector2(host.X - Scroll + host.Fall * 52, FloorY + 2);
    }

    /// <summary>The larva tears its way out of the chest in a fountain of blood.</summary>
    private void ChestBurst()
    {
        if (_host == null)
            return;

        _burst = true;
        _larvaFrom = Chest(_host);
        var at = World(_larvaFrom.X, _larvaFrom.Y - 6);
        Run.Emit(at, 60, CMUHuggerRun.Blood, 300, 1.4f, 2.6f, -MathF.PI / 2, 0.9f, 620, FloorY + 1, 16, stains: true);
        Run.Emit(at, 20, CMUHuggerRun.DarkBlood, 180, 1.2f, 3.4f, -MathF.PI / 2, 1.4f, 620, FloorY + 1, 16, stains: true);
        Run.Emit(at, 8, CMUHuggerRun.Blood.WithAlpha(0.5f), 30, 1, 4, -MathF.PI / 2, 1.2f, -20, kind: CMUHuggerRun.Smoke);
        Run.Splat(new Vector2(at.X, FloorY + 8), 60, CMUHuggerRun.Blood.WithAlpha(0.95f));
        Run.PlayAny(ChestBursts);
        Run.Play(BodyFall, -4, 0.8f);
        Run.Flash(CMUHuggerRun.Blood.WithAlpha(0.4f), 0.35f);
        Run.Shake(5, 0.45f);
        Run.Pop(Loc.GetString("cmu-game-hugger-burst"), at - new Vector2(0, 40), CMUHuggerRun.Alarm, 1.2f);
    }

    /// <summary>The host convulses harder and harder until the burst.</summary>
    protected override float Twitch(CMUHuggerHuman human)
    {
        if (human != _host || _burst)
            return base.Twitch(human);

        var build = Math.Clamp((_scene - 1.0f) / (Burst - 1.0f), 0, 1);
        return MathF.Sin(human.Timer * 47) * 0.05f * build + base.Twitch(human);
    }

    protected override Texture? FaceMask(CMUHuggerHuman human, RsiDirection direction)
    {
        // the hugger stays clamped on until the larva is out
        if (!human.Infected || human == _host && _burst)
            return null;

        var squeeze = human.State == CMUHuggerHumanState.Down ? human.Timer / 0.25f : human.Timer / 0.08f;
        return Run.Frame(CMUHuggerRun.Parasite, "equipped-MASK", direction, (int) squeeze);
    }

    protected override void DrawPlayer(DrawingHandleScreen handle)
    {
        if (Halted)
            return;

        // the leap keeps the running pose and facing, only tilted: nose up on the way up, down on the way down
        var frame = Grounded ? (int) (Anim / 0.1f) : 2;
        var texture = Run.Frame(CMUHuggerRun.Parasite, "running", RsiDirection.East, frame);
        var size = PlayerSize(Size);
        var feet = FloorY - Y;
        Run.DrawSprite(handle, texture, new Vector2(PlayerX, feet + FootPad * size.Y / Size - size.Y / 2), size, JumpTilt(), CMUHuggerRun.Phosphor);
    }

    public override void DrawFront(DrawingHandleScreen handle)
    {
        base.DrawFront(handle);
        if (!_burst || _host == null)
            return;

        // the larva arcs out of the chest and lands in front of the body
        var t = Math.Clamp((_scene - Burst) / 0.55f, 0, 1);
        var to = new Vector2(_larvaFrom.X - 34, FloorY);
        var at = Vector2.Lerp(_larvaFrom, to, t) - new Vector2(0, MathF.Sin(t * MathF.PI) * 34 + 10);
        var larva = Run.Frame(CMUHuggerRun.Larva, "alive", RsiDirection.West);
        Run.DrawSprite(handle, larva, at, new Vector2(40, 40), (1 - t) * -1.2f, CMUHuggerRun.Phosphor);
    }

    protected override Texture CorpseTexture(bool landed)
    {
        return landed
            ? Run.Frame(CMUHuggerRun.Parasite, "dead")
            : Run.Frame(CMUHuggerRun.Parasite, "running", RsiDirection.East, 2);
    }

    protected override float CorpseSize => Size;
}
