using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
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

/// <summary>A button that mutes or unmutes every game and arena on this client.</summary>
public sealed class CMUGameSoundButton : Button
{
    public CMUGameSoundButton()
    {
        HorizontalAlignment = HAlignment.Left;
        ToggleMode = true;
        OnToggled += args => CMUGameAudio.SetMuted(!args.Pressed);
        Refresh();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        CMUGameAudio.MutedChanged += Refresh;
        Refresh();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        CMUGameAudio.MutedChanged -= Refresh;
    }

    private void Refresh()
    {
        Pressed = !CMUGameAudio.Muted;
        Text = Loc.GetString(CMUGameAudio.Muted ? "cmu-game-sound-off" : "cmu-game-sound-on");
    }
}

/// <summary>
/// The arcade on a page: <c>&lt;CMUNetArcade/&gt;</c> puts every cabinet in its own tab, and
/// <c>&lt;CMUNetArcade Game="HuggerRun"/&gt;</c> or <c>Game="TrackerReflex"</c> shows just that one.
/// The games run on the client; their scores go to the round's high score boards.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetArcade : BoxContainer, IDocumentTag
{
    [Dependency] private IGameTiming _timing = default!;

    public CMUNetArcade()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (args.TryGetValue("Game", out var name))
        {
            if (!Enum.TryParse<CMUArcadeGame>(name, out var game))
                return false;

            AddChild(new CMUArcadeCabinet(game, _timing));
            control = this;
            return true;
        }

        var tabs = new TabContainer { HorizontalExpand = true };
        var games = Enum.GetValues<CMUArcadeGame>();
        foreach (var game in games)
        {
            tabs.AddChild(new CMUArcadeCabinet(game, _timing));
        }

        for (var i = 0; i < games.Length; i++)
        {
            tabs.SetTabTitle(i, Loc.GetString($"cmu-arcade-{games[i]}"));
        }

        // a game left in a hidden tab must not keep the keyboard
        tabs.OnTabChanged += _ =>
        {
            foreach (var cabinet in tabs.Children.OfType<CMUArcadeCabinet>())
            {
                cabinet.Screen.ReleaseKeyboardFocus();
            }
        };
        AddChild(new CMUGameSoundButton());
        AddChild(tabs);
        control = this;
        return true;
    }
}

/// <summary>One arcade game: its screen, how to play it, and the round's high scores under it.</summary>
public sealed class CMUArcadeCabinet : BoxContainer
{
    private readonly CMUArcadeGame _game;
    private readonly BoxContainer _scores = new() { Orientation = LayoutOrientation.Vertical };
    private ICMUComputerHost? _host;

    public readonly CMUArcadeScreen Screen;

    public CMUArcadeCabinet(CMUArcadeGame game, IGameTiming timing)
    {
        _game = game;
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 4);
        Screen = game switch
        {
            CMUArcadeGame.HuggerRun => new CMUHuggerRun(timing),
            _ => new CMUTrackerReflex(timing),
        };
        Screen.OnGameOver += score => _host?.SubmitScore(_game, score);

        AddChild(new Label { Text = Loc.GetString($"cmu-arcade-{game}"), StyleClasses = { "LabelHeading" } });
        AddChild(new Label { Text = Screen.Help });
        AddChild(Screen);
        AddChild(new Label { Text = Loc.GetString("cmu-arcade-scores"), StyleClasses = { "LabelKeyText" } });
        AddChild(_scores);
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

/// <summary>What an arcade key means. Arrows, WASD and the numpad's 8, 4, 6 and 2 steer; Space, Enter and numpad 5 act.</summary>
public enum CMUArcadeKey : byte
{
    None,
    Up,
    Down,
    Left,
    Right,
    Action,
}

/// <summary>
/// A key on the arcade's keyboard. <see cref="Pad"/> is the digit for the numpad and number row keys, 0 otherwise,
/// so a game can use the keypad as a 3 by 3 grid (7 8 9 on top) as well as for steering.
/// </summary>
public readonly record struct CMUArcadeInput(CMUArcadeKey Key, int Pad, bool Down, bool Repeat);

/// <summary>
/// A game screen: green phosphor on black, a score line, and a game over when it ends. It plays from the keyboard:
/// click the screen to take the keys, Space or Enter to start, Esc to let go. While it holds the keys the
/// character at the computer does not walk off.
/// </summary>
public abstract class CMUArcadeScreen : Control
{
    protected static readonly Color Green = Color.FromHex("#3FE05A");
    protected static readonly Color Dim = Color.FromHex("#1F8A33");
    protected readonly IGameTiming Timing;
    protected bool Running;
    protected int Score;
    private readonly IInputManager _input;
    private string _status = string.Empty;

    public event Action<int>? OnGameOver;

    protected CMUArcadeScreen(IGameTiming timing)
    {
        Timing = timing;
        _input = IoCManager.Resolve<IInputManager>();
        SetSize = new Vector2(320, 224);
        HorizontalAlignment = HAlignment.Left;
        MouseFilter = MouseFilterMode.Stop;
        KeyboardFocusOnClick = true;
        CanKeyboardFocus = true;
    }

    /// <summary>How to play, shown above the screen.</summary>
    public abstract string Help { get; }

    public virtual void Start()
    {
        Score = 0;
        Running = true;
        _status = string.Empty;
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

    /// <summary>Plays a game sound while the screen is on show and the games are not muted.</summary>
    protected void Sound(string path, float volume = 0, float pitch = 1)
    {
        CMUGameAudio.Play(this, path, volume, pitch);
    }

    /// <summary>A key went down or up while the screen has the keyboard. Space and Enter start a game that is not running.</summary>
    protected virtual void OnKey(CMUArcadeInput input)
    {
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _input.FirstChanceOnKeyEvent += OnRawKey;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _input.FirstChanceOnKeyEvent -= OnRawKey;
    }

    private void OnRawKey(KeyEventArgs args, KeyEventType type)
    {
        if (!HasKeyboardFocus() || !VisibleInTree)
            return;

        if (args.Key == Keyboard.Key.Escape)
        {
            if (type == KeyEventType.Down)
                ReleaseKeyboardFocus();

            args.Handle();
            return;
        }

        var (key, pad) = Map(args.Key);
        if (key == CMUArcadeKey.None && pad == 0)
            return;

        args.Handle();
        var down = type != KeyEventType.Up;
        if (!Running && down && type != KeyEventType.Repeat && key == CMUArcadeKey.Action)
        {
            Start();
            return;
        }

        OnKey(new CMUArcadeInput(key, pad, down, type == KeyEventType.Repeat));
    }

    private static (CMUArcadeKey Key, int Pad) Map(Keyboard.Key key)
    {
        return key switch
        {
            Keyboard.Key.Up or Keyboard.Key.W => (CMUArcadeKey.Up, 0),
            Keyboard.Key.Down or Keyboard.Key.S => (CMUArcadeKey.Down, 0),
            Keyboard.Key.Left or Keyboard.Key.A => (CMUArcadeKey.Left, 0),
            Keyboard.Key.Right or Keyboard.Key.D => (CMUArcadeKey.Right, 0),
            Keyboard.Key.Space or Keyboard.Key.Return or Keyboard.Key.NumpadEnter => (CMUArcadeKey.Action, 0),
            Keyboard.Key.NumpadNum8 => (CMUArcadeKey.Up, 8),
            Keyboard.Key.NumpadNum2 => (CMUArcadeKey.Down, 2),
            Keyboard.Key.NumpadNum4 => (CMUArcadeKey.Left, 4),
            Keyboard.Key.NumpadNum6 => (CMUArcadeKey.Right, 6),
            Keyboard.Key.NumpadNum5 => (CMUArcadeKey.Action, 5),
            >= Keyboard.Key.NumpadNum1 and <= Keyboard.Key.NumpadNum9 => (CMUArcadeKey.None, (int) (key - Keyboard.Key.NumpadNum0)),
            >= Keyboard.Key.Num1 and <= Keyboard.Key.Num9 => (CMUArcadeKey.None, (int) (key - Keyboard.Key.Num0)),
            _ => (CMUArcadeKey.None, 0),
        };
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();
        OnFocusLost();
    }

    /// <summary>The keys went elsewhere: let go of anything held.</summary>
    protected virtual void OnFocusLost()
    {
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, Color.FromHex("#020602"));
        DrawGame(handle);
        handle.DrawRect(PixelSizeBox, Green, false);
        var font = UserInterfaceManager.ThemeDefaults.DefaultFont;
        handle.DrawString(font, new Vector2(6, 4) * UIScale, _status.Length > 0 ? _status : Loc.GetString("cmu-arcade-score", ("score", Score)), UIScale, Green);
        if (HasKeyboardFocus())
            return;

        // without the keys the game cannot hear you, so say how to give them back
        var prompt = Loc.GetString(Running ? "cmu-game-arcade-resume" : "cmu-game-arcade-focus");
        var size = handle.GetDimensions(font, prompt, UIScale);
        var at = PixelSizeBox.Center - size / 2 + new Vector2(0, PixelHeight * 0.32f);
        handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(6, 3) * UIScale, size + new Vector2(12, 6) * UIScale), Color.Black.WithAlpha(0.8f));
        if (Timing.RealTime.TotalSeconds % 1.2 < 0.8)
            handle.DrawString(font, at, prompt, UIScale, Green);
    }

    protected abstract void DrawGame(DrawingHandleScreen handle);
}

/// <summary>
/// The sportsbook: <c>&lt;CMUNetSportsbook/&gt;</c>. Every sport has its own tab with its own arena, which plays
/// that sport's live match or the next one's lineup; the tab takes bets from the logged in account until kickoff,
/// lists the user's open bets on that sport and its latest results, each of which can be watched again.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetSportsbook : BoxContainer, IDocumentTag
{
    [Dependency] private IGameTiming _timing = default!;

    private readonly TabContainer _tabs = new() { HorizontalExpand = true };
    private readonly List<CMUSportsbookTab> _sports = new();
    private ICMUComputerHost? _host;
    private TimeSpan _next;

    public CMUNetSportsbook()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        foreach (var sport in Enum.GetValues<CMUSport>())
        {
            var tab = new CMUSportsbookTab(sport, _timing);
            tab.OnPlace += (match, entrant, amount) => _host?.PlaceBet(match, entrant, amount);
            _sports.Add(tab);
            _tabs.AddChild(tab);
        }

        AddChild(new CMUGameSoundButton());
        AddChild(_tabs);
        RefreshTitles();
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
        if (_host == null || _timing.RealTime < _next)
            return;

        _next = _timing.RealTime + TimeSpan.FromSeconds(5);
        _host.RequestBets();
        RefreshTitles();
    }

    private void OnState(CMUBetStateMessage state)
    {
        foreach (var tab in _sports)
        {
            tab.SetState(state, _host?.SessionUser != null);
        }

        RefreshTitles();
    }

    /// <summary>A tab whose sport is in play says so in its title.</summary>
    private void RefreshTitles()
    {
        for (var i = 0; i < _sports.Count; i++)
        {
            var name = Loc.GetString($"cmu-sport-{_sports[i].Sport}");
            _tabs.SetTabTitle(i, _sports[i].InPlay ? Loc.GetString("cmu-game-tab-live", ("name", name)) : name);
        }
    }
}

/// <summary>One sport's tab on the sportsbook: its arena, its open match, the user's bets on it, its results.</summary>
public sealed class CMUSportsbookTab : BoxContainer
{
    private readonly IGameTiming _timing;
    private readonly CMUSportsArena _arena;
    // the board keeps to the arena's width, so long lineups wrap into its grid instead of running off the page
    private readonly BoxContainer _board = new()
    {
        Orientation = LayoutOrientation.Vertical,
        SeparationOverride = 6,
        HorizontalAlignment = HAlignment.Left,
        SetWidth = CMUSportsArena.ArenaWidth + 4,
    };
    private readonly List<(Label Label, TimeSpan Kickoff)> _countdowns = new();
    private List<CMUBetMatch> _matches = new();
    private string _shown = string.Empty;
    private TimeSpan? _replayEnds;

    public readonly CMUSport Sport;

    public event Action<int, int, int>? OnPlace;

    public CMUSportsbookTab(CMUSport sport, IGameTiming timing)
    {
        Sport = sport;
        _timing = timing;
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);
        _arena = CMUSportsArena.Create(sport);
        AddChild(new PanelContainer
        {
            HorizontalAlignment = HAlignment.Left,
            PanelOverride = new StyleBoxFlat { BorderColor = Color.FromHex("#7CFF00"), BorderThickness = new Thickness(2) },
            Children = { _arena },
        });
        AddChild(_board);
        _board.AddChild(new Label { Text = Loc.GetString("cmu-cmb-loading") });
    }

    /// <summary>Whether this sport has a match being played right now.</summary>
    public bool InPlay
    {
        get
        {
            var now = _timing.CurTime;
            return _matches.Any(m => m.Result == null && m.Kickoff <= now);
        }
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
    }

    /// <summary>The arena plays the match in progress, or else shows the lineup of the next one.</summary>
    private void Feature()
    {
        if (_replayEnds != null)
            return;

        var now = _timing.CurTime;
        var live = _matches.FirstOrDefault(m => m.Result == null && m.Kickoff <= now)
            ?? _matches.Where(m => m.Result == null).OrderBy(m => m.Kickoff).FirstOrDefault()
            ?? _matches.FirstOrDefault();
        if (live != null)
            _arena.Show(live);
    }

    public void SetState(CMUBetStateMessage state, bool loggedIn)
    {
        _matches = state.Matches.Where(m => m.Sport == Sport).ToList();
        var slips = state.Slips.Where(s => s.Sport == Sport).ToList();
        Feature();

        // only rebuild when something changed, so a half typed stake survives the refresh
        var shown = string.Join(";", _matches.Select(m => $"{m.Id}:{m.Winner}:{m.Result}"))
            + string.Join(";", slips.Select(s => $"{s.Match}:{s.Entrant}:{s.Amount}")) + loggedIn;
        if (shown == _shown)
            return;

        _shown = shown;
        _board.RemoveAllChildren();
        _countdowns.Clear();
        foreach (var match in _matches.Where(m => m.Result == null).OrderBy(m => m.Kickoff))
        {
            AddUpcoming(match, loggedIn);
        }

        if (slips.Count > 0)
        {
            _board.AddChild(new Label { Text = Loc.GetString("cmu-bet-slips"), StyleClasses = { "LabelKeyText" } });
            foreach (var slip in slips)
            {
                _board.AddChild(new Label
                {
                    Text = Loc.GetString("cmu-bet-slip", ("amount", slip.Amount), ("team", slip.Team),
                        ("sport", Loc.GetString($"cmu-sport-{slip.Sport}"))),
                });
            }
        }

        _board.AddChild(new Label { Text = Loc.GetString("cmu-bet-results"), StyleClasses = { "LabelKeyText" } });
        foreach (var match in _matches.Where(m => m.Result != null))
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
                        HorizontalExpand = true,
                        ClipText = true,
                    },
                    replay,
                },
            });
        }
    }

    /// <summary>
    /// The sport's next or live match: a countdown, then its entrants two to a row, as bet buttons while it takes
    /// bets from a logged in account and as names with their odds otherwise.
    /// </summary>
    private void AddUpcoming(CMUBetMatch match, bool loggedIn)
    {
        _board.AddChild(new Label { Text = Loc.GetString($"cmu-sport-{match.Sport}"), StyleClasses = { "LabelHeading" } });
        var countdown = new Label();
        _countdowns.Add((countdown, match.Kickoff));
        _board.AddChild(countdown);
        var open = match.Kickoff > _timing.CurTime;
        if (open && !loggedIn)
            _board.AddChild(new Label { Text = Loc.GetString("cmu-bet-login") });

        LineEdit? amount = null;
        if (open && loggedIn)
        {
            amount = new LineEdit { PlaceHolder = Loc.GetString("cmu-bet-amount"), MinWidth = 170, HorizontalAlignment = HAlignment.Left };
            amount.IsValid = text => text.Length <= 5 && text.All(char.IsDigit);
            _board.AddChild(amount);
        }

        var picks = new GridContainer { Columns = 2, HSeparationOverride = 6, VSeparationOverride = 4, HorizontalExpand = true };
        for (var slot = 0; slot < match.Entrants.Count; slot++)
        {
            if (amount == null)
            {
                picks.AddChild(new Label
                {
                    Text = Loc.GetString("cmu-game-bet-entrant", ("team", match.Name(slot)), ("odds", match.Odds[slot])),
                    HorizontalExpand = true,
                    ClipText = true,
                });
                continue;
            }

            var pick = new Button
            {
                Text = Loc.GetString("cmu-bet-on", ("team", match.Name(slot)), ("odds", match.Odds[slot])),
                HorizontalExpand = true,
                ClipText = true,
            };
            var id = match.Id;
            var entrant = slot;
            var stakeBox = amount;
            pick.OnPressed += _ =>
            {
                if (int.TryParse(stakeBox.Text, out var stake))
                    OnPlace?.Invoke(id, entrant, stake);
            };
            picks.AddChild(pick);
        }

        _board.AddChild(picks);
    }
}
