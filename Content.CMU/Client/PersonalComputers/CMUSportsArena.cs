using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.Station;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Plays a sportsbook match live with dressed dolls and real item sprites. Before kickoff the lineup waits on the
/// field; from kickoff everyone watches the same match, worked out from the seed and winner the server reveals.
/// </summary>
public sealed class CMUSportsArena : LayoutContainer
{
    public const float ArenaWidth = 520;
    public const float ArenaHeight = 240;
    private const float Bar = 26;

    private static readonly EntProtoId Ball = "Basketball";
    private static readonly EntProtoId Baseball = "TennisBall";
    private static readonly EntProtoId Bat = "BaseBallBat";
    private static readonly string[] Hits = { "POW!", "BAM!", "WHAM!", "THUD!", "SMACK!" };

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;

    private readonly List<Doll> _dolls = new();
    private readonly List<(string Text, Vector2 Position, Color Colour, bool Big, bool Right)> _texts = new();
    private readonly TextLayer _layer;
    private readonly EntityPrototypeView _ball;
    private readonly EntityPrototypeView _bat;
    private readonly Font _big;
    private readonly Font _small;
    private CMUBetMatch? _match;
    private CMUSportsPlay? _play;
    private TimeSpan _start;
    private string _header = string.Empty;
    private string _clock = string.Empty;

    private sealed class Doll(SpriteView view, string kit)
    {
        public readonly SpriteView View = view;
        public readonly string Kit = kit;
        public EntityUid? Dummy;
    }

    public CMUSportsArena()
    {
        IoCManager.InjectDependencies(this);
        SetSize = new Vector2(ArenaWidth, ArenaHeight);
        HorizontalAlignment = HAlignment.Left;
        RectClipContent = true;
        _big = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 18);
        _small = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 10);
        _ball = new EntityPrototypeView(Ball, _entities) { SetSize = new Vector2(24, 24), Visible = false };
        _bat = new EntityPrototypeView(Bat, _entities) { SetSize = new Vector2(40, 40), Visible = false };
        _layer = new TextLayer(this) { SetSize = new Vector2(ArenaWidth, ArenaHeight) };
    }

    public CMUBetMatch? Match => _match;

    /// <summary>Shows a match, live from its kickoff, or from now when <paramref name="replay"/> is set.</summary>
    public void Show(CMUBetMatch match, bool replay = false)
    {
        var known = _match != null && _match.Id == match.Id && _match.Winner == match.Winner;
        _match = match;
        if (known && !replay)
            return;

        _play = match.Winner >= 0 ? CMUSports.Play(match.Sport, match.Seed, match.Entrants.Count, match.Winner) : null;
        _start = replay ? _timing.CurTime : match.Kickoff;
        Rebuild();
    }

    private void Rebuild()
    {
        ClearDolls();
        RemoveAllChildren();
        if (_match is not { } match)
            return;

        switch (match.Sport)
        {
            case CMUSport.Sprint:
                for (var lane = 0; lane < match.Entrants.Count; lane++)
                    AddDoll("Runner", CMUSports.Colour(match.Sport, lane, match.Entrants[lane]), 1.25f);
                break;
            case CMUSport.Boxing:
                AddDoll("Boxer", 0, 2f);
                AddDoll("Boxer", 1, 2f);
                break;
            case CMUSport.Baseball:
                // per side: pitcher, batter, then runners on first, second and third
                for (var side = 0; side < 2; side++)
                {
                    for (var i = 0; i < 5; i++)
                        AddDoll("Player", CMUSports.Colour(match.Sport, side, match.Entrants[side]), 1.25f);
                }
                break;
            case CMUSport.ZeroGBall:
                for (var side = 0; side < 2; side++)
                {
                    for (var i = 0; i < CMUSports.Squad; i++)
                        AddDoll("Spacer", CMUSports.Colour(match.Sport, side, match.Entrants[side]), 1.25f);
                }
                break;
        }

        _ball.SetPrototype(match.Sport == CMUSport.Baseball ? Baseball : Ball);
        _ball.Scale = match.Sport == CMUSport.Baseball ? new Vector2(0.75f, 0.75f) : Vector2.One;
        AddChild(_bat);
        AddChild(_ball);
        AddChild(_layer);
        if (IsInsideTree)
            SpawnDolls();
    }

    private void AddDoll(string role, int colour, float scale)
    {
        var view = new SpriteView(_entities)
        {
            Scale = new Vector2(scale, scale),
            SetSize = new Vector2(32 * scale, 32 * scale),
            OverrideDirection = Direction.South,
        };
        _dolls.Add(new Doll(view, CMUSports.Kit(role, colour)));
        AddChild(view);
    }

    // Dolls live only while the arena is on screen, like the ads.
    protected override void EnteredTree()
    {
        base.EnteredTree();
        SpawnDolls();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        ClearDolls(false);
    }

    private void SpawnDolls()
    {
        var doll = _prototypes.Index(HumanoidCharacterProfile.DefaultSpecies).DollPrototype;
        var spawning = _entities.System<StationSpawningSystem>();
        foreach (var entry in _dolls)
        {
            if (entry.Dummy != null)
                continue;

            var dummy = _entities.SpawnEntity(doll, MapCoordinates.Nullspace);
            if (_prototypes.HasIndex<Content.Shared.Roles.StartingGearPrototype>(entry.Kit))
                spawning.EquipStartingGear(dummy, entry.Kit);

            entry.Dummy = dummy;
            entry.View.SetEntity(dummy);
        }
    }

    private void ClearDolls(bool forget = true)
    {
        foreach (var entry in _dolls)
        {
            if (entry.Dummy is not { } dummy)
                continue;

            entry.View.SetEntity(null);
            _entities.QueueDeleteEntity(dummy);
            entry.Dummy = null;
        }

        if (forget)
            _dolls.Clear();
    }

    private void Place(Control control, float x, float y)
    {
        control.Visible = true;
        SetPosition(control, new Vector2(x - control.SetSize.X / 2, y - control.SetSize.Y / 2));
    }

    private void Pose(int doll, float x, float y, Direction direction, float rotation = 0)
    {
        if (doll >= _dolls.Count)
            return;

        var view = _dolls[doll].View;
        view.OverrideDirection = direction;
        view.WorldRotation = rotation == 0 ? null : Angle.FromDegrees(rotation);
        Place(view, x, y);
    }

    private void Hide(int doll)
    {
        if (doll < _dolls.Count)
            _dolls[doll].View.Visible = false;
    }

    /// <summary>Big calls are centred on their spot; labels hang from it, or end at it when right aligned.</summary>
    private void Say(string text, float x, float y, Color colour, bool big = false, bool right = false)
    {
        _texts.Add((text, new Vector2(x, y), colour, big, right));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _texts.Clear();
        if (_match is not { } match)
            return;

        var t = (float) (_timing.CurTime - _start).TotalSeconds;
        var length = CMUSports.Length(match.Sport);
        _header = Loc.GetString($"cmu-sport-{match.Sport}").ToUpperInvariant();
        _clock = _play == null
            ? Loc.GetString("cmu-sport-starts", ("seconds", Math.Max(0, (int) -t)))
            : t >= length ? Loc.GetString("cmu-sport-final") : $"{(int) t / 60}:{(int) t % 60:00}";

        _bat.Visible = false;
        _ball.Visible = false;
        var play = _play;
        var time = play == null ? -1 : Math.Min(t, length);
        switch (match.Sport)
        {
            case CMUSport.Sprint:
                UpdateSprint(match, play, time);
                break;
            case CMUSport.Boxing:
                UpdateBoxing(match, play, time);
                break;
            case CMUSport.Baseball:
                UpdateBaseball(match, play, time);
                break;
            case CMUSport.ZeroGBall:
                UpdateZeroG(match, play, time);
                break;
        }

        if (play != null && t >= length)
        {
            var winner = match.Name(match.Winner);
            Say(Loc.GetString("cmu-sport-winner", ("name", winner)), ArenaWidth / 2, ArenaHeight - 30, Color.Gold, true);
        }
    }

    #region Sprint

    private const float TrackTop = Bar + 10;
    private const float Lane = 38;
    private const float StartLine = 110;
    private const float FinishLine = 470;

    private void UpdateSprint(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        var running = t - CMUSports.SprintStart;
        var finished = new List<int>();
        for (var lane = 0; lane < match.Entrants.Count; lane++)
        {
            var y = TrackTop + Lane * lane + Lane / 2 - 6;
            var x = StartLine - 6;
            if (play != null && running > 0)
            {
                var finish = play.Score[lane] / 100f;
                var progress = Math.Clamp(running / finish, 0, 1);
                var distance = progress - 0.1f * MathF.Sin(MathF.PI * progress);
                x = StartLine + distance * (FinishLine - StartLine);
                if (running >= finish)
                {
                    x = FinishLine + Math.Min(1, running - finish) * 22;
                    finished.Add(lane);
                }
                else
                {
                    y -= MathF.Abs(MathF.Sin(running * 14 + lane)) * 3;
                }
            }

            Pose(lane, x, y, Direction.East);
            Say(match.Name(lane), 6, TrackTop + Lane * lane + 2, Color.White.WithAlpha(0.75f));
        }

        if (play == null || running < 0)
        {
            var call = play == null || running < -4 ? "cmu-sport-marks" : running < -1 ? "cmu-sport-set" : null;
            if (call != null)
                Say(Loc.GetString(call), ArenaWidth / 2, ArenaHeight / 2, Color.White, true);

            return;
        }

        if (running < 1)
            Say(Loc.GetString("cmu-sport-go"), ArenaWidth / 2, ArenaHeight / 2, Color.Lime, true);
        else if (finished.Count == 0)
            _clock = $"{running:0.00}";

        var order = finished.OrderBy(lane => play.Score[lane]).ToList();
        for (var place = 0; place < order.Count; place++)
        {
            var lane = order[place];
            Say($"{place + 1}. {play.Score[lane] / 100f:0.00}", FinishLine - 70, TrackTop + Lane * lane + 2,
                place == 0 ? Color.Gold : Color.White);
        }
    }

    private void DrawSprint(DrawingHandleScreen handle)
    {
        Rect(handle, 0, Bar, ArenaWidth, ArenaHeight - Bar, Color.FromHex("#2E5A2A"));
        var lanes = _match?.Entrants.Count ?? 5;
        Rect(handle, 0, TrackTop, ArenaWidth, Lane * lanes, Color.FromHex("#B5482F"));
        for (var i = 0; i <= lanes; i++)
            Rect(handle, 0, TrackTop + Lane * i - 1, ArenaWidth, 2, Color.White.WithAlpha(0.8f));

        Rect(handle, StartLine - 2, TrackTop, 3, Lane * lanes, Color.White);
        for (var y = 0; y < Lane * lanes / 6; y++)
        {
            for (var x = 0; x < 2; x++)
                Rect(handle, FinishLine + x * 6, TrackTop + y * 6, 6, 6, (x + y) % 2 == 0 ? Color.White : Color.Black);
        }
    }

    #endregion

    #region Boxing

    private const float RingFloor = 132;
    private const float BoxerY = 160;

    private void UpdateBoxing(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        Say(match.Name(0), 40, Bar + 6, Color.FromHex("#FF6B6B"));
        Say(match.Name(1), ArenaWidth - 40, Bar + 6, Color.FromHex("#6BA8FF"), right: true);
        if (play == null || t < 4)
        {
            Pose(0, 80, BoxerY, Direction.East);
            Pose(1, ArenaWidth - 80, BoxerY, Direction.West);
            if (play != null)
                Say(Loc.GetString("cmu-sport-round", ("round", 1)), ArenaWidth / 2, 80, Color.White, true);

            return;
        }

        var round = Math.Min(CMUSports.BoxingRounds, (int) ((t - 4) / (CMUSports.RoundLength + CMUSports.RoundRest)) + 1);
        var into = t - 4 - (round - 1) * (CMUSports.RoundLength + CMUSports.RoundRest);
        var landed = new int[2];
        float? down = null;
        var downSide = -1;
        var knockedOut = false;
        foreach (var ev in play.Events)
        {
            if (ev.Time > t)
                break;

            if (ev.Action == CMUSportsAction.Punch && ev.Value == 1)
                landed[ev.Side]++;
            else if (ev.Action is CMUSportsAction.Knockdown or CMUSportsAction.Knockout && (ev.Action == CMUSportsAction.Knockout || t < ev.Time + 4))
            {
                down = t - ev.Time;
                downSide = ev.Side;
                knockedOut = ev.Action == CMUSportsAction.Knockout;
            }
        }

        _clock = Loc.GetString("cmu-sport-round-clock", ("round", round), ("clock", $"{(int) Math.Min(into, CMUSports.RoundLength) / 60}:{(int) Math.Min(into, CMUSports.RoundLength) % 60:00}"));
        Say(Loc.GetString("cmu-sport-landed", ("count", landed[0])), 40, Bar + 20, Color.White.WithAlpha(0.8f));
        Say(Loc.GetString("cmu-sport-landed", ("count", landed[1])), ArenaWidth - 40, Bar + 20, Color.White.WithAlpha(0.8f), right: true);

        var resting = into > CMUSports.RoundLength && !knockedOut;
        if (resting || knockedOut && down > 6)
        {
            Pose(0, 80, BoxerY, Direction.East);
            Pose(1, ArenaWidth - 80, BoxerY, Direction.West);
            if (knockedOut)
                Pose(downSide, downSide == 0 ? 80 : ArenaWidth - 80, BoxerY + 18, Direction.South, downSide == 0 ? -90 : 90);

            if (resting && round < CMUSports.BoxingRounds)
                Say(Loc.GetString("cmu-sport-round", ("round", round + 1)), ArenaWidth / 2, 80, Color.White, true);
            else if (knockedOut)
                Say(Loc.GetString("cmu-sport-ko"), ArenaWidth / 2, 80, Color.Red, true);

            return;
        }

        var centre = ArenaWidth / 2 + MathF.Sin(t * 0.7f) * 50;
        var x = new[] { centre - 36 + MathF.Sin(t * 3) * 4, centre + 36 + MathF.Sin(t * 3 + 1.5f) * 4 };
        var y = new[] { BoxerY + MathF.Sin(t * 5) * 2, BoxerY + MathF.Sin(t * 5 + 2) * 2 };
        var index = 0;
        foreach (var ev in play.Events)
        {
            index++;
            if (ev.Action != CMUSportsAction.Punch || ev.Time > t || t > ev.Time + 0.6f)
                continue;

            var age = t - ev.Time;
            var towards = ev.Side == 0 ? 1 : -1;
            if (age < 0.35f)
                x[ev.Side] += towards * 18 * MathF.Sin(MathF.PI * age / 0.35f);

            if (ev.Value != 1 || age < 0.12f)
                continue;

            var defender = 1 - ev.Side;
            x[defender] += towards * 10 * MathF.Sin(MathF.PI * Math.Min(1, (age - 0.12f) / 0.48f));
            Say(Hits[index % Hits.Length], x[defender], y[defender] - 46 - age * 20, Color.Yellow);
        }

        for (var side = 0; side < 2; side++)
        {
            if (side == downSide && down != null)
            {
                Pose(side, x[side] - (side == 0 ? 14 : -14), BoxerY + 18, Direction.South, side == 0 ? -90 : 90);
                var count = Math.Min(10, (int) (down.Value / (knockedOut ? 0.6f : 0.9f)) + 1);
                Say(knockedOut && count >= 10 ? Loc.GetString("cmu-sport-ko") : count.ToString(), ArenaWidth / 2, 80,
                    knockedOut ? Color.Red : Color.White, true);
                continue;
            }

            Pose(side, x[side], y[side], side == 0 ? Direction.East : Direction.West);
        }
    }

    private void DrawBoxing(DrawingHandleScreen handle)
    {
        Rect(handle, 0, Bar, ArenaWidth, ArenaHeight - Bar, Color.FromHex("#14121A"));
        // the crowd, as rows of faint heads
        for (var row = 0; row < 3; row++)
        {
            for (var seat = 0; seat < 40; seat++)
            {
                var shade = Color.FromHex("#3A3346").WithAlpha(0.6f + 0.1f * ((seat * 7 + row * 3) % 4));
                Circle(handle, 8 + seat * 13 + row % 2 * 6, Bar + 16 + row * 12, 5, shade);
            }
        }

        Rect(handle, 30, RingFloor, ArenaWidth - 60, 76, Color.FromHex("#C9D3DE"));
        Rect(handle, 30, RingFloor + 76, ArenaWidth - 60, 14, Color.FromHex("#2A3B6E"));
        for (var rope = 0; rope < 3; rope++)
            Rect(handle, 30, RingFloor - 52 + rope * 16, ArenaWidth - 60, 3, rope == 1 ? Color.White : Color.FromHex("#C8102E"));

        Rect(handle, 26, RingFloor - 60, 8, 150, Color.FromHex("#C8102E"));
        Rect(handle, ArenaWidth - 34, RingFloor - 60, 8, 150, Color.FromHex("#1F4FC8"));
    }

    #endregion

    #region Baseball

    private static readonly Vector2 Home = new(260, 214);
    private static readonly Vector2 Mound = new(260, 150);
    private static readonly Vector2[] Bases = { new(340, 150), new(260, 92), new(180, 150) };

    private void UpdateBaseball(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        var runs = new int[2];
        var batting = 1;
        var inning = 1;
        var state = 0;
        CMUSportsEvent? current = null;
        CMUSportsEvent? next = null;
        if (play != null)
        {
            foreach (var ev in play.Events)
            {
                if (ev.Time > t)
                {
                    if (ev.Action == CMUSportsAction.AtBat)
                        next = ev;

                    break;
                }

                if (ev.Action == CMUSportsAction.Bell)
                {
                    batting = ev.Side;
                    inning = ev.Value;
                    state = 0;
                    current = null;
                }
                else if (ev.Action == CMUSportsAction.AtBat)
                {
                    current = ev;
                    runs[ev.Side] += ev.Extra;
                    if (t > ev.Time + 0.8f)
                        state = ev.State;
                }
            }
        }

        var fielding = 1 - batting;
        Pose(fielding * 5, Mound.X, Mound.Y - 14, Direction.South);
        Pose(batting * 5 + 1, Home.X - 26, Home.Y - 12, Direction.East);
        Hide(batting * 5);
        Hide(fielding * 5 + 1);
        for (var i = 0; i < 3; i++)
        {
            Hide(fielding * 5 + 2 + i);
            if ((state & 1 << i) != 0)
                Pose(batting * 5 + 2 + i, Bases[i].X, Bases[i].Y - 14, Direction.South);
            else
                Hide(batting * 5 + 2 + i);
        }

        _clock = Loc.GetString("cmu-sport-inning", ("inning", inning), ("half", batting == 1 ? "▲" : "▼"));
        Say($"{Short(match.Name(1))} {runs[1]}", 12, Bar + 6, batting == 1 ? Color.Gold : Color.White);
        Say($"{Short(match.Name(0))} {runs[0]}", 12, Bar + 20, batting == 0 ? Color.Gold : Color.White);
        Say(Loc.GetString("cmu-sport-outs", ("outs", state >> 4)), ArenaWidth - 70, Bar + 6, Color.White);

        // the bat rests on the shoulder and swings through the zone at contact
        var swing = next is { } pitch && t > pitch.Time - 0.12f ? Math.Min(1, (t - pitch.Time + 0.12f) / 0.25f) : 0;
        if (current is { } hit && t - hit.Time < 0.3f)
            swing = 1;

        _bat.WorldRotation = Angle.FromDegrees(-45 - 150 * swing);
        Place(_bat, Home.X - 12, Home.Y - 22);

        if (next is { } coming && t > coming.Time - 0.5f && t <= coming.Time)
        {
            var travel = (t - (coming.Time - 0.5f)) / 0.5f;
            var ball = Vector2.Lerp(Mound, Home - new Vector2(0, 10), travel);
            Place(_ball, ball.X, ball.Y);
            return;
        }

        if (current is not { } last || t - last.Time > 1.6f)
            return;

        var age = t - last.Time;
        var word = last.Value switch
        {
            0 => "cmu-sport-out",
            1 => "cmu-sport-single",
            2 => "cmu-sport-double",
            3 => "cmu-sport-triple",
            _ => "cmu-sport-homer",
        };
        Say(Loc.GetString(word), ArenaWidth / 2, 70, last.Value == 4 ? Color.Gold : last.Value == 0 ? Color.White : Color.Lime, true);
        if (last.Extra > 0)
            Say(Loc.GetString("cmu-sport-runs", ("runs", last.Extra)), ArenaWidth / 2, 96, Color.Gold);

        if (age > 0.9f)
            return;

        // hits fly to the outfield, home runs out of the park, outs drop into a glove
        var angle = ((int) (last.Time * 7) % 3 - 1) * 0.6f;
        var reach = last.Value switch { 0 => 60f, 4 => 260f, _ => 90f + last.Value * 30 };
        var flight = age / 0.9f;
        var target = Home + new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * reach;
        var arc = MathF.Sin(MathF.PI * flight) * (last.Value == 4 ? 60 : 25);
        var at = Vector2.Lerp(Home, target, flight) - new Vector2(0, arc);
        Place(_ball, at.X, at.Y);
    }

    private static string Short(string team)
    {
        var words = team.Split(' ');
        return words[^1].ToUpperInvariant();
    }

    private void DrawBaseball(DrawingHandleScreen handle)
    {
        Rect(handle, 0, Bar, ArenaWidth, ArenaHeight - Bar, Color.FromHex("#3E7D34"));
        for (var stripe = 0; stripe < 10; stripe++)
            Rect(handle, stripe * 52, Bar, 26, ArenaHeight - Bar, Color.FromHex("#468C3B"));

        var dirt = Color.FromHex("#B07A4A");
        var grow = 18f;
        Poly(handle, dirt, Home + new Vector2(0, grow), Bases[0] + new Vector2(grow, 0), Bases[1] - new Vector2(0, grow), Bases[2] - new Vector2(grow, 0));
        Poly(handle, Color.FromHex("#3E7D34"), Home - new Vector2(0, 26), Bases[0] - new Vector2(26, 0), Bases[1] + new Vector2(0, 26), Bases[2] + new Vector2(26, 0));
        Circle(handle, Mound.X, Mound.Y, 12, dirt);
        Circle(handle, Home.X, Home.Y, 16, dirt);
        foreach (var bag in Bases)
            Rect(handle, bag.X - 5, bag.Y - 5, 10, 10, Color.White);

        Poly(handle, Color.White, Home + new Vector2(-6, -4), Home + new Vector2(6, -4), Home + new Vector2(6, 2), Home + new Vector2(0, 6), Home + new Vector2(-6, 2));
    }

    #endregion

    #region Zero-g ball

    private const float CageLeft = 16;
    private const float CageRight = ArenaWidth - 16;
    private const float CageTop = Bar + 8;
    private const float CageBottom = ArenaHeight - 8;

    private static readonly Vector2[] Formation = { new(150, 80), new(200, 136), new(150, 192) };

    private Vector2 Player(int side, int player, float t)
    {
        var spot = Formation[player % Formation.Length];
        if (side == 1)
            spot = spot with { X = ArenaWidth - spot.X };

        var phase = side * 3 + player * 1.7f + (_match?.Id ?? 0);
        return spot + new Vector2(MathF.Sin(t * 0.6f + phase) * 40, MathF.Cos(t * 0.8f + phase * 1.3f) * 22);
    }

    private Vector2 Goal(int side)
    {
        // side 0 shoots at the right goal, side 1 at the left
        return new Vector2(side == 0 ? CageRight - 6 : CageLeft + 6, (CageTop + CageBottom) / 2);
    }

    private void UpdateZeroG(CMUBetMatch match, CMUSportsPlay? play, float t)
    {
        var score = new int[2];
        CMUSportsEvent? holder = null;
        CMUSportsEvent? previous = null;
        CMUSportsEvent? goal = null;
        if (play != null)
        {
            foreach (var ev in play.Events)
            {
                if (ev.Time > t)
                    break;

                if (ev.Action == CMUSportsAction.Pass)
                {
                    previous = holder;
                    holder = ev;
                    goal = null;
                }
                else if (ev.Action == CMUSportsAction.Goal)
                {
                    score[ev.Side]++;
                    goal = ev;
                }
            }
        }

        var clock = Math.Max(0, t);
        for (var side = 0; side < 2; side++)
        {
            for (var player = 0; player < CMUSports.Squad; player++)
            {
                var at = Player(side, player, clock);
                var spin = (clock * 25 + side * 90 + player * 50) % 360;
                Pose(side * CMUSports.Squad + player, at.X, at.Y, side == 0 ? Direction.East : Direction.West, play == null ? 0 : spin);
            }
        }

        Say($"{Short(match.Name(0))} {score[0]}", 30, Bar + 4, Color.White);
        Say($"{score[1]} {Short(match.Name(1))}", ArenaWidth - 30, Bar + 4, Color.White, right: true);

        Vector2 ball;
        if (goal is { } scored)
        {
            var age = t - scored.Time;
            var from = Player(scored.Side, scored.Value, scored.Time);
            ball = Vector2.Lerp(from, Goal(scored.Side), Math.Min(1, age / 0.6f));
            if (age < 2.5f)
                Say(Loc.GetString("cmu-sport-goal", ("team", match.Name(scored.Side))), ArenaWidth / 2, ArenaHeight / 2 - 10, Color.Gold, true);
        }
        else if (holder is { } has)
        {
            var at = Player(has.Side, has.Value, clock) + new Vector2(has.Side == 0 ? 12 : -12, -4);
            var age = t - has.Time;
            if (previous is { } from && age < 0.5f)
            {
                var start = Player(from.Side, from.Value, has.Time) + new Vector2(from.Side == 0 ? 12 : -12, -4);
                at = Vector2.Lerp(start, at, age / 0.5f);
            }

            ball = at;
        }
        else
        {
            ball = new Vector2(ArenaWidth / 2, (CageTop + CageBottom) / 2 + MathF.Sin(t * 2) * 4);
        }

        _ball.WorldRotation = Angle.FromDegrees(clock * 90 % 360);
        Place(_ball, ball.X, ball.Y);
    }

    private void DrawZeroG(DrawingHandleScreen handle)
    {
        Rect(handle, 0, Bar, ArenaWidth, ArenaHeight - Bar, Color.FromHex("#05060E"));
        var seed = (_match?.Id ?? 1) * 7919;
        for (var star = 0; star < 70; star++)
        {
            seed = seed * 1103515245 + 12345 & int.MaxValue;
            var x = seed % (int) ArenaWidth;
            seed = seed * 1103515245 + 12345 & int.MaxValue;
            var y = Bar + seed % (int) (ArenaHeight - Bar);
            Rect(handle, x, y, 1.5f, 1.5f, Color.White.WithAlpha(0.3f + star % 5 * 0.12f));
        }

        var cage = Color.FromHex("#3FD8FF");
        Frame(handle, CageLeft, CageTop, CageRight - CageLeft, CageBottom - CageTop, cage.WithAlpha(0.7f));
        Rect(handle, ArenaWidth / 2 - 1, CageTop, 2, CageBottom - CageTop, cage.WithAlpha(0.25f));
        handle.DrawCircle(new Vector2(ArenaWidth / 2, (CageTop + CageBottom) / 2) * UIScale, 30 * UIScale, cage.WithAlpha(0.25f), false);
        for (var side = 0; side < 2; side++)
        {
            var colour = TeamColour(side);
            var goal = Goal(1 - side);
            Rect(handle, goal.X - 6, goal.Y - 34, 12, 68, colour.WithAlpha(0.35f));
            Frame(handle, goal.X - 6, goal.Y - 34, 12, 68, colour);
        }
    }

    private Color TeamColour(int side)
    {
        if (_match is not { } match || side >= match.Entrants.Count)
            return Color.White;

        return CMUSports.Colours[CMUSports.Colour(match.Sport, side, match.Entrants[side])] switch
        {
            "Red" => Color.FromHex("#E04040"),
            "Blue" => Color.FromHex("#4080FF"),
            "Green" => Color.FromHex("#40C060"),
            "Yellow" => Color.FromHex("#F0D040"),
            "Orange" => Color.FromHex("#F09030"),
            "Purple" => Color.FromHex("#A060E0"),
            "Brown" => Color.FromHex("#A07040"),
            _ => Color.White,
        };
    }

    #endregion

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        switch (_match?.Sport)
        {
            case CMUSport.Sprint:
                DrawSprint(handle);
                break;
            case CMUSport.Boxing:
                DrawBoxing(handle);
                break;
            case CMUSport.Baseball:
                DrawBaseball(handle);
                break;
            case CMUSport.ZeroGBall:
                DrawZeroG(handle);
                break;
            default:
                Rect(handle, 0, 0, ArenaWidth, ArenaHeight, Color.Black);
                break;
        }

        Rect(handle, 0, 0, ArenaWidth, Bar, Color.FromHex("#0C0F14"));
        Rect(handle, 0, Bar - 2, ArenaWidth, 2, Color.FromHex("#7CFF00"));
    }

    private void Rect(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        handle.DrawRect(Box(x, y, width, height), colour);
    }

    private void Frame(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        handle.DrawRect(Box(x, y, width, height), colour, false);
    }

    private UIBox2 Box(float x, float y, float width, float height)
    {
        return new UIBox2(x * UIScale, y * UIScale, (x + width) * UIScale, (y + height) * UIScale);
    }

    private void Circle(DrawingHandleScreen handle, float x, float y, float radius, Color colour)
    {
        handle.DrawCircle(new Vector2(x, y) * UIScale, radius * UIScale, colour);
    }

    private void Poly(DrawingHandleScreen handle, Color colour, params Vector2[] points)
    {
        var scaled = points.Select(p => p * UIScale).ToArray();
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, scaled, colour);
    }

    /// <summary>Text above the dolls: the scoreboard, calls and hit words.</summary>
    private sealed class TextLayer(CMUSportsArena arena) : Control
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            var outline = new TextOutline(2, Color.Black);
            var scale = UIScale;
            handle.DrawString(arena._small, new Vector2(8, 7) * scale, arena._header, scale, Color.FromHex("#7CFF00"));
            var clock = handle.GetDimensions(arena._small, arena._clock, scale);
            handle.DrawString(arena._small, new Vector2(ArenaWidth - 8, 7) * scale - clock with { Y = 0 }, arena._clock, scale, Color.White);
            foreach (var (text, position, colour, big, right) in arena._texts)
            {
                var font = big ? arena._big : arena._small;
                var size = handle.GetDimensions(font, text, scale);
                var at = position * scale;
                if (big)
                    at -= size / 2;
                else if (right)
                    at.X -= size.X;

                handle.DrawString(font, at, text, scale, colour, outline);
            }
        }
    }
}
