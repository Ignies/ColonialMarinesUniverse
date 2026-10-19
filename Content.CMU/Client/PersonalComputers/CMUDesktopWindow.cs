using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A program window on the computer desktop: drag it by the title bar, resize it from the corner,
/// double click the title to maximize. Must be a child of a <see cref="LayoutContainer"/>.
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
        PanelOverride = theme.Box(theme.Background, theme.Accent, 2);

        _title = theme.Label(title, theme.Bold, theme.Background);
        _title.HorizontalExpand = true;
        _title.ClipText = true;

        var minimize = TitleButton("_", Loc.GetString("cmu-pc-window-minimize"));
        minimize.OnPressed += _ => OnMinimize?.Invoke(this);
        var close = TitleButton("X", Loc.GetString("cmu-pc-window-close"));
        close.OnPressed += _ => OnClose?.Invoke(this);

        _titleBar = new PanelContainer
        {
            PanelOverride = theme.Box(theme.Accent),
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
        set => _title.Text = value;
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
        var button = new Button { Text = text, ToolTip = tooltip, MinWidth = 20 };
        button.Label.FontOverride = _theme.Bold;
        button.Label.FontColorOverride = _theme.Accent;
        button.StyleBoxOverride = _theme.ButtonBox(_theme.Background);
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
