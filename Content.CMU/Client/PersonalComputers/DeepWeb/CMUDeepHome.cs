using System.Linq;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
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

    event Action<CMUMarketStateMessage>? MarketReceived;

    event Action<CMUDeepToolResultMessage>? ToolResult;

    void RequestMarket();

    void ListItem(string title, string description, int price, string drop);

    void Buy(int listing);

    void Sniff(int terminal);

    void Loop(string network);

    /// <summary>The insurgents' live relay bugs, answering <see cref="RequestRelays"/>.</summary>
    event Action<CMURelayListMessage>? RelayReceived;

    /// <summary>Asks the server for the relays this hacked terminal could route its deep traffic through.</summary>
    void RequestRelays();

    /// <summary>Routes this terminal's deep traffic through the given relay, or stops routing when null.</summary>
    void Route(NetEntity? terminal);

    /// <summary>Plants an incriminating file on the given relay, framing whoever uses it.</summary>
    void Plant(NetEntity terminal);
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
    private BoxContainer? _toolOutput;
    private BoxContainer? _relayList;

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

        if (host.IsInsurgent && IoCManager.Resolve<IConfigurationManager>().GetCVar(CMUPersonalComputerCVars.Relay))
            AddChild(BuildRelays());

        if (IoCManager.Resolve<IConfigurationManager>().GetCVar(CMUPersonalComputerCVars.HackerTools))
            AddChild(BuildTools());
    }

    /// <summary>The insurgents' relay bugs, each with a button to route this terminal's deep traffic through it.</summary>
    private Control BuildRelays()
    {
        _relayList = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };

        var refresh = _theme.Button(Loc.GetString("cmu-relay-refresh"));
        refresh.HorizontalAlignment = HAlignment.Left;
        refresh.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            _host.RequestRelays();
        };

        return new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(0, 14, 0, 0),
            Children =
            {
                new CMUTerminalRule { Color = _theme.Phosphor.WithAlpha(0.6f) },
                new Label { Text = Loc.GetString("cmu-relay-heading"), StyleClasses = { "LabelHeading" } },
                new Label { Text = Loc.GetString("cmu-relay-intro") },
                refresh,
                _relayList,
            },
        };
    }

    private void OnRelayList(CMURelayListMessage message)
    {
        if (_relayList == null)
            return;

        _relayList.RemoveAllChildren();
        if (message.Relays.Count == 0)
        {
            _relayList.AddChild(new Label { Text = Loc.GetString("cmu-relay-none"), FontColorOverride = _theme.Dim });
            return;
        }

        foreach (var relay in message.Relays)
        {
            var who = relay.User ?? Loc.GetString("cmu-relay-nobody");
            var status = Loc.GetString(relay.Online ? "cmu-relay-online" : "cmu-relay-offline");
            var title = Loc.GetString("cmu-relay-row",
                ("id", relay.TerminalId), ("location", relay.Location), ("who", who), ("status", status));

            var info = relay;
            var route = _theme.Button(Loc.GetString(relay.Routed ? "cmu-relay-routing" : "cmu-relay-route"));
            // A relay that has dropped offline cannot be newly routed to, but an active route can still be dropped.
            route.Disabled = !relay.Online && !relay.Routed;
            route.OnPressed += _ =>
            {
                OnClicked?.Invoke();
                _host.Route(info.Routed ? null : info.Terminal);
                _host.RequestRelays();
            };

            var plant = _theme.Button(Loc.GetString("cmu-relay-plant"), Loc.GetString("cmu-relay-plant-tooltip"));
            plant.Disabled = !relay.Online;
            plant.OnPressed += _ =>
            {
                OnClicked?.Invoke();
                _host.Plant(info.Terminal);
                _host.RequestRelays();
            };

            _relayList.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Children =
                {
                    new Label { Text = title, HorizontalExpand = true, ClipText = true },
                    route,
                    plant,
                },
            });
        }
    }

    /// <summary>The packet sniffer and the camera loop, with their last result under them.</summary>
    private Control BuildTools()
    {
        var output = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        _toolOutput = output;

        var terminal = new LineEdit { MinWidth = 80, PlaceHolder = Loc.GetString("cmu-deep-sniff-placeholder") };
        terminal.IsValid = text => text.Length <= 4 && text.All(char.IsDigit);
        var sniff = _theme.Button(Loc.GetString("cmu-deep-sniff"));
        sniff.OnPressed += _ =>
        {
            if (!int.TryParse(terminal.Text, out var id))
                return;

            OnClicked?.Invoke();
            _host.Sniff(id);
        };

        var loops = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
        foreach (var (network, label) in new[]
                 {
                     ("CMUSurveillanceCameraColonyGOVFOR", "cmu-deep-loop-govfor"),
                     ("CMUSurveillanceCameraColonyWEYU", "cmu-deep-loop-weyu"),
                     ("CMUSurveillanceCameraColonyCMB", "cmu-deep-loop-cmb"),
                 })
        {
            var button = _theme.Button(Loc.GetString(label));
            button.OnPressed += _ =>
            {
                OnClicked?.Invoke();
                _host.Loop(network);
            };
            loops.AddChild(button);
        }

        return new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(0, 14, 0, 0),
            Children =
            {
                new CMUTerminalRule { Color = _theme.Phosphor.WithAlpha(0.6f) },
                new Label { Text = Loc.GetString("cmu-deep-tools-heading"), StyleClasses = { "LabelHeading" } },
                new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, Children = { terminal, sniff } },
                new Label { Text = Loc.GetString("cmu-deep-loop-intro") },
                loops,
                output,
            },
        };
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _host.ToolResult += OnToolResult;
        _host.RelayReceived += OnRelayList;
        if (_relayList != null)
            _host.RequestRelays();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _host.ToolResult -= OnToolResult;
        _host.RelayReceived -= OnRelayList;
    }

    private void OnToolResult(CMUDeepToolResultMessage result)
    {
        if (_toolOutput == null)
            return;

        _toolOutput.RemoveAllChildren();
        _toolOutput.AddChild(new Label { Text = result.Title, StyleClasses = { "LabelKeyText" } });
        foreach (var line in result.Lines)
            _toolOutput.AddChild(new Label { Text = line });
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
                    title = CMUExtraNetSystem.Text(site.Title);
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
