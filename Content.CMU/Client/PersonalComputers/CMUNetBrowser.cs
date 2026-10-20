using System.Linq;
using Content.Client.CMU14.PersonalComputers.DeepWeb;
using Content.Client.Guidebook.RichText;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The ExtraNet browser: navigation bar, page and status line, in a desktop window. With a deep host it is The
/// deeps.exi: it also loads ad pages from the server, follows <c>cam://</c> links and opens hidden sites once
/// they are unlocked.
/// </summary>
public sealed partial class CMUNetBrowser : BoxContainer, ILinkClickHandler
{
    public const string DirectoryAddress = "sys://directory";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly CMUExtraNetSystem _net;
    private readonly CMUComputerTheme _theme;
    private readonly ICMUNetBoardHost? _boards;
    private readonly string? _colony;
    private readonly string? _colonyName;
    private readonly ICMUDeepHost? _deep;
    private readonly ICMUComputerHost? _shortcuts;
    private bool _shownCarrier = true;
    private readonly HashSet<ProtoId<CMUNetworkPrototype>> _networks;
    private readonly string _home;

    private readonly LineEdit _address;
    private readonly Button _back;
    private readonly Button _forward;
    private readonly Button _save;
    private readonly CMUNetPageView _view;
    private readonly BoxContainer _tabs;
    private readonly Label _status;

    private readonly List<string> _history = new();
    private readonly Random _random = new();
    private int _historyIndex = -1;

    /// <summary>The page being put back on screen is not a new visit, so it is not reported.</summary>
    private bool _quiet;

    /// <summary>The ad host waiting on the server, if the page on screen is one.</summary>
    private string? _pendingAd;

    /// <summary>The address on screen when it is a page that can be saved to the desktop.</summary>
    private string? _saveable;

    /// <summary>Site title of the page on screen, for the window title.</summary>
    public string PageTitle { get; private set; } = string.Empty;

    public event Action? OnKeyTyped;
    public event Action? OnClicked;
    public event Action<string>? OnTitleChanged;

    public CMUNetBrowser(
        CMUComputerTheme theme,
        HashSet<ProtoId<CMUNetworkPrototype>> networks,
        string home,
        ICMUNetBoardHost? boards,
        string? colony,
        string? colonyName,
        ICMUDeepHost? deep = null,
        ICMUComputerHost? shortcuts = null)
    {
        IoCManager.InjectDependencies(this);
        _net = _entities.System<CMUExtraNetSystem>();
        _theme = theme;
        _boards = boards;
        _colony = colony;
        _colonyName = colonyName;
        _deep = deep;
        _shortcuts = shortcuts;
        _networks = networks;
        _home = home;

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        _back = theme.Button("<", Loc.GetString("cmu-pc-browser-back"));
        _forward = theme.Button(">", Loc.GetString("cmu-pc-browser-forward"));
        var homeButton = theme.Button(Loc.GetString("cmu-pc-browser-home"));
        var directory = theme.Button(Loc.GetString("cmu-pc-browser-directory"));
        var go = theme.Button(Loc.GetString("cmu-pc-browser-go"));
        _save = theme.Button(Loc.GetString("cmu-pc-browser-save"), Loc.GetString("cmu-pc-browser-save-tooltip"));
        _save.Disabled = true;
        _save.Visible = shortcuts != null && _cfg.GetCVar(CMUPersonalComputerCVars.Shortcuts);
        _save.OnPressed += _ => Save();

        _address = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("cmu-pc-browser-address-placeholder"),
            ModulateSelfOverride = theme.Phosphor,
        };
        _address.OnTextEntered += args => Navigate(args.Text);
        _address.OnTextChanged += _ => OnKeyTyped?.Invoke();

        _back.OnPressed += _ => GoHistory(-1);
        _forward.OnPressed += _ => GoHistory(1);
        homeButton.OnPressed += _ => Click(_home);
        directory.OnPressed += _ => Click(DirectoryAddress);
        go.OnPressed += _ => Click(_address.Text);

        _view = new CMUNetPageView(theme);
        _view.OnPopupShown += () => _shortcuts?.PopupShown();

        _tabs = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            Margin = new Thickness(4, 3, 4, 0),
            Visible = false,
        };
        _status = theme.Label(string.Empty, theme.Small, theme.Dim);

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 4, 4, 4),
            SeparationOverride = 4,
            Children = { _back, _forward, homeButton, directory, _address, go, _save },
        });
        AddChild(new CMUTerminalRule { Color = theme.Phosphor.WithAlpha(0.6f) });
        AddChild(_tabs);
        AddChild(_view);
        AddChild(new CMUTerminalRule { Color = theme.Phosphor.WithAlpha(0.6f) });
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 1, 6, 2),
            Children = { _status },
        });
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_deep != null)
            _deep.PageReceived += OnPageReceived;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_deep != null)
            _deep.PageReceived -= OnPageReceived;
    }

    public void HandleClick(string link)
    {
        Click(link);
    }

    private void Save()
    {
        if (_saveable == null || _shortcuts == null)
            return;

        OnClicked?.Invoke();
        _shortcuts.SaveShortcut(_saveable, PageTitle.Length == 0 ? _saveable : PageTitle);
        _status.Text = Loc.GetString("cmu-pc-browser-status-saved");
    }

    /// <summary>Links and tabs on pages may use a deep site's fixed name; the address bar may not.</summary>
    private void Click(string address)
    {
        OnClicked?.Invoke();
        Navigate(address, true);
    }

    public void Navigate(string address)
    {
        _quiet = false;
        Navigate(address, false);
    }

    /// <summary>
    /// Puts a page back on screen the way it was left on the terminal, without logging it again as a new visit.
    /// </summary>
    public void Reopen(string address)
    {
        _quiet = true;
        Navigate(address, false);
    }

    /// <summary>The page on screen, or null before the first.</summary>
    public string? Address => _historyIndex >= 0 ? _history[_historyIndex] : null;

    private void Navigate(string address, bool fromPage)
    {
        address = address.Trim();
        if (address.Length == 0)
            return;

        // The webring link: somewhere listed and reachable, picked fresh each time.
        if (address.Equals(CMUExtraNetSystem.RandomAddress, StringComparison.OrdinalIgnoreCase))
        {
            var sites = _net.Sites.Where(s => !s.Unlisted && CMUExtraNetSystem.CanReach(s, _networks)).ToList();
            if (sites.Count == 0)
                return;

            address = sites[_random.Next(sites.Count)].Address;
        }

        // Camera links open a feed on the computer instead of a page.
        if (address.StartsWith(CMUDeepAddress.CameraScheme, StringComparison.OrdinalIgnoreCase))
        {
            _deep?.OpenCamera(address[CMUDeepAddress.CameraScheme.Length..].Trim('/'));
            return;
        }

        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(address);
        _historyIndex = _history.Count - 1;
        Show(address, fromPage);
    }

    private void GoHistory(int offset)
    {
        var index = _historyIndex + offset;
        if (index < 0 || index >= _history.Count)
            return;

        OnClicked?.Invoke();
        _historyIndex = index;
        Show(_history[index], false);
    }

    private void Show(string address, bool fromPage)
    {
        _back.Disabled = _historyIndex <= 0;
        _forward.Disabled = _historyIndex >= _history.Count - 1;
        _pendingAd = null;
        SetSaveable(null);

        // No sensor tower, no network: every address is out of reach until it comes back.
        _shownCarrier = _shortcuts?.Carrier ?? true;
        if (!_shownCarrier)
        {
            _tabs.Visible = false;
            _address.Text = address;
            ShowSystemPage("cmu-pc-browser-no-carrier-title", "cmu-pc-browser-no-carrier-body", address);
            SetStatus(string.Empty, "cmu-pc-browser-status-error");
            return;
        }

        if (_deep != null && address.Equals(CMUDeepHome.Address, StringComparison.OrdinalIgnoreCase))
        {
            _tabs.Visible = false;
            _address.Text = CMUDeepHome.Address;
            _view.ShowControls((page, _) =>
            {
                var home = new CMUDeepHome(_theme, _deep);
                home.OnClicked += () => OnClicked?.Invoke();
                home.OnKeyTyped += () => OnKeyTyped?.Invoke();
                page.AddChild(home);
            });
            SetStatus(Loc.GetString("cmu-deep-home-title"), "cmu-pc-browser-status-done");
            SetSaveable(CMUDeepHome.Address);
            return;
        }

        var host = CMUExtraNetSystem.Split(address).Host;
        if (_deep != null && CMUDeepAddress.IsAd(host))
        {
            _tabs.Visible = false;
            _address.Text = host;
            _pendingAd = host;
            ShowSystemPage("cmu-deep-ad-loading-title", "cmu-deep-ad-loading-body", host);
            SetStatus(Loc.GetString("cmu-deep-ad-title"), "cmu-pc-browser-status-loading");
            _deep.RequestPage(host);
            return;
        }

        if (address.Equals(DirectoryAddress, StringComparison.OrdinalIgnoreCase))
        {
            _tabs.Visible = false;
            _address.Text = DirectoryAddress;
            ShowDirectory();
            SetStatus(Loc.GetString("cmu-pc-browser-directory-title"), "cmu-pc-browser-status-done");
            SetSaveable(DirectoryAddress);
            return;
        }

        var page = Resolve(address, fromPage);

        // History keeps the address as shown, so going back to a deep page needs no fixed name.
        if (_historyIndex >= 0 && page.Result != CMUNetResolveResult.UnknownHost)
            _history[_historyIndex] = page.Address;

        // A hidden site does not exist until this computer has followed the ads to it.
        if (page.Site is { Hidden: true } hidden && (_deep == null || !_deep.Unlocked.Contains(hidden.ID)))
            page = new CMUNetPage(CMUNetResolveResult.UnknownHost, page.Address, null, null);

        _address.Text = page.Address;
        ShowTabs(page);
        var siteTitle = page.Site == null ? string.Empty : CMUExtraNetSystem.Text(page.Site.Title);

        switch (page.Result)
        {
            case CMUNetResolveResult.Found:
                var file = PageFile(page.Site!, page.File!.Value);
                if (!_resources.ContentFileExists(file))
                {
                    ShowSystemPage("cmu-pc-browser-missing-page-title", "cmu-pc-browser-missing-page-body", page.Address);
                    SetStatus(siteTitle, "cmu-pc-browser-status-error");
                    break;
                }

                using (var reader = _resources.ContentFileReadText(file))
                {
                    _view.ShowDocument(CMUExtraNetSystem.LocalPaths(FillColony(reader.ReadToEnd()), page.Site!), page.Site!.Style);
                }

                SetSaveable(page.Address);

                // A board's postings run under its index page.
                if (page.Site.Board != null && CMUExtraNetSystem.Split(page.Address).Path.Length == 0)
                {
                    var site = page.Site;
                    _view.Append(style =>
                    {
                        var board = new CMUNetBoardView(_theme, style, site, _boards);
                        board.OnClicked += () => OnClicked?.Invoke();
                        board.OnKeyTyped += () => OnKeyTyped?.Invoke();
                        return board;
                    });
                }

                SetStatus(siteTitle, "cmu-pc-browser-status-done");
                break;
            // A dark terminal is not refused: nothing on the colony network hears it at all.
            case CMUNetResolveResult.AccessDenied when _shortcuts?.Dark == true:
                ShowSystemPage("cmu-pc-browser-dark-title", "cmu-pc-browser-dark-body", page.Address);
                SetStatus(siteTitle, "cmu-pc-browser-status-error");
                break;
            case CMUNetResolveResult.AccessDenied:
                ShowSystemPage("cmu-pc-browser-denied-title", "cmu-pc-browser-denied-body", page.Address);
                SetStatus(siteTitle, "cmu-pc-browser-status-denied");
                break;
            case CMUNetResolveResult.UnknownPage:
                ShowSystemPage("cmu-pc-browser-missing-page-title", "cmu-pc-browser-missing-page-body", page.Address);
                SetStatus(siteTitle, "cmu-pc-browser-status-error");
                break;
            default:
                ShowSystemPage("cmu-pc-browser-unknown-host-title", "cmu-pc-browser-unknown-host-body", page.Address);
                SetStatus(string.Empty, "cmu-pc-browser-status-error");
                break;
        }
    }

    /// <summary>
    /// Deep sites only answer at this round's random address, which is what the address bar shows. Their fixed
    /// names still work in links on pages, so pages can link to each other, but typed in they go nowhere.
    /// </summary>
    private CMUNetPage Resolve(string address, bool fromPage)
    {
        var (host, path) = CMUExtraNetSystem.Split(address);
        if (_deep != null)
        {
            foreach (var (siteId, alias) in _deep.Aliases)
            {
                if (alias != host || !_prototype.TryIndex(siteId, out var aliased))
                    continue;

                var found = _net.Resolve(path.Length == 0 ? aliased.Address : $"{aliased.Address}/{path}", _networks);
                return found with { Address = path.Length == 0 ? alias : $"{alias}/{path}" };
            }
        }

        var page = _net.Resolve(address, _networks);
        if (page.Site == null || !CMUExtraNetSystem.IsDeep(page.Site))
            return page;

        if (!fromPage || _deep == null || !_deep.Aliases.TryGetValue(page.Site.ID, out var known))
            return new CMUNetPage(CMUNetResolveResult.UnknownHost, page.Address, null, null);

        var pagePath = CMUExtraNetSystem.Split(page.Address).Path;
        return page with { Address = pagePath.Length == 0 ? known : $"{known}/{pagePath}" };
    }

    private void OnPageReceived(CMUDeepPageMessage message)
    {
        if (message.Host != _pendingAd)
            return;

        _pendingAd = null;
        _view.ShowDocument(message.Markup, null);
        SetStatus(Loc.GetString("cmu-deep-ad-title"), "cmu-pc-browser-status-done");
        SetSaveable(message.Host);
    }

    private void SetSaveable(string? address)
    {
        _saveable = address;
        _save.Disabled = address == null;
    }

    /// <summary>
    /// The version of a page to show: the GOVFOR platoon's own, <c>&lt;root&gt;/&lt;platoon&gt;/&lt;file&gt;</c>, on
    /// sites with platoon pages; then this colony's (or map's), <c>&lt;root&gt;/&lt;colony&gt;/&lt;file&gt;</c>; then
    /// the generic page.
    /// </summary>
    private ResPath PageFile(CMUNetSitePrototype site, ResPath file)
    {
        if (site.PlatoonPages && Variant(site, _shortcuts?.Platoon, file) is { } platoon)
            return platoon;

        return Variant(site, _colony, file) ?? file;
    }

    /// <summary><c>&lt;root&gt;/&lt;folder&gt;/&lt;file&gt;</c> when it exists.</summary>
    private ResPath? Variant(CMUNetSitePrototype site, string? folder, ResPath file)
    {
        if (string.IsNullOrEmpty(folder))
            return null;

        var variant = site.Root / folder / file.RelativeTo(site.Root);
        return _resources.ContentFileExists(variant) ? variant : null;
    }

    private string FillColony(string text)
    {
        var name = string.IsNullOrEmpty(_colonyName) ? Loc.GetString("cmu-net-colony-unknown") : _colonyName;
        return text.Replace("{colony}", name, StringComparison.Ordinal);
    }


    /// <summary>The site's tab strip in its own colours, the open page's tab filled.</summary>
    private void ShowTabs(CMUNetPage page)
    {
        _tabs.RemoveAllChildren();
        _tabs.Visible = page.Site is { Tabs.Count: > 0 } && page.Result != CMUNetResolveResult.AccessDenied;
        if (!_tabs.Visible)
            return;

        var site = page.Site!;
        var style = (site.Style ?? new CMUNetPageStyle()).Over(_theme.DefaultPageStyle);
        var link = style.Link ?? _theme.Phosphor;
        var background = style.Background ?? _theme.Background;
        var open = CMUExtraNetSystem.Split(page.Address).Path;

        foreach (var tab in site.Tabs)
        {
            var path = tab.Path.Trim('/').ToLowerInvariant();
            var active = path == open;
            var button = _theme.Button(CMUExtraNetSystem.Text(tab.Title));
            button.StyleBoxOverride = _theme.ButtonBox(active ? link : background, link);
            button.Label.FontColorOverride = active ? background : link;
            button.OnPressed += _ => Click(path.Length == 0 ? site.Address : $"{site.Address}/{path}");
            _tabs.AddChild(button);
        }
    }

    private void SetStatus(string title, string status)
    {
        PageTitle = title;
        _status.Text = Loc.GetString(status);
        OnTitleChanged?.Invoke(title);

        // Every finished load goes to the terminal's log; failed lookups of live deep addresses alert the Marshals.
        if (status == "cmu-pc-browser-status-loading")
            return;

        if (!_quiet)
            _shortcuts?.Visited(_address.Text, status == "cmu-pc-browser-status-done");

        _quiet = false;
    }

    /// <summary>The computer this browser runs on, for page tags that talk to the server.</summary>
    public ICMUComputerHost? Host => _shortcuts;

    /// <summary>Reloads the page when the carrier drops or comes back.</summary>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_shortcuts == null || _shortcuts.Carrier == _shownCarrier || _historyIndex < 0)
            return;

        Show(_history[_historyIndex], false);
    }

    /// <summary>The deep web side of the computer, when this is The deeps.exi.</summary>
    public ICMUDeepHost? Deep => _deep;

    private void ShowSystemPage(string title, string body, string address)
    {
        _view.ShowControls((page, style) =>
        {
            page.AddChild(new Label { Text = Loc.GetString(title), StyleClasses = { "LabelHeading" } });
            page.AddChild(new RichTextLabel
            {
                Text = Loc.GetString(body, ("address", address)),
                Margin = new Thickness(0, 8, 0, 0),
                ModulateSelfOverride = style.Text,
            });
            page.AddChild(new CMUNetLinkLabel(Loc.GetString("cmu-pc-browser-return-home"), _home)
            {
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0),
            });
        });
    }

    private void ShowDirectory()
    {
        var sites = new List<CMUNetSitePrototype>();
        foreach (var site in _net.Sites)
        {
            if (!site.Unlisted && CMUExtraNetSystem.CanReach(site, _networks))
                sites.Add(site);
        }

        sites.Sort((a, b) => string.CompareOrdinal(a.Address, b.Address));
        _view.ShowControls((page, _) =>
        {
            page.AddChild(new Label
            {
                Text = Loc.GetString("cmu-pc-browser-directory-title"),
                StyleClasses = { "LabelHeading" },
                Margin = new Thickness(0, 0, 0, 8),
            });

            foreach (var site in sites)
            {
                page.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Margin = new Thickness(0, 2),
                    Children =
                    {
                        new BoxContainer
                        {
                            MinWidth = 220,
                            Children = { new CMUNetLinkLabel(site.Address, site.Address) },
                        },
                        new Label { Text = CMUExtraNetSystem.Text(site.Title) },
                    },
                });
            }
        });
    }
}
