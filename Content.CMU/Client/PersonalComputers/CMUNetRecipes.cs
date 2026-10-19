using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Kitchen;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A recipe book built from the game's own data, so it is never out of date:
/// <c>&lt;CMUNetRecipes Kind="Food"/&gt;</c> for microwave recipes or <c>&lt;CMUNetRecipes Kind="Chemistry"/&gt;</c>
/// for reactions, optionally <c>Group="Medicine"</c> to keep to one group. Each card shows the result's sprite,
/// its ingredients and how it is made. An ingredient that is itself made by a recipe is a link: hovering shows
/// its card, clicking searches for it.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetRecipes : BoxContainer, IDocumentTag
{
    private const int PageSize = 12;
    private static readonly EntProtoId Microwave = "CMMicrowave";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entities = default!;

    private readonly RMCReagentSystem _reagents;
    private readonly List<Recipe> _recipes = new();
    private readonly Dictionary<string, Recipe> _makesEntity = new();
    private readonly Dictionary<string, Recipe> _makesReagent = new();
    private readonly LineEdit _search;
    private readonly BoxContainer _cards;
    private readonly Label _count;
    private readonly Button _more;
    private List<Recipe> _shown = new();
    private int _page = 1;

    private sealed record Ingredient(string Name, FixedPoint2 Amount, string? Entity, ReagentPrototype? Reagent, bool Catalyst);

    private sealed record Recipe(string Name, string? Entity, ReagentPrototype? Reagent, FixedPoint2 Amount, string Group,
        List<Ingredient> Ingredients, string Method, string Detail);

    public CMUNetRecipes()
    {
        IoCManager.InjectDependencies(this);
        _reagents = _entities.System<RMCReagentSystem>();
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 8;

        _search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-net-recipes-search") };
        _search.OnTextChanged += _ => Filter();
        _count = new Label();
        _cards = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 8, HorizontalExpand = true };
        _more = new Button { Text = Loc.GetString("cmu-net-recipes-more"), HorizontalAlignment = HAlignment.Center };
        _more.OnPressed += _ =>
        {
            _page++;
            Show();
        };
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Kind", out var kind))
            return false;

        args.TryGetValue("Group", out var group);
        switch (kind)
        {
            case "Food":
                LoadFood(group);
                break;
            case "Chemistry":
                LoadChemistry(group);
                break;
            default:
                return false;
        }

        _recipes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Children = { _search, _count },
        });
        AddChild(_cards);
        AddChild(_more);
        Filter();
        control = this;
        return true;
    }

    private void LoadFood(string? group)
    {
        foreach (var recipe in _prototypes.EnumeratePrototypes<FoodRecipePrototype>())
        {
            if (recipe.SecretRecipe
                || group != null && recipe.Group != group
                || !_prototypes.TryIndex<EntityPrototype>(recipe.Result, out var result))
                continue;

            var ingredients = new List<Ingredient>();
            foreach (var (solid, amount) in recipe.IngredientsSolids)
            {
                if (_prototypes.TryIndex<EntityPrototype>(solid, out var item))
                    ingredients.Add(new Ingredient(item.Name, amount, solid, null, false));
            }

            foreach (var (reagent, amount) in recipe.IngredientsReagents)
            {
                if (_reagents.TryIndex(reagent, out var proto))
                    ingredients.Add(new Ingredient(proto.LocalizedName, amount, null, proto, false));
            }

            var name = recipe.Name.Length > 0 ? recipe.Name : result.Name;
            var card = new Recipe(name, result.ID, null, 1, recipe.Group, ingredients, "microwave",
                Loc.GetString("cmu-net-recipes-seconds", ("seconds", recipe.CookTime)));
            _recipes.Add(card);
            _makesEntity.TryAdd(result.ID, card);
        }

        // Reagents such as dough or batter come from reactions; show those too when hovered.
        LoadChemistry(null, false);
    }

    private void LoadChemistry(string? group, bool listed = true)
    {
        foreach (var reaction in _prototypes.EnumeratePrototypes<ReactionPrototype>())
        {
            if (reaction.Source || reaction.Products.Count == 0)
                continue;

            var (productId, amount) = reaction.Products.First();
            if (!_reagents.TryIndex(productId, out var product) || !product.IsCM)
                continue;

            var ingredients = new List<Ingredient>();
            foreach (var (reagent, info) in reaction.Reactants)
            {
                if (_reagents.TryIndex(reagent, out var proto))
                    ingredients.Add(new Ingredient(proto.LocalizedName, info.Amount, null, proto, info.Catalyst));
            }

            var detail = reaction.MinimumTemperature > 0
                ? Loc.GetString("cmu-net-recipes-heat", ("kelvin", (int) reaction.MinimumTemperature))
                : Loc.GetString("cmu-net-recipes-mix");
            var card = new Recipe(product.LocalizedName, null, product, amount, product.Group, ingredients, "mix", detail);
            if (!_makesReagent.TryAdd(product.ID, card))
                continue;

            if (listed && (group == null || product.Group == group))
                _recipes.Add(card);
        }
    }

    private void Filter()
    {
        var text = _search.Text.Trim();
        _shown = text.Length == 0
            ? _recipes
            : _recipes.Where(r => r.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || r.Ingredients.Any(i => i.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))).ToList();
        _page = 1;
        Show();
    }

    private void Show()
    {
        _cards.RemoveAllChildren();
        var count = Math.Min(_shown.Count, _page * PageSize);
        for (var i = 0; i < count; i++)
            _cards.AddChild(Card(_shown[i], true));

        _count.Text = Loc.GetString("cmu-net-recipes-count", ("shown", count), ("total", _shown.Count));
        _more.Visible = count < _shown.Count;
    }

    /// <summary>A recipe card: the result, its ingredients, and the method on the right.</summary>
    private Control Card(Recipe recipe, bool links)
    {
        var header = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, Margin = new Thickness(6, 4) };
        header.AddChild(Icon(recipe.Entity, recipe.Reagent, 2));
        header.AddChild(new Label
        {
            Text = recipe.Amount > 1 ? $"{recipe.Name} x{recipe.Amount}" : recipe.Name,
            StyleClasses = { "LabelKeyText" },
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true,
        });

        var list = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2, HorizontalExpand = true, Margin = new Thickness(8, 4) };
        foreach (var ingredient in recipe.Ingredients)
            list.AddChild(IngredientRow(ingredient, links));

        var method = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 90,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        if (recipe.Method == "microwave")
            method.AddChild(Icon(Microwave, null, 1.5f));
        method.AddChild(new Label { Text = recipe.Detail, Align = Label.AlignMode.Center, HorizontalAlignment = HAlignment.Center });

        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Black.WithAlpha(0.35f),
                BorderColor = Color.White.WithAlpha(0.15f),
                BorderThickness = new Thickness(1),
            },
            HorizontalExpand = true,
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        new PanelContainer
                        {
                            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.White.WithAlpha(0.07f) },
                            Children = { header },
                        },
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            Children = { list, method },
                        },
                    },
                },
            },
        };
    }

    private Control IngredientRow(Ingredient ingredient, bool links)
    {
        var amount = ingredient.Reagent != null ? $"{ingredient.Amount}u" : $"{ingredient.Amount}";
        var text = ingredient.Catalyst
            ? Loc.GetString("cmu-net-recipes-catalyst", ("amount", amount), ("name", ingredient.Name))
            : $"{amount} {ingredient.Name}";

        var made = ingredient.Entity != null
            ? _makesEntity.GetValueOrDefault(ingredient.Entity)
            : ingredient.Reagent != null ? _makesReagent.GetValueOrDefault(ingredient.Reagent.ID) : null;

        var label = new Label { Text = text, VerticalAlignment = VAlignment.Center };
        if (made != null && links)
        {
            // Made things are dotted links: hover for their card, click to look them up.
            label.MouseFilter = MouseFilterMode.Stop;
            label.DefaultCursorShape = CursorShape.Hand;
            label.TooltipSupplier = _ => new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#202226"), BorderColor = Color.White.WithAlpha(0.3f), BorderThickness = new Thickness(1) },
                MaxWidth = 340,
                Stylesheet = PageStylesheet(),
                Children = { Card(made, false) },
            };
            label.OnKeyBindDown += args =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                    return;

                args.Handle();
                _search.Text = made.Name;
                Filter();
            };
        }

        return new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children =
            {
                Icon(ingredient.Entity, ingredient.Reagent, 1),
                made != null && links ? new DottedUnderline(label) : label,
            },
        };
    }

    /// <summary>Tooltips live outside the page, so they borrow its fonts and colours.</summary>
    private Stylesheet? PageStylesheet()
    {
        for (var control = (Control?) this; control != null; control = control.Parent)
        {
            if (control.Stylesheet != null)
                return control.Stylesheet;
        }

        return null;
    }

    /// <summary>An item's own sprite, or a vial in the reagent's colour.</summary>
    private Control Icon(string? entity, ReagentPrototype? reagent, float scale)
    {
        if (entity != null)
        {
            var texture = _entities.System<SpriteSystem>().Frame0(new SpriteSpecifier.EntityPrototype(entity));
            return new TextureRect
            {
                Texture = texture,
                TextureScale = new Vector2(scale, scale),
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepCentered,
                MinSize = new Vector2(32 * scale, 32 * scale),
            };
        }

        var color = reagent?.SubstanceColor ?? Color.Gray;
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = color, BorderColor = Color.White.WithAlpha(0.6f), BorderThickness = new Thickness(1) },
            SetSize = new Vector2(10 * scale + 2, 14 * scale + 2),
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(11 * scale, 0, 11 * scale, 0),
        };
    }

    /// <summary>A label with a dashed underline, the mark of something that has its own recipe.</summary>
    private sealed class DottedUnderline : Control
    {
        private readonly Label _label;

        public DottedUnderline(Label label)
        {
            _label = label;
            AddChild(label);
            VerticalAlignment = VAlignment.Center;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var y = PixelHeight - 1;
            var color = (_label.FontColorOverride ?? Color.White).WithAlpha(0.6f);
            for (var x = 0f; x < PixelWidth; x += 6 * UIScale)
                handle.DrawLine(new Vector2(x, y), new Vector2(MathF.Min(x + 3 * UIScale, PixelWidth), y), color);
        }
    }
}
