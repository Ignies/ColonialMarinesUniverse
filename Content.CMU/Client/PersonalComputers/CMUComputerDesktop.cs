using System.Linq;
using System.Numerics;
using Content.Client.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The OS desktop: wallpaper, program icons, program windows, and a taskbar with the start menu and a clock, laid
/// out by the OS's <see cref="CMUComputerShell"/>. Whatever the layout, the same icons open the same programs in
/// the same windows; only the frame round them changes.
/// </summary>
public sealed partial class CMUComputerDesktop : BoxContainer
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private readonly CMUComputerTheme _theme;
    private readonly HashSet<ProtoId<CMUNetworkPrototype>> _networks;
    private readonly string _home;
    private readonly List<CMUComputerDocument> _documents;
    private readonly string _terminalName;
    private readonly ICMUNetBoardHost? _boards;
    private readonly string? _colony;
    private readonly string? _colonyName;
    private readonly ICMUDeepHost? _deep;
    private readonly ICMUComputerHost? _shortcuts;
    private readonly ProtoId<CMUNetworkPrototype> _deepNetwork = "CMUNetDeep";

    private readonly LayoutContainer _area;
    private readonly Label _trace;
    private readonly BoxContainer _tasks;
    private readonly Label _clock;
    private readonly PanelContainer _startMenu;
    private readonly Container _iconGrid;
    private readonly List<CMUDesktopIcon> _icons = new();
    private string _shownShortcuts = string.Empty;
    private float _shortcutCheck;
    private readonly List<CMUDesktopWindow> _windows = new();
    private readonly Dictionary<CMUDesktopWindow, Button> _taskButtons = new();
    private readonly TimeSpan _bootedAt;
    private readonly Texture? _wallpaper;
    private Texture? _foundWallpaper;
    private ProtoId<CMUWallpaperPrototype>? _shownWallpaper;
    private string _savedScreen = string.Empty;
    private float _screenCheck;
    private bool _restored;
    private Label? _inquiry;
    private float _blink;

    public event Action? OnKeyTyped;
    public event Action? OnClicked;
    public event Action? OnShutDown;

    public CMUComputerDesktop(
        CMUComputerTheme theme,
        HashSet<ProtoId<CMUNetworkPrototype>> networks,
        string home,
        List<CMUComputerDocument> documents,
        string terminalName,
        ICMUNetBoardHost? boards,
        string? colony,
        string? colonyName,
        ICMUDeepHost? deep,
        ICMUComputerHost? shortcuts = null)
    {
        IoCManager.InjectDependencies(this);
        _deep = deep;
        _shortcuts = shortcuts;
        _boards = boards;
        _colony = colony;
        _colonyName = colonyName;
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

        // Programs then saved links: on the desktop in columns, or down the shell's own menu or dock.
        _iconGrid = theme.Style?.NewIconBox() ?? theme.Shell switch
        {
            CMUComputerShell.Seegson => new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 1 },
            CMUComputerShell.Weyland => new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 },
            _ => new GridContainer { Rows = 5, VSeparationOverride = 6, HSeparationOverride = 6 },
        };
        BuildIcons();

        _trace = new Label
        {
            Visible = false,
            FontOverride = theme.Bold,
            FontColorOverride = Color.FromHex("#FF4040"),
            HorizontalAlignment = HAlignment.Center,
        };
        _area.AddChild(_trace);
        LayoutContainer.SetAnchorPreset(_trace, LayoutContainer.LayoutPreset.CenterTop);
        LayoutContainer.SetGrowHorizontal(_trace, LayoutContainer.GrowDirection.Both);

        _startMenu = BuildStartMenu();
        _area.AddChild(_startMenu);
        // Anchored to the bottom left and grown upwards, so it sits on the taskbar whatever its height.
        LayoutContainer.SetAnchorPreset(_startMenu, LayoutContainer.LayoutPreset.BottomLeft);
        LayoutContainer.SetGrowVertical(_startMenu, LayoutContainer.GrowDirection.Begin);

        _tasks = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(6, 0),
        };
        _clock = theme.Label(string.Empty, theme.ShellFont);

        if (theme.Style is { } style)
        {
            style.Build(this, new CMUShellParts
            {
                Theme = theme,
                Area = _area,
                Icons = _iconGrid,
                Tasks = _tasks,
                Clock = _clock,
                SessionButtons = SessionButtons,
                ToggleStartMenu = ToggleStartMenu,
                Clicked = () => OnClicked?.Invoke(),
                Host = _shortcuts,
            });
            return;
        }

        switch (theme.Shell)
        {
            case CMUComputerShell.Seegson:
                BuildSeegson();
                break;
            case CMUComputerShell.Weyland:
                BuildWeyland();
                break;
            default:
                BuildClassic();
                break;
        }
    }

    /// <summary>Icons on the wallpaper, a start button and a taskbar along the bottom.</summary>
    private void BuildClassic()
    {
        _area.AddChild(_iconGrid);
        _iconGrid.SetPositionFirst();
        LayoutContainer.SetPosition(_iconGrid, new Vector2(10, 10));

        var start = _theme.Button(Loc.GetString(_theme.Os.StartLabel));
        start.StyleBoxOverride = _theme.ButtonBox(_theme.Accent);
        start.MinWidth = 64;
        start.Label.FontColorOverride = _theme.Background;
        start.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            ToggleStartMenu();
        };
        _clock.Margin = new Thickness(0, 0, 8, 0);

        AddChild(_area);
        AddChild(new CMUTerminalRule { Color = _theme.Accent });
        AddChild(new PanelContainer
        {
            PanelOverride = _theme.Box(Color.InterpolateBetween(_theme.Background, _theme.Accent, 0.12f)),
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

    /// <summary>
    /// Sevastolink, the way Seegson's terminals look on Sevastopol: the network name across a header bar, a numbered
    /// main menu in inverted blocks down the left that opens with one click, and the open programs listed in a
    /// strip along the bottom. No start button: the session's controls sit under the menu.
    /// </summary>
    private void BuildSeegson()
    {
        var band = Color.InterpolateBetween(_theme.Background, _theme.Phosphor, 0.1f);
        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 4),
            SeparationOverride = 10,
        };
        if (_theme.Logo is { } logo)
        {
            header.AddChild(new TextureRect
            {
                Texture = logo,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(64, 14),
                MaxHeight = 16,
                VerticalAlignment = VAlignment.Center,
            });
        }

        var name = _theme.Label(_theme.DisplayName.ToUpperInvariant(), _theme.GetFont(CMUNetFont.Terminal, 13), _theme.Bright);
        name.HorizontalExpand = true;
        name.VerticalAlignment = VAlignment.Center;
        header.AddChild(name);
        if (_shortcuts != null)
        {
            var terminal = _theme.Label(Loc.GetString("cmu-os-shell-terminal", ("id", _shortcuts.TerminalId)), _theme.GetFont(CMUNetFont.Terminal, 10));
            terminal.VerticalAlignment = VAlignment.Center;
            header.AddChild(terminal);
        }

        _clock.VerticalAlignment = VAlignment.Center;
        _clock.FontColorOverride = _theme.Bright;
        _clock.FontOverride = _theme.GetFont(CMUNetFont.Terminal, 13);
        header.AddChild(_clock);

        var menu = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(4), SeparationOverride = 2 };
        menu.AddChild(SeegsonHeading(Loc.GetString("cmu-os-seegson-menu")));
        menu.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _iconGrid },
        });
        menu.AddChild(new CMUTerminalRule { Color = _theme.Phosphor, Margin = new Thickness(0, 2) });
        if (_shortcuts?.SessionUser is { } user)
        {
            var who = _theme.Label(Loc.GetString("cmu-os-shell-user", ("name", user.ToUpperInvariant())), _theme.ShellSmall, _theme.Dim);
            who.ClipText = true;
            menu.AddChild(who);
        }

        foreach (var control in SessionButtons())
            menu.AddChild(control);

        var column = new PanelContainer
        {
            MinWidth = 168,
            MaxWidth = 168,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = _theme.Background,
                BorderColor = _theme.Phosphor,
                BorderThickness = new Thickness(0, 0, 2, 0),
            },
            Children = { menu },
        };

        var strip = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 3),
            SeparationOverride = 6,
        };
        var active = _theme.Label(Loc.GetString("cmu-os-seegson-active"), _theme.ShellSmall, _theme.Dim);
        active.VerticalAlignment = VAlignment.Center;
        strip.AddChild(active);
        _tasks.Margin = new Thickness(0);
        strip.AddChild(_tasks);
        var maker = _theme.Label(Loc.GetString("cmu-os-seegson-maker"), _theme.ShellSmall, _theme.Dim);
        maker.VerticalAlignment = VAlignment.Center;
        strip.AddChild(maker);

        AddChild(new PanelContainer { PanelOverride = _theme.Box(band), Children = { header } });
        AddChild(new CMUTerminalRule { Color = _theme.Phosphor });
        AddChild(new CMUTerminalRule { Color = _theme.Phosphor, Margin = new Thickness(0, 2, 0, 0) });
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            VerticalExpand = true,
            Children = { column, _area },
        });
        AddChild(new CMUTerminalRule { Color = _theme.Phosphor });
        AddChild(new PanelContainer { PanelOverride = _theme.Box(band), Children = { strip } });
    }

    /// <summary>An inverted block heading, the way Sevastolink titles its menus.</summary>
    private Control SeegsonHeading(string text)
    {
        return new PanelContainer
        {
            PanelOverride = _theme.Box(_theme.Phosphor),
            Children = { _theme.Label($" {text.ToUpperInvariant()}", _theme.ShellFont, _theme.Background) },
        };
    }

    /// <summary>
    /// A Company workstation in the MU/TH/UR manner: the winged logo and slogan across the top, the programs as
    /// Semiotic Standard tiles down a dock on the left, and along the bottom the directory, the open programs and
    /// the inquiry line.
    /// </summary>
    private void BuildWeyland()
    {
        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(10, 5),
            SeparationOverride = 12,
        };
        if (_theme.Logo is { } logo)
        {
            header.AddChild(new TextureRect
            {
                Texture = logo,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(46, 16),
                MaxHeight = 32,
                VerticalAlignment = VAlignment.Center,
            });
        }

        var brand = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        brand.AddChild(_theme.Label(Loc.GetString("cmu-os-weyland-company"), _theme.ShellFont, _theme.Accent));
        brand.AddChild(_theme.Label(Loc.GetString("cmu-os-weyland-slogan"), _theme.ShellSmall, _theme.Dim));
        header.AddChild(brand);

        var right = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalAlignment = VAlignment.Center };
        _clock.HorizontalAlignment = HAlignment.Right;
        _clock.FontColorOverride = _theme.Accent;
        right.AddChild(_clock);
        var where = _theme.Label(_shortcuts != null
            ? Loc.GetString("cmu-os-weyland-station", ("id", _shortcuts.TerminalId))
            : _theme.DisplayName, _theme.ShellSmall, _theme.Dim);
        where.HorizontalAlignment = HAlignment.Right;
        right.AddChild(where);
        header.AddChild(right);

        var dock = new PanelContainer
        {
            MinWidth = 92,
            MaxWidth = 92,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(_theme.Background, _theme.Accent, 0.05f),
                BorderColor = _theme.Accent.WithAlpha(0.6f),
                BorderThickness = new Thickness(0, 0, 1, 0),
            },
            Children =
            {
                new ScrollContainer
                {
                    HScrollEnabled = false,
                    Margin = new Thickness(4, 6),
                    Children = { _iconGrid },
                },
            },
        };

        var directory = _theme.Button(Loc.GetString("cmu-os-weyland-directory"));
        directory.Label.FontOverride = _theme.ShellFont;
        directory.Label.FontColorOverride = _theme.Accent;
        directory.StyleBoxOverride = _theme.ButtonBox(_theme.Background, _theme.Accent);
        directory.MinWidth = 84;
        directory.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            ToggleStartMenu();
        };

        _inquiry = _theme.Label(string.Empty, _theme.Font, _theme.Dim);
        _inquiry.VerticalAlignment = VAlignment.Center;
        _inquiry.Margin = new Thickness(0, 0, 6, 0);

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = _theme.Background,
                BorderColor = _theme.Accent,
                BorderThickness = new Thickness(0, 0, 0, 1),
            },
            Children = { header },
        });
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            VerticalExpand = true,
            Children = { dock, _area },
        });
        AddChild(new CMUWeylandRule(_theme.Accent));
        AddChild(new PanelContainer
        {
            PanelOverride = _theme.Box(_theme.Background),
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Margin = new Thickness(4, 3),
                    Children = { directory, _tasks, _inquiry },
                },
            },
        });
    }

    /// <summary>Log off, when the terminal takes cards, and shut down: the start menu's last entries.</summary>
    private List<Control> SessionButtons()
    {
        var controls = new List<Control>();
        if (_shortcuts is { RequireLogin: true })
        {
            var logOff = ShellButton(Loc.GetString("cmu-pc-start-log-off"));
            logOff.HorizontalExpand = true;
            logOff.OnPressed += _ =>
            {
                _startMenu.Visible = false;
                OnClicked?.Invoke();
                _shortcuts.Logout();
            };
            controls.Add(logOff);
        }

        var shutDown = ShellButton(Loc.GetString("cmu-pc-start-shut-down"));
        shutDown.HorizontalExpand = true;
        shutDown.OnPressed += _ => OnShutDown?.Invoke();
        controls.Add(shutDown);
        return controls;
    }

    private Button ShellButton(string text)
    {
        var button = _theme.Button(_theme.Shell == CMUComputerShell.Classic ? text : text.ToUpperInvariant());
        button.Label.FontOverride = _theme.ShellFont;
        return button;
    }

    private void BuildIcons()
    {
        _icons.Clear();
        _iconGrid.RemoveAllChildren();
        AddIcon(CMUComputerApp.Browser, "cmu-pc-app-browser");
        AddIcon(CMUComputerApp.Files, "cmu-pc-app-files");
        AddIcon(CMUComputerApp.System, "cmu-pc-app-system");
        if (_shortcuts != null && _cfg.GetCVar(CMUPersonalComputerCVars.Mail))
            AddIcon(CMUComputerApp.Mail, "cmu-pc-app-mail");
        if (_deep != null)
            AddIcon(CMUComputerApp.Deeps, "cmu-pc-app-deeps");

        if (_shortcuts == null)
            return;

        // A colony console's own screens, each opening the old console window.
        var host = _shortcuts;
        for (var i = 0; i < host.ConsoleApps.Count; i++)
        {
            var index = i;
            var name = host.ConsoleApps[i];
            var icon = new CMUDesktopIcon(_theme, CMUComputerApp.Console, Loc.TryGetString(name, out var localized) ? localized : name);
            icon.OnSelect += SelectIcon;
            icon.OnOpen += _ =>
            {
                OnClicked?.Invoke();
                host.OpenConsoleApp(index);
            };
            _icons.Add(icon);
            _iconGrid.AddChild(icon);
        }

        foreach (var shortcut in _shortcuts.Shortcuts)
        {
            var app = _deep != null && IsDeepLink(shortcut.Address) ? CMUComputerApp.Deeps : CMUComputerApp.Browser;
            var icon = new CMUDesktopIcon(_theme, app, shortcut.Title, shortcut.Address);
            icon.OnSelect += SelectIcon;
            icon.OnOpen += _ => OpenLink(shortcut.Address);
            icon.OnRemove += _ =>
            {
                OnClicked?.Invoke();
                _shortcuts.RemoveShortcut(shortcut.Address);
            };
            _icons.Add(icon);
            _iconGrid.AddChild(icon);
        }

        AddGameIcons();
    }

    private void AddIcon(CMUComputerApp app, string label)
    {
        var icon = new CMUDesktopIcon(_theme, app, Loc.GetString(label));
        icon.OnSelect += SelectIcon;
        icon.OnOpen += Open;
        _icons.Add(icon);
        _iconGrid.AddChild(icon);
    }

    /// <summary>Deep sites, ad pages and The deeps.exi home open in The deeps.exi.</summary>
    private bool IsDeepLink(string address)
    {
        if (address.StartsWith("deeps://", StringComparison.OrdinalIgnoreCase))
            return true;

        var host = CMUExtraNetSystem.Split(address).Host;
        return Content.Shared.CMU14.PersonalComputers.DeepWeb.CMUDeepAddress.IsAd(host)
            || _deep != null && _deep.Aliases.Values.Contains(host);
    }

    /// <summary>A saved page, always in a new window of the browser it belongs to.</summary>
    private void OpenLink(string address)
    {
        OnClicked?.Invoke();
        if (_deep != null && IsDeepLink(address))
        {
            var deeps = NewDeepsBrowser(_deep);
            deeps.Navigate(address);
            AddWindow(CMUComputerApp.Deeps, Loc.GetString("cmu-pc-app-deeps"), deeps, new Vector2(640, 440));
            return;
        }

        var browser = NewBrowser();
        browser.Navigate(address);
        AddWindow(CMUComputerApp.Browser, Loc.GetString("cmu-pc-app-browser"), browser, new Vector2(640, 440));
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
        var heading = _theme.Style?.StartMenuHeading
                      ?? (_theme.Shell == CMUComputerShell.Weyland ? Loc.GetString("cmu-os-weyland-directory") : _theme.DisplayName);
        list.AddChild(_theme.Label(heading, _theme.ShellFont, _theme.Accent));
        list.AddChild(new CMUTerminalRule { Color = _theme.Accent, Margin = new Thickness(0, 2, 0, 4) });

        foreach (var (app, label) in new[]
                 {
                     (CMUComputerApp.Browser, "cmu-pc-app-browser"),
                     (CMUComputerApp.Files, "cmu-pc-app-files"),
                     (CMUComputerApp.System, "cmu-pc-app-system"),
                     (CMUComputerApp.Deeps, "cmu-pc-app-deeps"),
                 })
        {
            if (app == CMUComputerApp.Deeps && _deep == null)
                continue;

            var text = Loc.GetString(label);
            var button = _theme.AppButton(app, _theme.Shell == CMUComputerShell.Classic ? text : text.ToUpperInvariant());
            button.Label.FontOverride = _theme.ShellFont;
            button.HorizontalExpand = true;
            button.OnPressed += _ =>
            {
                _startMenu.Visible = false;
                Open(app);
            };
            list.AddChild(button);
        }

        list.AddChild(new CMUTerminalRule { Color = _theme.Accent, Margin = new Thickness(0, 4, 0, 2) });
        foreach (var control in SessionButtons())
            list.AddChild(control);

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
        RefreshShortcuts(args.DeltaSeconds);
        SaveScreen(args.DeltaSeconds);
        _theme.Style?.FrameUpdate(args.DeltaSeconds);
        if (_inquiry != null)
        {
            _blink += args.DeltaSeconds;
            var cursor = _blink % 1f < 0.55f ? "_" : " ";
            _inquiry.Text = Loc.GetString("cmu-os-weyland-inquiry", ("id", _shortcuts?.TerminalId ?? 0)) + cursor;
        }

        RefreshWallpaper();
        UpdateTrace();
        // Shift clock: the round starts at 06:00 colony time.
        var time = TimeSpan.FromHours(6) + _timing.CurTime;
        _clock.Text = $"{time.Hours:00}:{time.Minutes:00}";
    }

    /// <summary>A wallpaper someone found on the ExtraNet takes over from the OS one.</summary>
    private void RefreshWallpaper()
    {
        var wanted = _shortcuts?.Wallpaper;
        if (wanted == _shownWallpaper)
            return;

        _shownWallpaper = wanted;
        _foundWallpaper = wanted is { } id
            && _cfg.GetCVar(CMUPersonalComputerCVars.Wallpapers)
            && _prototype.TryIndex(id, out var proto)
            && _theme.TryGetTexture(proto.Path, out var texture)
                ? texture
                : null;
    }

    /// <summary>A blinking warning while the Marshals trace this terminal, so whoever is on it can log off in time.</summary>
    private void UpdateTrace()
    {
        var now = _timing.CurTime;
        if (_shortcuts is not { } host || host.TraceEnds <= now)
        {
            _trace.Visible = false;
            return;
        }

        var total = (host.TraceEnds - host.TraceStarted).TotalSeconds;
        var done = total <= 0 ? 0 : (now - host.TraceStarted).TotalSeconds / total;
        _trace.Text = Loc.GetString("cmu-pc-trace-banner", ("percent", (int) (Math.Clamp(done, 0, 1) * 100)));
        _trace.Visible = now.TotalSeconds % 1 < 0.7;
        _trace.SetPositionLast();
    }

    /// <summary>Rebuilds the icons when the saved links change, checked twice a second.</summary>
    private void RefreshShortcuts(float frameTime)
    {
        if (_shortcuts == null)
            return;

        _shortcutCheck -= frameTime;
        if (_shortcutCheck > 0)
            return;

        _shortcutCheck = 0.5f;
        var shown = string.Empty;
        foreach (var shortcut in _shortcuts.Shortcuts)
            shown += $"{shortcut.Address}\t{shortcut.Title}\n";
        shown += GameIconsKey();

        if (shown == _shownShortcuts)
            return;

        _shownShortcuts = shown;
        BuildIcons();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_theme.Style is { } style)
        {
            style.DrawBackground(handle, this, _foundWallpaper ?? _wallpaper);
            return;
        }

        handle.DrawRect(PixelSizeBox, _theme.Background);
        if ((_foundWallpaper ?? _wallpaper) is { } wallpaper)
        {
            handle.DrawTextureRect(wallpaper, PixelSizeBox);
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
        var name = _theme.DisplayName;
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
        // Sevastolink's menu is always on screen, as are the menus of shells without a start menu.
        if (_theme.Shell == CMUComputerShell.Seegson || _theme.Style is { HasStartMenu: false })
            return;

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
                var files = new CMUComputerFilesApp(_theme, _shortcuts, AllDocuments(), _cfg.GetCVar(CMUPersonalComputerCVars.Notes));
                files.OnClicked += () => OnClicked?.Invoke();
                files.OnKeyTyped += () => OnKeyTyped?.Invoke();
                AddWindow(app, Loc.GetString("cmu-pc-app-files"), files, new Vector2(560, 380));
                break;
            case CMUComputerApp.Mail when _shortcuts != null:
                var mail = new CMUComputerMailApp(_theme, _shortcuts);
                mail.OnClicked += () => OnClicked?.Invoke();
                mail.OnKeyTyped += () => OnKeyTyped?.Invoke();
                AddWindow(app, Loc.GetString("cmu-pc-app-mail"), mail, new Vector2(620, 420));
                break;
            case CMUComputerApp.Deeps when _deep != null:
                var deeps = NewDeepsBrowser(_deep);
                deeps.Navigate(CMUDeepHome.Address);
                AddWindow(app, Loc.GetString("cmu-pc-app-deeps"), deeps, new Vector2(640, 440));
                break;
            default:
                AddWindow(app, Loc.GetString("cmu-pc-app-system"), BuildSystem(), new Vector2(560, 300));
                break;
        }
    }

    /// <summary>The ExtraNet browser. It stays off the deep network: only The deeps.exi goes there.</summary>
    private CMUNetBrowser NewBrowser()
    {
        var networks = new HashSet<ProtoId<CMUNetworkPrototype>>(_networks);
        networks.Remove(_deepNetwork);
        var browser = new CMUNetBrowser(_theme, networks, _home, _boards, _colony, _colonyName, null, _shortcuts);
        browser.OnKeyTyped += () => OnKeyTyped?.Invoke();
        browser.OnClicked += () => OnClicked?.Invoke();
        return browser;
    }

    /// <summary>The deeps.exi: the same browser in the deep skin, reaching everything this computer can.</summary>
    private CMUNetBrowser NewDeepsBrowser(ICMUDeepHost deep)
    {
        var skin = new CMUComputerTheme(_prototype.Index<CMUComputerOsPrototype>("CMUOsDeeps"), IoCManager.Resolve<IResourceCache>());
        var browser = new CMUNetBrowser(skin, _networks, CMUDeepHome.Address, _boards, _colony, _colonyName, deep, _shortcuts);
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

        var offset = (_theme.Style?.WindowOffset ?? (_theme.Shell == CMUComputerShell.Classic ? 110 : 14)) + _windows.Count * 26;
        _area.AddChild(window);
        LayoutContainer.SetPosition(window, new Vector2(offset, 14 + _windows.Count * 22));
        window.SetSize = Vector2.Min(size, Vector2.Max(_area.Size - new Vector2(offset + 6, 40), new Vector2(260, 160)));
        _windows.Add(window);

        if (content is CMUNetBrowser browser)
            browser.OnTitleChanged += pageTitle => SetBrowserTitle(window, pageTitle);

        var task = _theme.AppButton(app, _theme.Shell == CMUComputerShell.Classic ? title : title.ToUpperInvariant());
        task.Label.FontOverride = _theme.ShellFont;
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

    /// <summary>The programs open now, back to front, the way the server keeps them for the next viewer.</summary>
    public List<CMUComputerScreenWindow> Snapshot()
    {
        var screen = new List<CMUComputerScreenWindow>();
        foreach (var window in _windows)
        {
            if (window.App is not (CMUComputerApp.Browser or CMUComputerApp.Deeps or CMUComputerApp.Files
                or CMUComputerApp.Mail or CMUComputerApp.System))
                continue;

            screen.Add(new CMUComputerScreenWindow
            {
                App = window.App.ToString(),
                Address = (window.Content as CMUNetBrowser)?.Address,
                Position = window.Position,
                Size = window.Size,
                Minimized = !window.Visible,
            });
        }

        return screen;
    }

    /// <summary>
    /// Opens again what was left on the terminal's screen, silently and without logging the pages as new visits.
    /// Only once, on a fresh desktop: from then on this desktop is the one saving.
    /// </summary>
    public void RestoreScreen(IReadOnlyList<CMUComputerScreenWindow> screen)
    {
        if (_restored)
            return;

        _restored = true;
        var clicked = OnClicked;
        OnClicked = null;
        foreach (var saved in screen)
        {
            if (!Enum.TryParse<CMUComputerApp>(saved.App, out var app))
                continue;

            var before = _windows.Count;
            switch (app)
            {
                case CMUComputerApp.Browser:
                    var browser = NewBrowser();
                    browser.Reopen(saved.Address ?? _home);
                    AddWindow(app, Loc.GetString("cmu-pc-app-browser"), browser, saved.Size);
                    break;
                case CMUComputerApp.Deeps when _deep != null:
                    var deeps = NewDeepsBrowser(_deep);
                    deeps.Reopen(saved.Address ?? CMUDeepHome.Address);
                    AddWindow(app, Loc.GetString("cmu-pc-app-deeps"), deeps, saved.Size);
                    break;
                case CMUComputerApp.Files:
                case CMUComputerApp.Mail when _shortcuts != null:
                case CMUComputerApp.System:
                    Open(app);
                    break;
            }

            if (_windows.Count == before)
                continue;

            var window = _windows[^1];
            LayoutContainer.SetPosition(window, saved.Position);
            if (saved.Size.X > 0 && saved.Size.Y > 0)
                window.SetSize = saved.Size;

            if (saved.Minimized)
                Minimize(window);
        }

        OnClicked = clicked;
        _savedScreen = ScreenKey(Snapshot());
    }

    /// <summary>Tells the terminal what is on screen whenever it changes, checked twice a second.</summary>
    private void SaveScreen(float frameTime)
    {
        if (_shortcuts == null)
            return;

        _screenCheck -= frameTime;
        if (_screenCheck > 0)
            return;

        _screenCheck = 0.5f;
        var screen = Snapshot();
        var key = ScreenKey(screen);
        if (key == _savedScreen)
            return;

        _savedScreen = key;
        _restored = true;
        _shortcuts.SaveScreen(screen);
    }

    private static string ScreenKey(List<CMUComputerScreenWindow> screen)
    {
        var key = new System.Text.StringBuilder();
        foreach (var window in screen)
        {
            key.Append(window.App).Append('|').Append(window.Address).Append('|')
                .Append((int) window.Position.X).Append(',').Append((int) window.Position.Y).Append('|')
                .Append((int) window.Size.X).Append(',').Append((int) window.Size.Y).Append('|')
                .Append(window.Minimized).Append('\n');
        }

        return key.ToString();
    }

    private List<CMUComputerDocument> AllDocuments()
    {
        var all = new List<CMUComputerDocument>(_theme.Os.Documents);
        all.AddRange(_documents);
        return all;
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
        box.AddChild(_theme.Label(_theme.DisplayName, _theme.Bold));
        if (_theme.Os.BootLines.Count > 0)
            box.AddChild(_theme.Label(Loc.GetString(_theme.Os.BootLines[0]), _theme.Font, _theme.Dim));

        box.AddChild(new CMUTerminalRule { Color = _theme.Accent, Margin = new Thickness(0, 4) });
        box.AddChild(_theme.Label(Loc.GetString("cmu-pc-system-terminal", ("name", _terminalName))));
        if (_shortcuts != null)
        {
            box.AddChild(_theme.Label(Loc.GetString("cmu-pc-system-terminal-id", ("id", _shortcuts.TerminalId))));
            if (_shortcuts.SessionUser is { } user)
                box.AddChild(_theme.Label(Loc.GetString("cmu-pc-system-user", ("name", user), ("job", _shortcuts.SessionJob ?? "-"))));
        }

        var names = new List<string>();
        foreach (var network in _networks)
        {
            if (_prototype.TryIndex(network, out var proto))
                names.Add(Loc.GetString(proto.Name));
        }

        names.Sort(string.CompareOrdinal);
        box.AddChild(_theme.Label(Loc.GetString("cmu-pc-system-networks")));
        if (_shortcuts?.Dark == true)
            box.AddChild(_theme.Label(Loc.GetString("cmu-pc-system-dark"), _theme.Font, _theme.Accent));

        foreach (var name in names)
            box.AddChild(_theme.Label($"  {name}", _theme.Font, _theme.Dim));

        var uptime = _theme.Label(string.Empty);
        box.AddChild(uptime);
        box.AddChild(new CMUSystemUptime(uptime, _bootedAt, _timing));
        return box;
    }
}

/// <summary>A double hairline, the Company's rule between the desktop and its bottom bar.</summary>
public sealed class CMUWeylandRule : Control
{
    private readonly Color _color;

    public CMUWeylandRule(Color color)
    {
        _color = color;
        MinHeight = 4;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var line = MathF.Max(1, MathF.Round(UIScale));
        handle.DrawRect(new UIBox2(0, 0, PixelWidth, line), _color);
        handle.DrawRect(new UIBox2(0, PixelHeight - line, PixelWidth, PixelHeight), _color.WithAlpha(0.45f));
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
