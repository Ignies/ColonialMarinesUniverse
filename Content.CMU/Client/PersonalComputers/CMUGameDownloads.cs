using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The download button under an arcade cabinet: a short download with a progress bar, then the game is on the
/// computer's desktop, where it opens in a window of its own. Shown only on a computer with a desktop, while the
/// arcade is on.
/// </summary>
public sealed class CMUGameDownloadButton : BoxContainer
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(2.6);

    /// <summary>How long to wait for the computer to put the game on the desktop before calling it failed.</summary>
    private static readonly TimeSpan SaveWait = TimeSpan.FromSeconds(4);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private readonly CMUArcadeGame _game;
    private readonly Button _button;
    private readonly CMUDownloadBar _bar = new() { Visible = false, VerticalAlignment = VAlignment.Center };
    private readonly Label _status = new() { VerticalAlignment = VAlignment.Center };
    private ICMUComputerHost? _host;
    private TimeSpan? _started;
    private TimeSpan? _sent;
    private bool? _saved;
    private float _check;

    public CMUGameDownloadButton(CMUArcadeGame game)
    {
        IoCManager.InjectDependencies(this);
        _game = game;
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 8;

        _button = new Button
        {
            Text = Loc.GetString("cmu-game-download"),
            ToolTip = Loc.GetString("cmu-game-download-tooltip"),
            VerticalAlignment = VAlignment.Center,
        };
        _button.OnPressed += _ => Begin();
        _button.Visible = false;
        _status.Visible = false;
        AddChild(_button);
        AddChild(_bar);
        AddChild(_status);
    }

    private bool Available => _host != null && _cfg.GetCVar(CMUPersonalComputerCVars.Arcade);

    private bool OnDesktop => _host != null && _host.Downloads.Contains(_game);

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _host = CMUNetPageHost.Find(this);
        _saved = null;
        _check = 0;
    }

    private void Begin()
    {
        if (!Available || _started != null || _sent != null)
            return;

        _started = _timing.RealTime;
        _button.Disabled = true;
        _bar.Progress = 0;
        _bar.Visible = true;
        CMUGameAudio.Play(this, "/Audio/Machines/quickbeep.ogg", -4);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var now = _timing.RealTime;
        if (_started is { } started)
        {
            var done = (float) ((now - started) / Duration);
            if (done < 1)
            {
                // a colony uplink: it comes in chunks, with a stall or two
                var shown = MathF.Floor(MathF.Pow(done, 1.4f) * 24) / 24;
                _bar.Progress = shown;
                _status.Text = Loc.GetString("cmu-game-downloading", ("percent", (int) (shown * 100)));
                return;
            }

            _started = null;
            _sent = now;
            _bar.Progress = 1;
            _status.Text = Loc.GetString("cmu-game-download-installing");
            _host?.DownloadGame(_game);
            return;
        }

        if (_sent is { } sent)
        {
            if (OnDesktop)
            {
                _sent = null;
                Show(true);
                CMUGameAudio.Play(this, "/Audio/Machines/twobeep.ogg", -4);
            }
            else if (now - sent > SaveWait)
            {
                _sent = null;
                _saved = false;
                _bar.Visible = false;
                _button.Disabled = false;
                _status.Text = Loc.GetString("cmu-game-download-failed");
            }

            return;
        }

        // whether the game is still on the desktop, checked twice a second like the desktop's own icons
        _check -= args.DeltaSeconds;
        if (_check > 0)
            return;

        _check = 0.5f;
        // the row itself stays in the tree so it keeps checking; only what is in it hides
        var available = Available;
        _button.Visible = available;
        _status.Visible = available;
        if (available && _saved != OnDesktop)
            Show(OnDesktop);
    }

    private void Show(bool saved)
    {
        _saved = saved;
        _bar.Visible = false;
        _button.Disabled = saved;
        _button.Text = Loc.GetString(saved ? "cmu-game-download-on-desktop" : "cmu-game-download");
        _status.Text = Loc.GetString(saved ? "cmu-game-download-done" : "cmu-game-download-hint");
    }
}

/// <summary>A download's progress, in blocks like an old uplink terminal.</summary>
public sealed class CMUDownloadBar : Control
{
    private const int Blocks = 16;
    private static readonly Color Green = Color.FromHex("#3FE05A");

    public float Progress;

    public CMUDownloadBar()
    {
        SetSize = new Vector2(130, 12);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var scale = UIScale;
        handle.DrawRect(PixelSizeBox, Color.Black);
        handle.DrawRect(PixelSizeBox, Green, false);
        var inner = new UIBox2(2 * scale, 2 * scale, PixelWidth - 2 * scale, PixelHeight - 2 * scale);
        var step = inner.Width / Blocks;
        var lit = (int) MathF.Floor(Math.Clamp(Progress, 0, 1) * Blocks);
        for (var i = 0; i < lit; i++)
        {
            var left = inner.Left + i * step;
            handle.DrawRect(new UIBox2(left, inner.Top, left + step - scale, inner.Bottom), Green);
        }
    }
}

/// <summary>Desktop icons for downloaded games: a sprite from the game itself.</summary>
public static class CMUGameIcons
{
    private static readonly ResPath Parasite = new("/Textures/_RMC14/Mobs/Xenonids/Parasite/parasite.rsi");
    private static readonly ResPath Detector = new("/Textures/_RMC14/Objects/Tools/motion_detector.rsi");
    private static readonly Dictionary<CMUArcadeGame, Texture> Icons = new();

    public static Texture For(CMUArcadeGame game)
    {
        if (Icons.TryGetValue(game, out var cached))
            return cached;

        var sprites = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        var icon = game switch
        {
            // the hugger mid stride, running the way it runs in the game
            CMUArcadeGame.HuggerRun => sprites.RsiStateLike(new SpriteSpecifier.Rsi(Parasite, "running")).GetFrame(RsiDirection.East, 0),
            _ => sprites.Frame0(new SpriteSpecifier.Rsi(Detector, "detector")),
        };
        Icons[game] = icon;
        return icon;
    }
}

/// <summary>
/// A downloaded game in its desktop window: the screen as big as the window allows, how to play it, and the sound
/// button. The window opens maximized. Scores still go to the round's high score board.
/// </summary>
public sealed class CMUArcadeWindow : BoxContainer
{
    private bool _maximized;

    public readonly CMUArcadeGame Game;
    public readonly CMUArcadeScreen Screen;

    public CMUArcadeWindow(CMUComputerTheme theme, CMUArcadeGame game, IGameTiming timing, ICMUComputerHost? host)
    {
        Game = game;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        Screen = CMUArcadeCabinet.NewScreen(game, timing);
        Screen.OnGameOver += score => host?.SubmitScore(game, score);

        var help = new FormattedMessage();
        help.PushColor(theme.Dim);
        help.AddText(Screen.Help);
        help.Pop();
        var helpLabel = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        helpLabel.SetMessage(help);

        AddChild(new CMUArcadeStage(Screen));
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 3),
            SeparationOverride = 8,
            Children = { helpLabel, new CMUGameSoundButton { VerticalAlignment = VAlignment.Center } },
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_maximized)
            return;

        // once the desktop has laid the window out, so un-maximizing goes back to a sensible size
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is not CMUDesktopWindow window)
                continue;

            if (window.Size.X > 0)
            {
                _maximized = true;
                window.ToggleMaximized();
            }

            return;
        }
    }
}

/// <summary>
/// Holds an arcade screen at the largest whole number of screen pixels per game unit that fits, centred on black,
/// so the pixel art stays sharp at any window size. Clicking anywhere on it gives the game the keys.
/// </summary>
public sealed class CMUArcadeStage : Control
{
    private static readonly Vector2 ScreenSize = new(320, 224);

    private readonly CMUArcadeScreen _screen;

    public CMUArcadeStage(CMUArcadeScreen screen)
    {
        _screen = screen;
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        AddChild(screen);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _screen.Measure(ScreenSize);
        return ScreenSize / 2;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var scale = UIScale;
        var pixels = finalSize * scale;
        var fit = MathF.Min(pixels.X / ScreenSize.X, pixels.Y / ScreenSize.Y);
        // screen pixels per game unit: whole numbers when the window is big enough, else just fit it
        var perUnit = fit >= 1 ? MathF.Floor(fit) : MathF.Max(fit, 0.25f);
        _screen.Zoom = perUnit / scale;

        // the screen's own units are zoomed, so its place inside the stage is given in them
        var offset = new Vector2(MathF.Floor((pixels.X - ScreenSize.X * perUnit) / 2), MathF.Floor((pixels.Y - ScreenSize.Y * perUnit) / 2));
        _screen.Arrange(UIBox2.FromDimensions(offset / perUnit, ScreenSize));
        return finalSize;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _screen.GrabKeyboardFocus();
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, Color.Black);
    }
}
