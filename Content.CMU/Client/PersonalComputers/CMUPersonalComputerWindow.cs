using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.CMU14.PersonalComputers.DeepWeb;
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
/// A personal computer: the screen in a drawn machine, its bezel round the screen and its front panel under it with
/// the card reader and power button, in the casing of its OS. The screen is a dead tube until the machine is switched
/// on, boots, then shows the login screen or the OS desktop. The pop out button beside the close button moves the
/// machine into an OS window of its own; closing that brings it back.
/// </summary>
public sealed partial class CMUPersonalComputerWindow : DefaultWindow
{
    [Dependency] private IResourceCache _cache = default!;

    // The OS desktop's size inside the machine: by default, and the smallest the window can be dragged to.
    private static readonly Vector2 Screen = new(820, 585);
    private static readonly Vector2 MinScreen = new(560, 405);
    // The window's title bar over the machine.
    private const float TitleHeight = 35;
    // Never wider or taller than this share of the game window.
    private const float MaxScreenFraction = 0.95f;

    private readonly Control _body;
    private readonly CMUComputerMachine _machine;
    private Control? _trackedParent;
    private CMUComputerPanel? _panel;
    private bool? _shownOn;

    /// <summary>Whether the window has not shown the machine on yet since it opened.</summary>
    private bool _firstShow = true;
    private bool _jacked;
    private CMUComputerOsPrototype? _os;
    private readonly PanelContainer _screen;
    private readonly CrtScreenControl _crt;
    private readonly Control _layer;

    private CMUComputerTheme? _theme;
    private CMUComputerDesktop? _desktop;
    private OSWindow? _popOut;
    private CMUComputerBootSplash? _finishedSplash;
    private ICMUComputerHost? _host;
    private Func<CMUComputerDesktop>? _newDesktop;
    private bool _booted;
    private bool _sessionShown;
    private bool _showingDesktop;
    private bool _desktopUsed;
    private string? _shownUser;

    public event Action? OnKeyTyped;
    public event Action? OnClicked;

    /// <summary>The machine starts booting after the power button.</summary>
    public event Action? OnBooting;

    /// <summary>The machine was switched off, or lost power, while the window was open.</summary>
    public event Action? OnShutDown;

    /// <summary>The window opened on a machine someone switched on earlier: it is already running.</summary>
    public event Action? OnResumed;

    /// <summary>
    /// A machine switched on this recently is booting for whoever pressed the button, so the window plays the
    /// boot; switched on longer ago, the window opens on the running machine.
    /// </summary>
    private static readonly TimeSpan JustSwitchedOn = TimeSpan.FromSeconds(2);

    /// <summary>Stands in for a user's name while a guest is on, so a guest session is told apart from a user's.</summary>
    private const string GuestSession = "\u0001guest";

    public CMUPersonalComputerWindow()
    {
        IoCManager.InjectDependencies(this);
        FitMachine();

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

        _machine = new CMUComputerMachine(_body);
        Contents.AddChild(_machine);
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

    /// <summary>
    /// Sizes the window round its screen. The bezel and front panel go round the screen rather than into it, so the
    /// screen keeps the size it had before the machine was drawn and the window grows by the machine's size at this
    /// UI scale; on a game window too small for that, the window shrinks to fit and the screen gives way. Done again
    /// whenever the game window or the UI scale changes.
    /// </summary>
    private void FitMachine()
    {
        var chrome = CMUComputerMachine.Chrome(UIScale) + new Vector2(0, TitleHeight);
        var min = Vector2.Max(MinScreen + chrome, new Vector2(CMUComputerMachine.MinWidth(UIScale), 0));
        var size = Screen + chrome;
        if (Parent is { } parent && parent.Size.X > 0 && parent.Size.Y > 0)
        {
            size = Vector2.Min(size, parent.Size * MaxScreenFraction);
            min = Vector2.Min(min, size);
        }

        MinSize = min;
        SetSize = size;
    }

    /// <summary>The game window shrank: the machine shrinks with it if it no longer fits, whatever size it was.</summary>
    private void KeepOnScreen()
    {
        if (Parent is not { } parent || parent.Size.X <= 0 || parent.Size.Y <= 0)
            return;

        var fits = parent.Size * MaxScreenFraction;
        MinSize = Vector2.Min(MinSize, fits);
        SetSize = Vector2.Min(SetSize, fits);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _trackedParent = Parent;
        if (_trackedParent != null)
            _trackedParent.OnResized += KeepOnScreen;

        FitMachine();
        // Re-measured now, so a window opened centred is centred at its fitted size.
        Measure(Vector2Helpers.Infinity);
    }

    protected override void ExitedTree()
    {
        if (_trackedParent != null)
            _trackedParent.OnResized -= KeepOnScreen;

        _trackedParent = null;
        base.ExitedTree();
    }

    // The machine's size in UI units changes with the UI scale, as its art is drawn on whole screen pixels.
    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        FitMachine();
    }

    /// <summary>The desktop once booted, for previews that drive it.</summary>
    public CMUComputerDesktop? Desktop => _desktop;

    /// <summary>Sets the machine up. It boots, splash then desktop, whenever it is switched on.</summary>
    public void Boot(
        CMUComputerOsPrototype os,
        HashSet<ProtoId<CMUNetworkPrototype>> networks,
        string home,
        List<CMUComputerDocument> documents,
        string terminalName,
        ICMUNetBoardHost? boards,
        string? colony,
        string? colonyName,
        ICMUDeepHost? deep = null,
        ICMUComputerHost? shortcuts = null,
        CMUComputerCasing? casing = null)
    {
        _theme = new CMUComputerTheme(os, _cache);
        if (os.NamedAfterColony && !string.IsNullOrWhiteSpace(colonyName))
            _theme.DisplayName = CMUComputerOsPrototype.ColonyLink(colonyName);
        Title = _theme.DisplayName;
        _os = os;
        _crt.Phosphor = os.Phosphor;

        _layer.RemoveAllChildren();
        _host = shortcuts;
        _sessionShown = false;
        _desktopUsed = false;
        _newDesktop = () =>
        {
            var desktop = new CMUComputerDesktop(_theme!, networks, home, documents, terminalName, boards, colony, colonyName, deep, shortcuts);
            desktop.OnKeyTyped += () =>
            {
                _panel?.Pulse(0.15f);
                OnKeyTyped?.Invoke();
            };
            desktop.OnClicked += () =>
            {
                _panel?.Pulse(0.5f);
                OnClicked?.Invoke();
            };
            desktop.OnShutDown += Close;
            return desktop;
        };
        _desktop = _newDesktop();
        _booted = false;
        _jacked = deep != null;

        // The computer's own casing beats its OS's.
        var built = casing ?? os.Casing;
        _panel = new CMUComputerPanel(built, shortcuts);
        _panel.OnClicked += () => OnClicked?.Invoke();
        _machine.Build(built, _panel);

        _shownOn = null;
        _firstShow = true;
        UpdatePower();
    }

    /// <summary>Boots when the machine comes on; goes dark, losing whatever was open, when it goes off.</summary>
    private void UpdatePower()
    {
        var on = _host?.On ?? true;
        if (_shownOn == on || _theme == null || _os == null)
            return;

        var wasOn = _shownOn == true;
        _shownOn = on;
        _machine.Off = !on;
        _layer.RemoveAllChildren();
        _finishedSplash = null;
        _booted = false;
        _sessionShown = false;
        if (!on)
        {
            if (wasOn)
                OnShutDown?.Invoke();

            _screen.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black };
            _crt.ArtifactAmount = 0f;
            _desktopUsed = true;
            return;
        }

        _screen.PanelOverride = new StyleBoxFlat { BackgroundColor = _os.Background };
        var since = _host?.SinceSwitchedOn ?? TimeSpan.Zero;
        if (_firstShow && since >= JustSwitchedOn)
        {
            _firstShow = false;
            OnResumed?.Invoke();
            // Booted while nobody was looking: straight to whatever is on screen, the same as for whoever left it.
            if (since >= TimeSpan.FromSeconds(_os.BootSoundLength))
            {
                _crt.ArtifactAmount = _jacked ? 0.2f : 0f;
                _crt.ArtifactPeriod = 9f;
                _booted = true;
                UpdateSession();
                return;
            }
        }
        else
        {
            _firstShow = false;
            _panel?.Pulse(4f);
            OnBooting?.Invoke();
        }

        // A jacked terminal never boots clean: the tube tears and smears all the way to the desktop.
        _crt.ArtifactAmount = _jacked ? 0.9f : 0f;
        _crt.ArtifactPeriod = 2.5f;
        var splash = new CMUComputerBootSplash(_theme, _jacked);
        splash.Finished += () => _finishedSplash = splash;
        _layer.AddChild(splash);
    }

    // The splash finishes inside its own frame update, while the UI walks this window's children, so the
    // swap to the desktop waits for the next frame update here, which runs before the children are walked.
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdatePower();
        if (_finishedSplash != null && _desktop != null)
        {
            _finishedSplash.Orphan();
            _finishedSplash = null;
            _booted = true;
            if (_crt.ArtifactAmount > 0)
            {
                _crt.ArtifactAmount = 0.2f;
                _crt.ArtifactPeriod = 9f;
            }
        }

        if (_booted)
            UpdateSession();
    }

    /// <summary>
    /// Shows the login screen while nobody is logged in, and a fresh desktop for each new session, so one user
    /// never finds the last one's windows still open.
    /// </summary>
    private void UpdateSession()
    {
        var guest = _host is { Guest: true } && _host.SessionUser == null;
        // A guest is its own session: the desktop starts fresh when one begins or ends.
        var user = _host?.SessionUser ?? (guest ? GuestSession : null);
        var loggedIn = _host is not { RequireLogin: true } || user != null;
        if (_sessionShown && loggedIn == _showingDesktop && user == _shownUser)
            return;

        _sessionShown = true;
        _showingDesktop = loggedIn;
        _shownUser = user;
        _layer.RemoveAllChildren();
        if (!loggedIn)
        {
            _layer.AddChild(new CMUComputerLogin(_theme!, _host!.TerminalId, _host));
            return;
        }

        if (_desktop == null || _desktopUsed)
        {
            _desktop = _newDesktop!();
            // Whatever this session left open, for whoever looks at the terminal now.
            if (_host != null)
                _desktop.RestoreScreen(_host.Screen);
        }

        _desktopUsed = true;
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
            SetSize = new Vector2(1160, 900),
        };
        _popOut = window;
        _machine.Orphan();
        window.Show();
        window.Root?.AddChild(_machine);
        window.Closed += () =>
        {
            if (_popOut != window)
                return;

            _popOut = null;
            _machine.Orphan();
            Contents.AddChild(_machine);
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
