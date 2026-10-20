using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>Finds the computer a page tag runs on, through the browser around it.</summary>
public static class CMUNetPageHost
{
    public static ICMUComputerHost? Find(Control control)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is CMUNetBrowser browser)
                return browser.Host;
        }

        return null;
    }
}

/// <summary>
/// An arcade cabinet on a page: <c>&lt;CMUNetArcade Game="HuggerRun"/&gt;</c> or <c>Game="TrackerReflex"</c>.
/// The game runs on the client; its score goes to the round's high score board under it.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetArcade : BoxContainer, IDocumentTag
{
    [Dependency] private IGameTiming _timing = default!;

    private CMUArcadeGame _game;
    private readonly BoxContainer _scores;
    private ICMUComputerHost? _host;
    private CMUArcadeScreen? _screen;

    public CMUNetArcade()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);
        _scores = new BoxContainer { Orientation = LayoutOrientation.Vertical };
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Game", out var game) || !Enum.TryParse(game, out _game))
            return false;

        _screen = _game == CMUArcadeGame.HuggerRun ? new CMUHuggerRun(_timing) : new CMUTrackerReflex(_timing);
        _screen.OnGameOver += score => _host?.SubmitScore(_game, score);

        AddChild(new Label { Text = Loc.GetString($"cmu-arcade-{_game}"), StyleClasses = { "LabelHeading" } });
        AddChild(new Label { Text = Loc.GetString(_game == CMUArcadeGame.HuggerRun ? "cmu-arcade-help-hugger" : "cmu-arcade-help-tracker") });
        AddChild(_screen);
        if (_screen is CMUHuggerRun hugger)
            AddChild(hugger.Pad());

        var start = new Button { Text = Loc.GetString("cmu-arcade-start"), HorizontalAlignment = HAlignment.Left };
        start.OnPressed += _ => _screen.Start();
        AddChild(start);
        AddChild(new Label { Text = Loc.GetString("cmu-arcade-scores"), StyleClasses = { "LabelKeyText" } });
        AddChild(_scores);
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _host = CMUNetPageHost.Find(this);
        if (_host == null)
            return;

        _host.ArcadeReceived += OnScores;
        _host.RequestArcade(_game);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.ArcadeReceived -= OnScores;
    }

    private void OnScores(CMUArcadeStateMessage state)
    {
        if (state.Game != _game)
            return;

        _scores.RemoveAllChildren();
        if (state.Scores.Count == 0)
            _scores.AddChild(new Label { Text = Loc.GetString("cmu-arcade-no-scores") });

        for (var i = 0; i < state.Scores.Count; i++)
        {
            var score = state.Scores[i];
            _scores.AddChild(new Label { Text = Loc.GetString("cmu-arcade-score-line", ("rank", i + 1), ("name", score.Name), ("score", score.Score)) });
        }
    }
}

/// <summary>A game screen: green phosphor on black, a score line, and a game over when it ends.</summary>
public abstract class CMUArcadeScreen : Control
{
    protected static readonly Color Green = Color.FromHex("#3FE05A");
    protected static readonly Color Dim = Color.FromHex("#1F8A33");
    protected readonly IGameTiming Timing;
    protected bool Running;
    protected int Score;
    private string _status = string.Empty;

    public event Action<int>? OnGameOver;

    protected CMUArcadeScreen(IGameTiming timing)
    {
        Timing = timing;
        SetSize = new Vector2(320, 224);
        HorizontalAlignment = HAlignment.Left;
        MouseFilter = MouseFilterMode.Stop;
        KeyboardFocusOnClick = true;
        CanKeyboardFocus = true;
    }

    public virtual void Start()
    {
        Score = 0;
        Running = true;
        GrabKeyboardFocus();
    }

    protected void GameOver()
    {
        Running = false;
        _status = Loc.GetString("cmu-arcade-over", ("score", Score));
        OnGameOver?.Invoke(Score);
    }

    protected void SetStatus(string status)
    {
        _status = status;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, Color.FromHex("#020602"));
        DrawGame(handle);
        handle.DrawRect(PixelSizeBox, Green, false);
        var font = UserInterfaceManager.ThemeDefaults.DefaultFont;
        handle.DrawString(font, new Vector2(6, 4) * UIScale, _status.Length > 0 ? _status : Loc.GetString("cmu-arcade-score", ("score", Score)), UIScale, Green);
    }

    protected abstract void DrawGame(DrawingHandleScreen handle);
}

/// <summary>Snake: steer the hugger to the eggs; every egg makes it longer; hitting a wall or its tail ends it.</summary>
public sealed class CMUHuggerRun : CMUArcadeScreen
{
    private const int Width = 20;
    private const int Height = 14;
    private const float Step = 0.14f;

    private readonly List<Vector2i> _body = new();
    private readonly Random _random = new();
    private Vector2i _direction = new(1, 0);
    private Vector2i _next = new(1, 0);
    private Vector2i _egg;
    private float _timer;

    public CMUHuggerRun(IGameTiming timing) : base(timing)
    {
    }

    /// <summary>Buttons for those who would rather click than use the arrow keys.</summary>
    public Control Pad()
    {
        Button Arrow(string text, Vector2i direction)
        {
            var button = new Button { Text = text, MinWidth = 32 };
            button.OnPressed += _ => Turn(direction);
            return button;
        }

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children = { Arrow("<", new Vector2i(-1, 0)), Arrow("^", new Vector2i(0, -1)), Arrow("v", new Vector2i(0, 1)), Arrow(">", new Vector2i(1, 0)) },
        };
    }

    public override void Start()
    {
        base.Start();
        _body.Clear();
        _body.Add(new Vector2i(5, 7));
        _body.Add(new Vector2i(4, 7));
        _body.Add(new Vector2i(3, 7));
        _direction = _next = new Vector2i(1, 0);
        SetStatus(string.Empty);
        PlaceEgg();
    }

    private void Turn(Vector2i direction)
    {
        // No turning straight back into the tail.
        if (direction + _direction != Vector2i.Zero)
            _next = direction;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.TextCursorUp)
            Turn(new Vector2i(0, -1));
        else if (args.Function == EngineKeyFunctions.TextCursorDown)
            Turn(new Vector2i(0, 1));
        else if (args.Function == EngineKeyFunctions.TextCursorLeft)
            Turn(new Vector2i(-1, 0));
        else if (args.Function == EngineKeyFunctions.TextCursorRight)
            Turn(new Vector2i(1, 0));
        else
            return;

        args.Handle();
    }

    private void PlaceEgg()
    {
        do
        {
            _egg = new Vector2i(_random.Next(Width), _random.Next(1, Height));
        } while (_body.Contains(_egg));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!Running)
            return;

        _timer += args.DeltaSeconds;
        if (_timer < Step)
            return;

        _timer = 0;
        _direction = _next;
        var head = _body[0] + _direction;
        if (head.X < 0 || head.Y < 1 || head.X >= Width || head.Y >= Height || _body.Contains(head))
        {
            GameOver();
            return;
        }

        _body.Insert(0, head);
        if (head == _egg)
        {
            Score += 10;
            PlaceEgg();
            return;
        }

        _body.RemoveAt(_body.Count - 1);
    }

    protected override void DrawGame(DrawingHandleScreen handle)
    {
        var cell = PixelWidth / (float) Width;
        UIBox2 Box(Vector2i at) => UIBox2.FromDimensions(new Vector2(at.X * cell + 1, at.Y * cell + 1), new Vector2(cell - 2, cell - 2));

        handle.DrawRect(Box(_egg), Color.FromHex("#C8C060"));
        for (var i = 0; i < _body.Count; i++)
            handle.DrawRect(Box(_body[i]), i == 0 ? Color.FromHex("#E8D8B0") : Color.FromHex("#B8A070"));
    }
}

/// <summary>Reflexes: blips pop up on a tracker sweep; click them before they fade. Thirty seconds.</summary>
public sealed class CMUTrackerReflex : CMUArcadeScreen
{
    private const float Length = 30f;
    private const float Life = 1.4f;

    private readonly List<(Vector2 At, float Age)> _blips = new();
    private readonly Random _random = new();
    private float _left;
    private float _spawn;
    private float _sweep;

    public CMUTrackerReflex(IGameTiming timing) : base(timing)
    {
    }

    public override void Start()
    {
        base.Start();
        _blips.Clear();
        _left = Length;
        _spawn = 0;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _sweep = (_sweep + args.DeltaSeconds * 2) % MathF.Tau;
        if (!Running)
            return;

        _left -= args.DeltaSeconds;
        SetStatus(Loc.GetString("cmu-arcade-time", ("seconds", (int) MathF.Ceiling(_left))) + "  " + Loc.GetString("cmu-arcade-score", ("score", Score)));
        if (_left <= 0)
        {
            GameOver();
            return;
        }

        for (var i = _blips.Count - 1; i >= 0; i--)
        {
            var (at, age) = _blips[i];
            if (age + args.DeltaSeconds > Life)
                _blips.RemoveAt(i);
            else
                _blips[i] = (at, age + args.DeltaSeconds);
        }

        _spawn -= args.DeltaSeconds;
        if (_spawn > 0)
            return;

        _spawn = 0.35f + (float) _random.NextDouble() * 0.5f;
        var angle = (float) _random.NextDouble() * MathF.Tau;
        var distance = 0.2f + (float) _random.NextDouble() * 0.75f;
        _blips.Add((new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance, 0));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || !Running)
            return;

        var center = PixelSizeBox.Center;
        var radius = MathF.Min(PixelWidth, PixelHeight) * 0.45f;
        for (var i = _blips.Count - 1; i >= 0; i--)
        {
            var spot = center + _blips[i].At * radius;
            if ((spot - args.RelativePixelPosition).Length() > 12 * UIScale)
                continue;

            _blips.RemoveAt(i);
            Score += 5;
            args.Handle();
            return;
        }
    }

    protected override void DrawGame(DrawingHandleScreen handle)
    {
        var center = PixelSizeBox.Center;
        var radius = MathF.Min(PixelWidth, PixelHeight) * 0.45f;
        handle.DrawCircle(center, radius, Dim, false);
        handle.DrawCircle(center, radius * 0.66f, Dim, false);
        handle.DrawCircle(center, radius * 0.33f, Dim, false);
        handle.DrawLine(center, center + new Vector2(MathF.Cos(_sweep), MathF.Sin(_sweep)) * radius, Green);
        foreach (var (at, age) in _blips)
            handle.DrawCircle(center + at * radius, 7 * UIScale, Green.WithAlpha(1 - age / Life));
    }
}

/// <summary>
/// The sportsbook: <c>&lt;CMUNetSportsbook/&gt;</c>. Plays the live match, or the next one's lineup, in an arena;
/// takes bets from the logged in account until kickoff; lists the user's open bets and the latest results, each of
/// which can be watched again.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetSportsbook : BoxContainer, IDocumentTag
{
    [Dependency] private IGameTiming _timing = default!;

    private readonly CMUSportsArena _arena = new();
    private readonly BoxContainer _board = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly List<(Label Label, TimeSpan Kickoff)> _countdowns = new();
    private ICMUComputerHost? _host;
    private TimeSpan _next;
    private CMUBetStateMessage? _state;
    private string _shown = string.Empty;
    private TimeSpan? _replayEnds;

    public CMUNetSportsbook()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        AddChild(new PanelContainer
        {
            HorizontalAlignment = HAlignment.Left,
            PanelOverride = new StyleBoxFlat { BorderColor = Color.FromHex("#7CFF00"), BorderThickness = new Thickness(2) },
            Children = { _arena },
        });
        AddChild(_board);
        _board.AddChild(new Label { Text = Loc.GetString("cmu-cmb-loading") });
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _host = CMUNetPageHost.Find(this);
        if (_host != null)
            _host.BetsReceived += OnState;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.BetsReceived -= OnState;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var now = _timing.CurTime;
        foreach (var (label, kickoff) in _countdowns)
        {
            var wait = kickoff - now;
            label.Text = wait > TimeSpan.Zero
                ? Loc.GetString("cmu-bet-kickoff", ("seconds", (int) wait.TotalSeconds))
                : Loc.GetString("cmu-bet-closed");
        }

        if (_replayEnds != null && now > _replayEnds)
        {
            _replayEnds = null;
            Feature();
        }

        if (_host == null || _timing.RealTime < _next)
            return;

        _next = _timing.RealTime + TimeSpan.FromSeconds(5);
        _host.RequestBets();
    }

    /// <summary>The arena plays the match in progress, or else shows the lineup of the next one.</summary>
    private void Feature()
    {
        if (_replayEnds != null || _state == null)
            return;

        var now = _timing.CurTime;
        var live = _state.Matches.FirstOrDefault(m => m.Result == null && m.Kickoff <= now)
            ?? _state.Matches.Where(m => m.Result == null).OrderBy(m => m.Kickoff).FirstOrDefault()
            ?? _state.Matches.FirstOrDefault();
        if (live != null)
            _arena.Show(live);
    }

    private void OnState(CMUBetStateMessage state)
    {
        _state = state;
        Feature();

        // only rebuild when something changed, so a half typed stake survives the refresh
        var shown = string.Join(";", state.Matches.Select(m => $"{m.Id}:{m.Winner}:{m.Result}"))
            + string.Join(";", state.Slips) + (_host?.SessionUser != null);
        if (shown == _shown)
            return;

        _shown = shown;
        _board.RemoveAllChildren();
        _countdowns.Clear();
        foreach (var match in state.Matches.Where(m => m.Result == null).OrderBy(m => m.Kickoff))
            AddUpcoming(match);

        if (state.Slips.Count > 0)
        {
            _board.AddChild(new Label { Text = Loc.GetString("cmu-bet-slips"), StyleClasses = { "LabelKeyText" } });
            foreach (var slip in state.Slips)
                _board.AddChild(new Label { Text = slip });
        }

        _board.AddChild(new Label { Text = Loc.GetString("cmu-bet-results"), StyleClasses = { "LabelKeyText" } });
        foreach (var match in state.Matches.Where(m => m.Result != null))
        {
            var replay = new Button { Text = Loc.GetString("cmu-bet-replay") };
            var played = match;
            replay.OnPressed += _ =>
            {
                _replayEnds = _timing.CurTime + TimeSpan.FromSeconds(CMUSports.Length(played.Sport) + 6);
                _arena.Show(played, true);
            };
            _board.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Children =
                {
                    new Label
                    {
                        Text = Loc.GetString("cmu-bet-result", ("sport", Loc.GetString($"cmu-sport-{match.Sport}")),
                            ("winner", match.Name(match.Winner)), ("result", match.Result ?? string.Empty)),
                        VerticalAlignment = VAlignment.Center,
                    },
                    replay,
                },
            });
        }
    }

    private void AddUpcoming(CMUBetMatch match)
    {
        var names = Enumerable.Range(0, match.Entrants.Count).Select(match.Name);
        _board.AddChild(new Label
        {
            Text = Loc.GetString("cmu-bet-open", ("sport", Loc.GetString($"cmu-sport-{match.Sport}")), ("entrants", string.Join(" vs ", names))),
            StyleClasses = { "LabelHeading" },
        });
        var countdown = new Label();
        _countdowns.Add((countdown, match.Kickoff));
        _board.AddChild(countdown);
        if (match.Kickoff <= _timing.CurTime)
            return;

        if (_host?.SessionUser == null)
        {
            _board.AddChild(new Label { Text = Loc.GetString("cmu-bet-login") });
            return;
        }

        var amount = new LineEdit { PlaceHolder = Loc.GetString("cmu-bet-amount"), MinWidth = 120 };
        amount.IsValid = text => text.Length <= 5 && text.All(char.IsDigit);
        var picks = new GridContainer { Columns = 3, HSeparationOverride = 6, VSeparationOverride = 4 };
        for (var slot = 0; slot < match.Entrants.Count; slot++)
        {
            var pick = new Button { Text = Loc.GetString("cmu-bet-on", ("team", match.Name(slot)), ("odds", match.Odds[slot])) };
            var id = match.Id;
            var entrant = slot;
            pick.OnPressed += _ => Place(id, entrant, amount.Text);
            picks.AddChild(pick);
        }

        _board.AddChild(amount);
        _board.AddChild(picks);
    }

    private void Place(int match, int entrant, string amount)
    {
        if (int.TryParse(amount, out var stake))
            _host?.PlaceBet(match, entrant, stake);
    }
}
