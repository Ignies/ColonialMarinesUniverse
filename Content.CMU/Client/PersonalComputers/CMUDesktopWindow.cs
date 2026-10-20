using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A program window on the computer desktop: drag it by the title bar, resize it from the corner,
/// double click the title to maximize. Must be a child of a <see cref="LayoutContainer"/>. Framed by the OS's
/// shell: a filled title bar; Sevastolink's thick box with an inverted capitals title; or the Company's thin double
/// rule with its title in amber over a dark bar.
/// </summary>
public sealed class CMUDesktopWindow : PanelContainer
{
    private static readonly Vector2 MinimumSize = new(260, 160);

    private readonly CMUComputerTheme _theme;
    private readonly Label _title;
    private readonly PanelContainer _titleBar;
    private readonly Control _grip;
    private readonly BoxContainer _body;

    private Vector2? _dragFrom;
    private Vector2? _resizeFrom;
    private Vector2 _sizeAtResize;
    private (Vector2 Position, Vector2 Size)? _restore;
    private TimeSpan _lastTitleClick;

    public readonly CMUComputerApp App;
    public readonly Control Content;

    public event Action<CMUDesktopWindow>? OnClose;
    public event Action<CMUDesktopWindow>? OnMinimize;
    public event Action<CMUDesktopWindow>? OnFocus;

    public CMUDesktopWindow(CMUComputerTheme theme, CMUComputerApp app, string title, Control content)
    {
        _theme = theme;
        App = app;
        Content = content;
        MouseFilter = MouseFilterMode.Stop;
        var shell = theme.Shell;
        var style = theme.Style;
        PanelOverride = style?.WindowFrame() ?? shell switch
        {
            CMUComputerShell.Seegson => theme.Box(theme.Background, theme.Phosphor, 3),
            // Room inside the outer hairline for the inner one.
            CMUComputerShell.Weyland => new StyleBoxFlat
            {
                BackgroundColor = theme.Background,
                BorderColor = theme.Accent,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 4,
                ContentMarginTopOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginBottomOverride = 4,
            },
            _ => theme.Box(theme.Background, theme.Accent, 2),
        };

        var titleColour = style?.TitleColour ?? (shell == CMUComputerShell.Weyland ? theme.Accent : theme.Background);
        var titleFont = style?.TitleFont ?? (shell == CMUComputerShell.Classic ? theme.Bold : theme.ShellFont);
        _title = theme.Label(Letter(title), titleFont, titleColour);
        _title.HorizontalExpand = true;
        _title.ClipText = true;
        _title.VerticalAlignment = VAlignment.Center;

        var minimize = TitleButton("_", Loc.GetString("cmu-pc-window-minimize"));
        minimize.OnPressed += _ => OnMinimize?.Invoke(this);
        var close = TitleButton("X", Loc.GetString("cmu-pc-window-close"));
        close.OnPressed += _ => OnClose?.Invoke(this);

        _titleBar = new PanelContainer
        {
            PanelOverride = style?.TitleBar() ?? shell switch
            {
                CMUComputerShell.Seegson => theme.Box(theme.Phosphor),
                CMUComputerShell.Weyland => new StyleBoxFlat
                {
                    BackgroundColor = Color.InterpolateBetween(theme.Background, theme.Accent, 0.1f),
                    BorderColor = theme.Accent.WithAlpha(0.6f),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                },
                _ => theme.Box(theme.Accent),
            },
            MouseFilter = MouseFilterMode.Stop,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(6, 1, 2, 1),
                    SeparationOverride = 2,
                    Children = { TitleIcon(theme, app), _title, minimize, close },
                },
            },
        };
        _titleBar.OnKeyBindDown += TitleDown;
        _titleBar.OnKeyBindUp += args => EndDrag(args);

        _grip = new CMUResizeGrip(theme.Accent)
        {
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
        };
        _grip.OnKeyBindDown += GripDown;
        _grip.OnKeyBindUp += args => EndDrag(args);

        _body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { _titleBar, content },
        };
        AddChild(_body);
        AddChild(_grip);
    }

    public string Title
    {
        get => _title.Text ?? string.Empty;
        set => _title.Text = Letter(value);
    }

    /// <summary>Titles in capitals outside the classic shell, the way both companies' terminals letter them.</summary>
    private string Letter(string title)
    {
        return _theme.Shell == CMUComputerShell.Classic ? title : title.ToUpperInvariant();
    }

    /// <summary>The Company's second, inner hairline round the window.</summary>
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        _theme.Style?.DrawWindow(handle, this);
        if (_theme.Shell != CMUComputerShell.Weyland)
            return;

        var inset = 2.5f * UIScale;
        var box = new UIBox2(inset, inset, PixelWidth - inset, PixelHeight - inset);
        handle.DrawRect(box, _theme.Accent.WithAlpha(0.35f), false);
    }

    private static Control TitleIcon(CMUComputerTheme theme, CMUComputerApp app)
    {
        return new TextureRect
        {
            Texture = theme.AppIcon(app),
            Stretch = TextureRect.StretchMode.KeepCentered,
            MinSize = new Vector2(16, 16),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VAlignment.Center,
        };
    }

    private Button TitleButton(string text, string tooltip)
    {
        var shell = _theme.Shell;
        if (_theme.Style is { } style)
        {
            var styled = new Button { Text = style.TitleButtonText(text), ToolTip = tooltip, MinWidth = 20 };
            styled.Label.FontOverride = style.TitleFont;
            styled.Label.FontColorOverride = style.TitleButtonColour;
            styled.StyleBoxOverride = style.TitleButtonBox();
            styled.ModulateSelfOverride = Color.White;
            return styled;
        }

        var button = new Button { Text = shell == CMUComputerShell.Seegson ? $"[{text}]" : text, ToolTip = tooltip, MinWidth = 20 };
        button.Label.FontOverride = shell == CMUComputerShell.Classic ? _theme.Bold : _theme.ShellFont;
        button.Label.FontColorOverride = shell == CMUComputerShell.Seegson ? _theme.Background : _theme.Accent;
        button.StyleBoxOverride = shell switch
        {
            // Sevastolink's controls are part of the inverted title.
            CMUComputerShell.Seegson => _theme.ButtonBox(_theme.Phosphor),
            CMUComputerShell.Weyland => _theme.ButtonBox(_theme.Background, _theme.Accent.WithAlpha(0.7f)),
            _ => _theme.ButtonBox(_theme.Background),
        };
        button.ModulateSelfOverride = Color.White;
        return button;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            OnFocus?.Invoke(this);
    }

    private void TitleDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        OnFocus?.Invoke(this);
        var now = IoCManager.Resolve<Robust.Shared.Timing.IGameTiming>().RealTime;
        if (now - _lastTitleClick < TimeSpan.FromSeconds(0.4))
        {
            ToggleMaximized();
            _lastTitleClick = TimeSpan.Zero;
            args.Handle();
            return;
        }

        _lastTitleClick = now;
        _dragFrom = args.RelativePosition + _titleBar.Position;
        args.Handle();
    }

    private void GripDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        OnFocus?.Invoke(this);
        _resizeFrom = UserInterfaceManager.MousePositionScaled.Position;
        _sizeAtResize = Size;
        _restore = null;
        args.Handle();
    }

    private void EndDrag(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragFrom = null;
        _resizeFrom = null;
    }

    protected override void FrameUpdate(Robust.Shared.Timing.FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Parent == null || (_dragFrom == null && _resizeFrom == null))
            return;

        var mouse = UserInterfaceManager.MousePositionScaled.Position;
        if (_dragFrom is { } grab)
        {
            var local = mouse - Parent.GlobalPosition - grab;
            var max = Parent.Size - new Vector2(60, 24);
            LayoutContainer.SetPosition(this, Vector2.Clamp(local, Vector2.Zero, Vector2.Max(max, Vector2.Zero)));
            _restore = null;
        }
        else if (_resizeFrom is { } from)
        {
            var size = _sizeAtResize + mouse - from;
            SetSize = Vector2.Clamp(size, MinimumSize, Vector2.Max(Parent.Size - Position, MinimumSize));
        }
    }

    public void ToggleMaximized()
    {
        if (Parent == null)
            return;

        if (_restore is { } restore)
        {
            LayoutContainer.SetPosition(this, restore.Position);
            SetSize = restore.Size;
            _restore = null;
            return;
        }

        _restore = (Position, Size);
        LayoutContainer.SetPosition(this, Vector2.Zero);
        SetSize = Parent.Size;
    }
}

/// <summary>Diagonal hatching in the corner of a desktop window.</summary>
public sealed class CMUResizeGrip : Control
{
    private readonly Color _color;

    public CMUResizeGrip(Color color)
    {
        _color = color;
        MinSize = new Vector2(12, 12);
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Crosshair;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var s = PixelSize;
        for (var i = 1; i <= 3; i++)
        {
            var d = i * s.X / 4f;
            handle.DrawLine(new Vector2(s.X - d, s.Y - 1), new Vector2(s.X - 1, s.Y - d), _color);
        }
    }
}
