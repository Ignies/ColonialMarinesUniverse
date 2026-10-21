using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// An ExtraNet shop on a page: <c>&lt;CMUNetShop Catalog="CMUWeyBayCatalog"/&gt;</c>. The balance of the card in the
/// reader and its orders, tracked to the pickup, then the catalogue by heading: each item's picture, name, line,
/// price and delivery time, with a BUY button that charges the card. Optional colours: <c>Text</c>, <c>Accent</c>,
/// <c>Muted</c>, <c>Panel</c>.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetShop : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Label _balance;
    private readonly BoxContainer _orders;
    private readonly BoxContainer _catalogue;
    private string _catalog = string.Empty;
    private ICMUComputerHost? _host;
    private CMUShopStateMessage? _state;
    private float _refresh;

    private Color _text = Color.FromHex("#1C1C1C");
    private Color _accent = Color.FromHex("#D4342C");
    private Color _muted = Color.FromHex("#6A6A6A");
    private Color _panel = Color.FromHex("#F2EEE4");

    public CMUNetShop()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);
        _balance = new Label();
        _orders = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2 };
        _catalogue = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Catalog", out var catalog) || !_prototypes.TryIndex<CMUShopCatalogPrototype>(catalog, out var proto))
            return false;

        _catalog = catalog;
        _text = Colour(args, "Text", _text);
        _accent = Colour(args, "Accent", _accent);
        _muted = Colour(args, "Muted", _muted);
        _panel = Colour(args, "Panel", _panel);
        _balance.FontColorOverride = _text;

        AddChild(_balance);
        AddChild(Heading(Loc.GetString("cmu-shop-your-orders")));
        AddChild(_orders);
        AddChild(_catalogue);
        BuildCatalogue(proto);
        control = this;
        return true;
    }

    private static Color Colour(Dictionary<string, string> args, string key, Color fallback)
    {
        return args.TryGetValue(key, out var value) && Color.TryFromHex(value, out var colour) ? colour : fallback;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _host = Browser()?.Host;
        if (_host == null)
        {
            ShowState(new CMUShopStateMessage(new List<CMUShopOrderView>(), -1));
            return;
        }

        _host.ShopReceived += ShowState;
        _host.RequestShop();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.ShopReceived -= ShowState;

        _host = null;
    }

    /// <summary>Asks again every few seconds while the page is up, so orders move along on their own.</summary>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_host == null)
            return;

        _refresh -= args.DeltaSeconds;
        if (_refresh > 0)
            return;

        _refresh = 5;
        _host.RequestShop();
        if (_state != null)
            ShowOrders(_state);
    }

    private void BuildCatalogue(CMUShopCatalogPrototype catalog)
    {
        foreach (var group in catalog.Entries.Select((entry, index) => (entry, index)).GroupBy(e => e.entry.Category))
        {
            _catalogue.AddChild(Heading(Loc.GetString(group.Key)));
            foreach (var (entry, index) in group)
            {
                if (!_prototypes.TryIndex(entry.Item, out var item))
                    continue;

                var buy = Button(Loc.GetString("cmu-shop-buy"));
                var at = index;
                buy.OnPressed += _ => _host?.BuyShop(_catalog, at);

                var words = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
                words.AddChild(new Label { Text = item.Name, FontColorOverride = _text, ClipText = true });
                var blurb = entry.Blurb is { } line ? Loc.GetString(line) : item.Description;
                words.AddChild(new Label { Text = blurb, FontColorOverride = _muted, ClipText = true });
                words.AddChild(new Label
                {
                    Text = Loc.GetString("cmu-shop-price", ("price", entry.Price), ("minutes", (int) Math.Ceiling(entry.Minutes))),
                    FontColorOverride = _accent,
                });

                _catalogue.AddChild(new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = _panel, ContentMarginLeftOverride = 6, ContentMarginRightOverride = 6, ContentMarginTopOverride = 4, ContentMarginBottomOverride = 4 },
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            SeparationOverride = 8,
                            Children =
                            {
                                new EntityPrototypeView(entry.Item, _entities) { SetSize = new System.Numerics.Vector2(48, 48), Scale = new System.Numerics.Vector2(1.5f, 1.5f) },
                                words,
                                buy,
                            },
                        },
                    },
                });
            }
        }
    }

    private void ShowState(CMUShopStateMessage state)
    {
        _state = state;
        _balance.Text = state.Balance < 0
            ? Loc.GetString("cmu-shop-balance-none")
            : Loc.GetString("cmu-shop-balance", ("balance", state.Balance));
        ShowOrders(state);
    }

    private void ShowOrders(CMUShopStateMessage state)
    {
        _orders.RemoveAllChildren();
        if (state.Orders.Count == 0)
        {
            _orders.AddChild(new Label { Text = Loc.GetString("cmu-shop-no-orders"), FontColorOverride = _muted });
            return;
        }

        var now = _timing.CurTime;
        foreach (var order in state.Orders)
        {
            var name = _prototypes.TryIndex<EntityPrototype>(order.Item, out var item) ? item.Name : order.Item;
            var minutes = Math.Max(0, (int) Math.Ceiling((order.Due - now).TotalMinutes));
            var status = order.State switch
            {
                CMUShopOrderState.Arrived => Loc.GetString("cmu-shop-order-arrived", ("where", order.Where)),
                CMUShopOrderState.InTransit => Loc.GetString("cmu-shop-order-transit", ("minutes", minutes)),
                _ => Loc.GetString("cmu-shop-order-processing", ("minutes", minutes)),
            };
            _orders.AddChild(new Label
            {
                Text = Loc.GetString("cmu-shop-order-line", ("id", order.Id), ("item", name), ("status", status)),
                FontColorOverride = order.State == CMUShopOrderState.Arrived ? _accent : _text,
            });
        }
    }

    private Label Heading(string text)
    {
        return new Label { Text = text, FontColorOverride = _accent, Margin = new Thickness(0, 6, 0, 0) };
    }

    private Button Button(string text)
    {
        var button = new Button
        {
            Text = text,
            VerticalAlignment = VAlignment.Center,
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = _accent,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            },
        };
        button.Label.FontColorOverride = Color.White;
        return button;
    }

    private CMUNetBrowser? Browser()
    {
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is CMUNetBrowser browser)
                return browser;
        }

        return null;
    }
}
