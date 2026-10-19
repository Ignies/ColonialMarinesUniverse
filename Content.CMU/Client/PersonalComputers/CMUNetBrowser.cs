using Content.Client.Guidebook.RichText;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The ExtraNet browser: navigation bar, page and status line, in a desktop window.
/// </summary>
public sealed partial class CMUNetBrowser : BoxContainer, ILinkClickHandler
{
    public const string DirectoryAddress = "sys://directory";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IResourceManager _resources = default!;

    private readonly CMUExtraNetSystem _net;
    private readonly CMUComputerTheme _theme;
    private readonly ICMUNetBoardHost? _boards;
    private readonly HashSet<ProtoId<CMUNetworkPrototype>> _networks;
    private readonly string _home;

    private readonly LineEdit _address;
    private readonly Button _back;
    private readonly Button _forward;
    private readonly CMUNetPageView _view;
    private readonly Label _status;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    /// <summary>Site title of the page on screen, for the window title.</summary>
    public string PageTitle { get; private set; } = string.Empty;

    public event Action? OnKeyTyped;
    public event Action? OnClicked;
    public event Action<string>? OnTitleChanged;

    public CMUNetBrowser(
        CMUComputerTheme theme,
        HashSet<ProtoId<CMUNetworkPrototype>> networks,
        string home,
        ICMUNetBoardHost? boards)
    {
        IoCManager.InjectDependencies(this);
        _net = _entities.System<CMUExtraNetSystem>();
        _theme = theme;
        _boards = boards;
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
        _status = theme.Label(string.Empty, theme.Small, theme.Dim);

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 4, 4, 4),
            SeparationOverride = 4,
            Children = { _back, _forward, homeButton, directory, _address, go },
        });
        AddChild(new CMUTerminalRule { Color = theme.Phosphor.WithAlpha(0.6f) });
        AddChild(_view);
        AddChild(new CMUTerminalRule { Color = theme.Phosphor.WithAlpha(0.6f) });
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 1, 6, 2),
            Children = { _status },
        });
    }

    public void HandleClick(string link)
    {
        Click(link);
    }

    private void Click(string address)
    {
        OnClicked?.Invoke();
        Navigate(address);
    }

    public void Navigate(string address)
    {
        address = address.Trim();
        if (address.Length == 0)
            return;

        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(address);
        _historyIndex = _history.Count - 1;
        Show(address);
    }

    private void GoHistory(int offset)
    {
        var index = _historyIndex + offset;
        if (index < 0 || index >= _history.Count)
            return;

        OnClicked?.Invoke();
        _historyIndex = index;
        Show(_history[index]);
    }

    private void Show(string address)
    {
        _back.Disabled = _historyIndex <= 0;
        _forward.Disabled = _historyIndex >= _history.Count - 1;

        if (address.Equals(DirectoryAddress, StringComparison.OrdinalIgnoreCase))
        {
            _address.Text = DirectoryAddress;
            ShowDirectory();
            SetStatus(Loc.GetString("cmu-pc-browser-directory-title"), "cmu-pc-browser-status-done");
            return;
        }

        var page = _net.Resolve(address, _networks);
        _address.Text = page.Address;
        var siteTitle = page.Site == null ? string.Empty : Loc.GetString(page.Site.Title);

        switch (page.Result)
        {
            case CMUNetResolveResult.Found:
                using (var reader = _resources.ContentFileReadText(page.File!.Value))
                {
                    _view.ShowDocument(reader.ReadToEnd(), page.Site!.Style);
                }

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

    private void SetStatus(string title, string status)
    {
        PageTitle = title;
        _status.Text = Loc.GetString(status);
        OnTitleChanged?.Invoke(title);
    }

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
                        new Label { Text = Loc.GetString(site.Title) },
                    },
                });
            }
        });
    }
}
