using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The screen of a personal computer: the boot splash, then the OS desktop. The pop out button beside the close
/// button moves the whole screen into an OS window of its own; closing that window brings it back.
/// </summary>
public sealed partial class CMUPersonalComputerWindow : DefaultWindow
{
    [Dependency] private IResourceCache _cache = default!;

    private readonly Control _body;
    private readonly PanelContainer _screen;
    private readonly CrtScreenControl _crt;
    private readonly Control _layer;

    private CMUComputerTheme? _theme;
    private CMUComputerDesktop? _desktop;
    private OSWindow? _popOut;
    private CMUComputerBootSplash? _finishedSplash;

    public event Action? OnKeyTyped;
    public event Action? OnClicked;

    public CMUPersonalComputerWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(560, 440);
        SetSize = new Vector2(820, 620);

        _layer = new Control { HorizontalExpand = true, VerticalExpand = true };
        _screen = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { _layer, new CMUTerminalScanlines() },
        };
        _crt = new CrtScreenControl
        {
            Source = _screen,
            Roll = false,
            Grain = false,
            // Barrel curvature drops pixels from one pixel rules and underlines.
            Curvature = 0f,
            Vignette = 0.25f,
        };
        _body = new Control
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { _screen, _crt },
        };

        Contents.AddChild(_body);
        Contents.Margin = new Thickness(0);

        // Beside the close button, as the crayon editor's is.
        var popOut = new Button
        {
            Text = Loc.GetString("cmu-pc-pop-out"),
            ToolTip = Loc.GetString("cmu-pc-pop-out-tooltip"),
            StyleClasses = { "OpenRight" },
            VerticalAlignment = VAlignment.Center,
        };
        popOut.OnPressed += _ => PopOut();
        CloseButton.Parent?.AddChild(popOut);
        popOut.SetPositionInParent(CloseButton.GetPositionInParent());

        OnClose += ClosePopOut;
    }

    /// <summary>The desktop once booted, for previews that drive it.</summary>
    public CMUComputerDesktop? Desktop => _desktop;

    /// <summary>Plays the boot splash, then shows the desktop.</summary>
    public void Boot(
        CMUComputerOsPrototype os,
        HashSet<ProtoId<CMUNetworkPrototype>> networks,
        string home,
        List<CMUComputerDocument> documents,
        string terminalName)
    {
        _theme = new CMUComputerTheme(os, _cache);
        Title = Loc.GetString(os.Name);
        _screen.PanelOverride = new StyleBoxFlat { BackgroundColor = os.Background };
        _crt.Phosphor = os.Phosphor;

        _layer.RemoveAllChildren();
        _desktop = new CMUComputerDesktop(_theme, networks, home, documents, terminalName);
        _desktop.OnKeyTyped += () => OnKeyTyped?.Invoke();
        _desktop.OnClicked += () => OnClicked?.Invoke();
        _desktop.OnShutDown += Close;

        var splash = new CMUComputerBootSplash(_theme);
        splash.Finished += () => _finishedSplash = splash;
        _layer.AddChild(splash);
    }

    // The splash finishes inside its own frame update, while the UI walks this window's children, so the
    // swap to the desktop waits for the next frame update here, which runs before the children are walked.
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_finishedSplash == null || _desktop == null)
            return;

        _finishedSplash.Orphan();
        _finishedSplash = null;
        _layer.AddChild(_desktop);
    }

    /// <summary>
    /// Moves the screen into an OS window, which can go on another monitor and be made larger. This window
    /// stays open but hidden, so the terminal still closes when its user walks away from the computer.
    /// </summary>
    public void PopOut()
    {
        if (_popOut != null)
            return;

        OnClicked?.Invoke();
        var window = new OSWindow
        {
            Title = Title ?? string.Empty,
            SetSize = new Vector2(1100, 820),
        };
        _popOut = window;
        _body.Orphan();
        window.Show();
        window.Root?.AddChild(_body);
        window.Closed += () =>
        {
            if (_popOut != window)
                return;

            _popOut = null;
            _body.Orphan();
            Contents.AddChild(_body);
            Visible = true;
        };
        Visible = false;
    }

    /// <summary>Closes the popped out window, which brings the screen back into this one.</summary>
    public void ReturnPopOut()
    {
        _popOut?.Close();
    }

    /// <summary>The terminal closing takes the popped out window with it.</summary>
    private void ClosePopOut()
    {
        var window = _popOut;
        _popOut = null;
        window?.Close();
    }
}

/// <summary>One pixel rule.</summary>
public sealed class CMUTerminalRule : Control
{
    public Color Color = Color.White;

    public CMUTerminalRule()
    {
        MinHeight = 1;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, Color);
    }
}

/// <summary>Always on scanlines, so the screen reads as a tube even with the CRT UI theme off.</summary>
public sealed class CMUTerminalScanlines : Control
{
    public CMUTerminalScanlines()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var color = Color.Black.WithAlpha(0.22f);
        for (var y = 0; y < PixelHeight; y += 3)
            handle.DrawRect(new UIBox2(0, y, PixelWidth, y + 1), color);
    }
}
