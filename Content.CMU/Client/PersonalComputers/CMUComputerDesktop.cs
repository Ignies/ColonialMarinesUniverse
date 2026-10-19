using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The OS desktop: wallpaper, program icons, program windows, and a taskbar with the start menu and a clock.
/// </summary>
public sealed partial class CMUComputerDesktop : BoxContainer
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly CMUComputerTheme _theme;
    private readonly HashSet<ProtoId<CMUNetworkPrototype>> _networks;
    private readonly string _home;
    private readonly List<CMUComputerDocument> _documents;
    private readonly string _terminalName;

    private readonly LayoutContainer _area;
    private readonly BoxContainer _tasks;
    private readonly Label _clock;
    private readonly PanelContainer _startMenu;
    private readonly List<CMUDesktopIcon> _icons = new();
    private readonly List<CMUDesktopWindow> _windows = new();
    private readonly Dictionary<CMUDesktopWindow, Button> _taskButtons = new();
    private readonly TimeSpan _bootedAt;
    private readonly Texture? _wallpaper;

    public event Action? OnKeyTyped;
    public event Action? OnClicked;
    public event Action? OnShutDown;

    public CMUComputerDesktop(
        CMUComputerTheme theme,
        HashSet<ProtoId<CMUNetworkPrototype>> networks,
        string home,
        List<CMUComputerDocument> documents,
        string terminalName)
    {
        IoCManager.InjectDependencies(this);
        _theme = theme;
        _networks = networks;
        _home = home;
        _documents = documents;
        _terminalName = terminalName;
        _bootedAt = _timing.RealTime;
        if (theme.TryGetTexture(theme.Os.Wallpaper, out var wallpaper))
            _wallpaper = wallpaper;

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        _area = new LayoutContainer { VerticalExpand = true, HorizontalExpand = true, MouseFilter = MouseFilterMode.Stop };
        _area.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            SelectIcon(null);
            _startMenu!.Visible = false;
        };

        var icons = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        AddIcon(icons, CMUComputerApp.Browser, "cmu-pc-app-browser");
        AddIcon(icons, CMUComputerApp.Files, "cmu-pc-app-files");
        AddIcon(icons, CMUComputerApp.System, "cmu-pc-app-system");
        _area.AddChild(icons);
        LayoutContainer.SetPosition(icons, new Vector2(10, 10));

        _startMenu = BuildStartMenu();
        _area.AddChild(_startMenu);
        // Anchored to the bottom left and grown upwards, so it sits on the taskbar whatever its height.
        LayoutContainer.SetAnchorPreset(_startMenu, LayoutContainer.LayoutPreset.BottomLeft);
        LayoutContainer.SetGrowVertical(_startMenu, LayoutContainer.GrowDirection.Begin);

        var start = theme.Button(Loc.GetString(theme.Os.StartLabel));
        start.StyleBoxOverride = theme.ButtonBox(theme.Accent);
        start.MinWidth = 64;
        start.Label.FontColorOverride = theme.Background;
        start.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            ToggleStartMenu();
        };

        _tasks = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(6, 0),
        };
        _clock = theme.Label(string.Empty, theme.Bold);
        _clock.Margin = new Thickness(0, 0, 8, 0);

        AddChild(_area);
        AddChild(new CMUTerminalRule { Color = theme.Accent });
        AddChild(new PanelContainer
        {
            PanelOverride = theme.Box(Color.InterpolateBetween(theme.Background, theme.Accent, 0.12f)),
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Margin = new Thickness(3),
                    Children = { start, _tasks, _clock },
                },
            },
        });
    }

    private void AddIcon(BoxContainer column, CMUComputerApp app, string label)
    {
        var icon = new CMUDesktopIcon(_theme, app, Loc.GetString(label));
        icon.OnSelect += SelectIcon;
        icon.OnOpen += Open;
        _icons.Add(icon);
        column.AddChild(icon);
    }

    private void SelectIcon(CMUDesktopIcon? selected)
    {
        foreach (var icon in _icons)
            icon.Selected = icon == selected;

        if (selected != null)
            OnClicked?.Invoke();
    }

    private PanelContainer BuildStartMenu()
    {
        var list = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(4), SeparationOverride = 2 };
        list.AddChild(_theme.Label(Loc.GetString(_theme.Os.Name), _theme.Bold, _theme.Accent));
        list.AddChild(new CMUTerminalRule { Color = _theme.Accent, Margin = new Thickness(0, 2, 0, 4) });

        foreach (var (app, label) in new[]
                 {
                     (CMUComputerApp.Browser, "cmu-pc-app-browser"),
                     (CMUComputerApp.Files, "cmu-pc-app-files"),
                     (CMUComputerApp.System, "cmu-pc-app-system"),
                 })
        {
            var button = _theme.AppButton(app, Loc.GetString(label));
            button.HorizontalExpand = true;
            button.OnPressed += _ =>
            {
                _startMenu.Visible = false;
                Open(app);
            };
            list.AddChild(button);
        }

        var shutDown = _theme.Button(Loc.GetString("cmu-pc-start-shut-down"));
        shutDown.OnPressed += _ => OnShutDown?.Invoke();
        list.AddChild(new CMUTerminalRule { Color = _theme.Accent, Margin = new Thickness(0, 4, 0, 2) });
        list.AddChild(shutDown);

        return new PanelContainer
        {
            Visible = false,
            MinWidth = 200,
            PanelOverride = _theme.Box(_theme.Background, _theme.Accent, 2),
            Children = { list },
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        // Shift clock: the round starts at 06:00 colony time.
        var time = TimeSpan.FromHours(6) + _timing.CurTime;
        _clock.Text = $"{time.Hours:00}:{time.Minutes:00}";
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, _theme.Background);
        if (_wallpaper != null)
        {
            handle.DrawTextureRect(_wallpaper, PixelSizeBox);
            return;
        }

        // Without a wallpaper: a faint grid and the OS name in the corner, like an idle workstation.
        var grid = _theme.Phosphor.WithAlpha(0.05f);
        var step = 32 * UIScale;
        for (var x = 0f; x < PixelWidth; x += step)
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, PixelHeight), grid);
        for (var y = 0f; y < PixelHeight; y += step)
            handle.DrawLine(new Vector2(0, y), new Vector2(PixelWidth, y), grid);

        // The largest size that keeps the name within 70% of the screen; long corporate names step down.
        var name = Loc.GetString(_theme.Os.Name);
        Font font = default!;
        var width = 0f;
        foreach (var size in new[] { 22, 18, 15, 12 })
        {
            font = _theme.GetFont(CMUNetFont.Display, size, true);
            width = 0f;
            foreach (var rune in name.EnumerateRunes())
            {
                if (font.TryGetCharMetrics(rune, UIScale, out var metrics))
                    width += metrics.Advance;
            }

            if (width <= PixelWidth * 0.7f)
                break;
        }

        handle.DrawString(font, new Vector2(PixelWidth - width - 16 * UIScale, PixelHeight - 80 * UIScale), name, UIScale,
            _theme.Phosphor.WithAlpha(0.18f));
    }

    /// <summary>The browser in a desktop window, if one is open.</summary>
    public CMUNetBrowser? Browser
    {
        get
        {
            foreach (var window in _windows)
            {
                if (window.Content is CMUNetBrowser browser)
                    return browser;
            }

            return null;
        }
    }

    public void ToggleStartMenu()
    {
        _startMenu.Visible = !_startMenu.Visible;
        _startMenu.SetPositionLast();
    }

    public void Open(CMUComputerApp app)
    {
        OnClicked?.Invoke();
        foreach (var window in _windows)
        {
            if (window.App != app)
                continue;

            Restore(window);
            return;
        }

        switch (app)
        {
            case CMUComputerApp.Browser:
                var browser = NewBrowser();
                browser.Navigate(_home);
                AddWindow(app, Loc.GetString("cmu-pc-app-browser"), browser, new Vector2(640, 440));
                break;
            case CMUComputerApp.Files:
                AddWindow(app, Loc.GetString("cmu-pc-app-files"), BuildFiles(), new Vector2(560, 380));
                break;
            default:
                AddWindow(app, Loc.GetString("cmu-pc-app-system"), BuildSystem(), new Vector2(560, 300));
                break;
        }
    }

    private CMUNetBrowser NewBrowser()
    {
        var browser = new CMUNetBrowser(_theme, _networks, _home);
        browser.OnKeyTyped += () => OnKeyTyped?.Invoke();
        browser.OnClicked += () => OnClicked?.Invoke();
        return browser;
    }

    private void AddWindow(CMUComputerApp app, string title, Control content, Vector2 size)
    {
        var window = new CMUDesktopWindow(_theme, app, title, content);
        window.OnClose += Close;
        window.OnMinimize += Minimize;
        window.OnFocus += Focus;

        var offset = 110 + _windows.Count * 26;
        _area.AddChild(window);
        LayoutContainer.SetPosition(window, new Vector2(offset, 14 + _windows.Count * 22));
        window.SetSize = Vector2.Min(size, Vector2.Max(_area.Size - new Vector2(offset + 6, 40), new Vector2(260, 160)));
        _windows.Add(window);

        if (content is CMUNetBrowser browser)
            browser.OnTitleChanged += pageTitle => SetBrowserTitle(window, pageTitle);

        var task = _theme.AppButton(app, title);
        task.MinWidth = 110;
        task.MaxWidth = 170;
        task.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            if (window.Visible && window == _windows[^1])
                Minimize(window);
            else
                Restore(window);
        };
        _taskButtons[window] = task;
        _tasks.AddChild(task);
        Focus(window);
    }

    private void SetBrowserTitle(CMUDesktopWindow window, string pageTitle)
    {
        window.Title = pageTitle.Length == 0
            ? Loc.GetString("cmu-pc-app-browser")
            : Loc.GetString("cmu-pc-browser-window-title", ("page", pageTitle));
    }

    private void Close(CMUDesktopWindow window)
    {
        OnClicked?.Invoke();
        _windows.Remove(window);
        window.Orphan();
        if (_taskButtons.Remove(window, out var task))
            task.Orphan();
    }

    private void Minimize(CMUDesktopWindow window)
    {
        window.Visible = false;
        UpdateTasks();
    }

    private void Restore(CMUDesktopWindow window)
    {
        window.Visible = true;
        Focus(window);
    }

    private void Focus(CMUDesktopWindow window)
    {
        _windows.Remove(window);
        _windows.Add(window);
        window.SetPositionLast();
        _startMenu.SetPositionLast();
        UpdateTasks();
    }

    private void UpdateTasks()
    {
        CMUDesktopWindow? top = null;
        for (var i = _windows.Count - 1; i >= 0; i--)
        {
            if (!_windows[i].Visible)
                continue;

            top = _windows[i];
            break;
        }

        foreach (var (window, task) in _taskButtons)
        {
            var active = window == top;
            task.StyleBoxOverride = active ? _theme.ButtonBox(_theme.Accent) : _theme.ButtonBox(_theme.Background, _theme.Accent);
            task.Label.FontColorOverride = active ? _theme.Background : _theme.Accent;
        }
    }

    private Control BuildFiles()
    {
        var all = new List<CMUComputerDocument>(_theme.Os.Documents);
        all.AddRange(_documents);

        var viewer = new CMUNetPageView(_theme);
        var list = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(4), SeparationOverride = 2 };
        var resources = IoCManager.Resolve<Robust.Shared.ContentPack.IResourceManager>();

        void Show(CMUComputerDocument document)
        {
            OnClicked?.Invoke();
            using var reader = resources.ContentFileReadText(document.Path);
            viewer.ShowDocument(reader.ReadToEnd(), null);
        }

        foreach (var document in all)
        {
            var button = _theme.Button(document.Name);
            button.HorizontalExpand = true;
            button.Label.HorizontalAlignment = HAlignment.Left;
            button.OnPressed += _ => Show(document);
            list.AddChild(button);
        }

        if (all.Count == 0)
            list.AddChild(_theme.Label(Loc.GetString("cmu-pc-files-empty"), _theme.Font, _theme.Dim));
        else
            Show(all[0]);

        return new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            VerticalExpand = true,
            Children =
            {
                new ScrollContainer { MinWidth = 150, HScrollEnabled = false, Children = { list } },
                new CMUTerminalRule { Color = _theme.Accent, MinWidth = 1, HorizontalExpand = false, VerticalExpand = true },
                viewer,
            },
        };
    }

    private Control BuildSystem()
    {
        var box = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(10), SeparationOverride = 4 };
        // Lines clip rather than spill out of the window when it is narrower than the OS name.
        box.OnChildAdded += child =>
        {
            if (child is Label label)
                label.ClipText = true;
        };
        box.AddChild(_theme.Label(Loc.GetString(_theme.Os.Name), _theme.Bold));
        if (_theme.Os.BootLines.Count > 0)
            box.AddChild(_theme.Label(Loc.GetString(_theme.Os.BootLines[0]), _theme.Font, _theme.Dim));

        box.AddChild(new CMUTerminalRule { Color = _theme.Accent, Margin = new Thickness(0, 4) });
        box.AddChild(_theme.Label(Loc.GetString("cmu-pc-system-terminal", ("name", _terminalName))));

        var names = new List<string>();
        foreach (var network in _networks)
        {
            if (_prototype.TryIndex(network, out var proto))
                names.Add(Loc.GetString(proto.Name));
        }

        names.Sort(string.CompareOrdinal);
        box.AddChild(_theme.Label(Loc.GetString("cmu-pc-system-networks")));
        foreach (var name in names)
            box.AddChild(_theme.Label($"  {name}", _theme.Font, _theme.Dim));

        var uptime = _theme.Label(string.Empty);
        box.AddChild(uptime);
        box.AddChild(new CMUSystemUptime(uptime, _bootedAt, _timing));
        return box;
    }
}

/// <summary>Keeps the System program's uptime line current.</summary>
public sealed class CMUSystemUptime : Control
{
    private readonly Label _label;
    private readonly TimeSpan _since;
    private readonly IGameTiming _timing;

    public CMUSystemUptime(Label label, TimeSpan since, IGameTiming timing)
    {
        _label = label;
        _since = since;
        _timing = timing;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var up = _timing.RealTime - _since;
        _label.Text = Loc.GetString("cmu-pc-system-uptime", ("time", $"{(int) up.TotalMinutes:00}:{up.Seconds:00}"));
    }
}
