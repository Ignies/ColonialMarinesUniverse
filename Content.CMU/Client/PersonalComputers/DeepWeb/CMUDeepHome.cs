using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// What The deeps.exi needs from its computer: what has been found and unlocked, when the next scan is ready,
/// and the server side of ad pages, camera links and hijacks.
/// </summary>
public interface ICMUDeepHost
{
    IReadOnlyList<string> Discovered { get; }

    IReadOnlySet<ProtoId<CMUNetSitePrototype>> Unlocked { get; }

    /// <summary>This round's random address of every deep site.</summary>
    IReadOnlyDictionary<ProtoId<CMUNetSitePrototype>, string> Aliases { get; }

    TimeSpan NextScan { get; }

    /// <summary>Whether the person at the computer is an insurgent, who may seize the news feeds.</summary>
    bool IsInsurgent { get; }

    event Action<CMUDeepPageMessage>? PageReceived;

    void Scan();

    void RequestPage(string host);

    void OpenCamera(string network);

    void Hijack(string title, string body);
}

/// <summary>
/// The deeps.exi home page: a scan button that turns up deep sites and ad pages, the list of what this
/// computer has found, and for insurgents a form to seize the colony's news feeds. Rebuilds the list when a
/// scan finds something, and counts down to the next scan.
/// </summary>
public sealed partial class CMUDeepHome : BoxContainer
{
    public const string Address = "deeps://home";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly ICMUDeepHost _host;
    private readonly CMUComputerTheme _theme;
    private readonly Button _scan;
    private readonly Label _status;
    private readonly BoxContainer _found;
    private int _shown = -1;

    public event Action? OnClicked;
    public event Action? OnKeyTyped;

    public CMUDeepHome(CMUComputerTheme theme, ICMUDeepHost host)
    {
        IoCManager.InjectDependencies(this);
        _theme = theme;
        _host = host;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 6;

        _scan = theme.Button(Loc.GetString("cmu-deep-home-scan"), Loc.GetString("cmu-deep-home-scan-tooltip"));
        _scan.HorizontalAlignment = HAlignment.Left;
        _scan.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            _host.Scan();
        };
        _status = new Label();
        _found = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 3 };

        AddChild(new Label { Text = Loc.GetString("cmu-deep-home-title"), StyleClasses = { "LabelHeadingBigger" } });
        AddChild(new Label { Text = Loc.GetString("cmu-deep-home-intro") });
        AddChild(new Label { Text = Loc.GetString("cmu-deep-home-doors", ("mark", "::")), FontColorOverride = theme.Dim });
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(0, 6),
            Children = { _scan, _status },
        });
        AddChild(new Label { Text = Loc.GetString("cmu-deep-home-found"), StyleClasses = { "LabelHeading" } });
        AddChild(_found);

        if (host.IsInsurgent)
            AddChild(BuildHijack());
    }

    /// <summary>The broadcast form: a headline and a message that cover the news front pages for a few minutes.</summary>
    private Control BuildHijack()
    {
        var title = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-deep-hijack-title-placeholder") };
        title.IsValid = text => text.Length <= CMUNetBoardLimits.TitleLength;
        title.OnTextChanged += _ => OnKeyTyped?.Invoke();
        var body = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-deep-hijack-body-placeholder") };
        body.IsValid = text => text.Length <= CMUNetBoardLimits.BodyLength;
        body.OnTextChanged += _ => OnKeyTyped?.Invoke();

        var send = _theme.Button(Loc.GetString("cmu-deep-hijack-send"));
        send.HorizontalAlignment = HAlignment.Left;
        send.OnPressed += _ =>
        {
            if (title.Text.Trim().Length == 0 || body.Text.Trim().Length == 0)
                return;

            OnClicked?.Invoke();
            _host.Hijack(title.Text, body.Text);
            title.Text = string.Empty;
            body.Text = string.Empty;
        };

        return new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(0, 14, 0, 0),
            Children =
            {
                new CMUTerminalRule { Color = _theme.Phosphor.WithAlpha(0.6f) },
                new Label { Text = Loc.GetString("cmu-deep-hijack-heading"), StyleClasses = { "LabelHeading" } },
                new Label { Text = Loc.GetString("cmu-deep-hijack-intro") },
                title,
                body,
                send,
            },
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var wait = _host.NextScan - _timing.CurTime;
        _scan.Disabled = wait > TimeSpan.Zero;
        _status.Text = wait > TimeSpan.Zero
            ? Loc.GetString("cmu-deep-home-cooling", ("seconds", (int) Math.Ceiling(wait.TotalSeconds)))
            : Loc.GetString("cmu-deep-home-ready");

        if (_host.Discovered.Count == _shown)
            return;

        _shown = _host.Discovered.Count;
        _found.RemoveAllChildren();
        if (_shown == 0)
            _found.AddChild(new Label { Text = Loc.GetString("cmu-deep-home-nothing") });

        for (var i = _host.Discovered.Count - 1; i >= 0; i--)
        {
            var address = _host.Discovered[i];
            var title = Loc.GetString("cmu-deep-home-ad");
            foreach (var (siteId, alias) in _host.Aliases)
            {
                if (alias == address && _prototype.TryIndex(siteId, out var site))
                    title = Loc.GetString(site.Title);
            }

            var link = new CMUNetLinkLabel(address, address);
            link.SetColors(_theme.Phosphor, _theme.Background);
            // Round addresses are long, so the title sits above its address.
            _found.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(0, 0, 0, 4),
                Children =
                {
                    new Label { Text = title, ClipText = true, HorizontalExpand = true },
                    new BoxContainer { Children = { link } },
                },
            });
        }
    }
}
