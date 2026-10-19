using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// The contraband market on a deep page: <c>&lt;CMUNetMarket/&gt;</c>. Lists goods by seller handle and price,
/// buys from the logged in account, shows the dead drops of what this account bought, and lets insurgents list.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetMarket : BoxContainer, IDocumentTag
{
    private ICMUDeepHost? _host;

    public CMUNetMarket()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        AddChild(new Label { Text = Loc.GetString("cmu-market-offline") });
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is CMUNetBrowser browser)
            {
                _host = browser.Deep;
                break;
            }
        }

        if (_host == null)
            return;

        _host.MarketReceived += OnState;
        _host.RequestMarket();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.MarketReceived -= OnState;
    }

    private void OnState(CMUMarketStateMessage state)
    {
        RemoveAllChildren();
        if (state.Listings.Count == 0)
            AddChild(new Label { Text = Loc.GetString("cmu-market-empty") });

        foreach (var listing in state.Listings)
        {
            var buy = new Button { Text = Loc.GetString("cmu-market-buy", ("price", listing.Price)) };
            var id = listing.Id;
            buy.OnPressed += _ => _host?.Buy(id);
            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Children =
                {
                    new Label { Text = Loc.GetString("cmu-market-listing", ("title", listing.Title), ("seller", listing.Seller)), HorizontalExpand = true, ClipText = true },
                    buy,
                },
            });
            if (listing.Description.Length > 0)
                AddChild(Plain(listing.Description));
        }

        if (state.Purchases.Count > 0)
        {
            AddChild(new Label { Text = Loc.GetString("cmu-market-bought"), StyleClasses = { "LabelKeyText" } });
            foreach (var purchase in state.Purchases)
                AddChild(Plain(Loc.GetString("cmu-market-drop", ("title", purchase.Title), ("drop", purchase.Drop))));
        }

        if (state.CanSell)
            AddChild(Seller());
    }

    private Control Seller()
    {
        var title = new LineEdit { PlaceHolder = Loc.GetString("cmu-market-title"), HorizontalExpand = true };
        title.IsValid = text => text.Length <= CMUMarketLimits.TitleLength;
        var description = new LineEdit { PlaceHolder = Loc.GetString("cmu-market-description"), HorizontalExpand = true };
        description.IsValid = text => text.Length <= CMUMarketLimits.DescriptionLength;
        var price = new LineEdit { PlaceHolder = Loc.GetString("cmu-market-price"), MinWidth = 100 };
        price.IsValid = text => text.Length <= 5 && text.All(char.IsDigit);
        var drop = new LineEdit { PlaceHolder = Loc.GetString("cmu-market-drop-placeholder"), HorizontalExpand = true };
        drop.IsValid = text => text.Length <= CMUMarketLimits.DropLength;
        var list = new Button { Text = Loc.GetString("cmu-market-list"), HorizontalAlignment = HAlignment.Left };
        list.OnPressed += _ =>
        {
            if (!int.TryParse(price.Text, out var amount) || title.Text.Trim().Length == 0 || drop.Text.Trim().Length == 0)
                return;

            _host?.ListItem(title.Text, description.Text, amount, drop.Text);
        };

        return new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                new Label { Text = Loc.GetString("cmu-market-sell"), StyleClasses = { "LabelKeyText" } },
                title,
                description,
                new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, Children = { price, drop } },
                list,
            },
        };
    }

    private static RichTextLabel Plain(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }
}
