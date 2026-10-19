using System.Linq;
using System.Numerics;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Lays out an ExtraNet page or document with its style: background colour or image, fonts and colours.
/// Used by the browser and the Files viewer.
/// </summary>
public sealed partial class CMUNetPageView : Control
{
    [Dependency] private DocumentParsingManager _documents = default!;

    private readonly CMUComputerTheme _theme;
    private readonly PanelContainer _background;
    private readonly TextureRect _backgroundImage;
    private readonly ScrollContainer _scroll;
    private readonly BoxContainer _page;
    private readonly LayoutContainer _overlay;
    private readonly List<CMUNetPopup> _popups = new();
    private float _elapsed;
    private CMUNetPageStyle _style;

    public CMUNetPageView(CMUComputerTheme theme)
    {
        IoCManager.InjectDependencies(this);
        _theme = theme;
        _style = theme.DefaultPageStyle;
        HorizontalExpand = true;
        VerticalExpand = true;

        _background = new PanelContainer();
        _backgroundImage = new TextureRect { Visible = false };
        _page = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(14, 10),
            HorizontalExpand = true,
        };
        _scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            Children = { _page },
        };

        AddChild(_background);
        AddChild(_backgroundImage);
        AddChild(_scroll);

        // Pop ups float over the page, inside the browser window.
        _overlay = new LayoutContainer { MouseFilter = MouseFilterMode.Ignore };
        AddChild(_overlay);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_popups.Count == 0)
            return;

        _elapsed += args.DeltaSeconds;
        foreach (var popup in _popups)
        {
            if (popup.Closed || _elapsed < popup.Delay)
                continue;

            popup.Visible = true;
            var room = Vector2.Max(Vector2.Zero, Size - popup.DesiredSize);
            LayoutContainer.SetPosition(popup, room * popup.Place);
        }
    }

    /// <summary>Parses a page document. A CMUNetStyle tag in it overrides <paramref name="siteStyle"/>.</summary>
    public void ShowDocument(string text, CMUNetPageStyle? siteStyle)
    {
        var style = (siteStyle ?? new CMUNetPageStyle()).Over(_theme.DefaultPageStyle);

        // The base text colour is baked in while parsing, so a page that restyles itself is parsed twice.
        if (text.Contains("<CMUNetStyle", StringComparison.Ordinal))
        {
            var probe = new Document();
            Parse(probe, text, style);
            foreach (var marker in Descendants(probe))
            {
                if (marker is CMUNetStyle tag)
                    style = tag.Style.Over(style);
            }
        }

        Clear(style);
        Parse(_page, text, style);
        Finish(style);
    }

    /// <summary>Shows controls built in code, such as error pages and the directory, in the default style.</summary>
    public void ShowControls(Action<BoxContainer, CMUNetPageStyle> build)
    {
        var style = _theme.DefaultPageStyle;
        Clear(style);
        build(_page, style);
        Finish(style);
    }

    /// <summary>Adds a control under the page in the page's style, e.g. a board's postings.</summary>
    public void Append(Func<CMUNetPageStyle, Control> build)
    {
        _page.AddChild(build(_style));
        Finish(_style);
    }

    private void Parse(Control target, string text, CMUNetPageStyle style)
    {
        var previous = DocumentParsingManager.CMUTextColor;
        DocumentParsingManager.CMUTextColor = style.Text ?? Color.White;
        try
        {
            _documents.TryAddMarkup(target, text);
        }
        finally
        {
            DocumentParsingManager.CMUTextColor = previous;
        }
    }

    private void Clear(CMUNetPageStyle style)
    {
        _style = style;
        _page.RemoveAllChildren();
        _overlay.RemoveAllChildren();
        _popups.Clear();
        _elapsed = 0;
        _page.Stylesheet = BuildStylesheet(style);
        _scroll.SetScrollValue(Vector2.Zero);
        _background.PanelOverride = new StyleBoxFlat { BackgroundColor = style.Background ?? _theme.Background };

        _backgroundImage.Visible = _theme.TryGetTexture(style.BackgroundImage, out var texture);
        if (!_backgroundImage.Visible)
            return;

        _backgroundImage.Texture = texture;
        _backgroundImage.Stretch = style.BackgroundMode switch
        {
            CMUNetBackgroundMode.Tile => TextureRect.StretchMode.Tile,
            CMUNetBackgroundMode.Center => TextureRect.StretchMode.KeepCentered,
            _ => TextureRect.StretchMode.Scale,
        };
    }

    /// <summary>Links are drawn by code rather than styled, so they get their colours here.</summary>
    private void Finish(CMUNetPageStyle style)
    {
        var link = style.Link ?? _theme.Phosphor;
        var background = style.Background ?? _theme.Background;
        foreach (var popup in Descendants(_page).OfType<CMUNetPopup>().ToList())
        {
            popup.Orphan();
            _overlay.AddChild(popup);
            _popups.Add(popup);
        }

        foreach (var control in Descendants(_page))
        {
            if (control is CMUNetLinkLabel label)
                label.SetColors(link, background);
        }
    }

    private Stylesheet BuildStylesheet(CMUNetPageStyle style)
    {
        var font = style.Font ?? CMUNetFont.Sans;
        var text = style.Text ?? Color.White;
        var heading = style.Heading ?? text;

        return new Stylesheet(new[]
        {
            Rule(typeof(RichTextLabel), null, _theme.GetFont(font, 12)),
            Rule(typeof(Label), null, _theme.GetFont(font, 12), text),
            Rule(typeof(Label), "LabelHeadingBigger", _theme.GetFont(font, 20, true), heading),
            Rule(typeof(Label), "LabelHeading", _theme.GetFont(font, 16, true), heading),
            Rule(typeof(Label), "LabelKeyText", _theme.GetFont(font, 13, true), heading),
            FieldRule(typeof(LineEdit), style, true),
            FieldRule(typeof(TextEdit), style, false),
            new StyleRule(new SelectorElement(typeof(LineEdit), null, null, new[] { LineEdit.StylePseudoClassPlaceholder }),
                new[] { new StyleProperty("font-color", text.WithAlpha(0.45f)) }),
            new StyleRule(new SelectorElement(typeof(TextEdit), null, null, new[] { TextEdit.StylePseudoClassPlaceholder }),
                new[] { new StyleProperty("font-color", text.WithAlpha(0.45f)) }),
        });
    }

    /// <summary>Text fields on a page, which otherwise lose their look under the page's own stylesheet.</summary>
    private StyleRule FieldRule(Type type, CMUNetPageStyle style, bool boxed)
    {
        var text = style.Text ?? Color.White;
        var properties = new List<StyleProperty>
        {
            new("font", _theme.GetFont(CMUNetFont.Mono, 12)),
            new("font-color", text),
            new("cursor-color", style.Link ?? text),
            new("selection-color", (style.Link ?? text).WithAlpha(0.35f)),
        };
        if (boxed)
        {
            properties.Add(new StyleProperty(LineEdit.StylePropertyStyleBox, new StyleBoxFlat
            {
                BackgroundColor = style.Background ?? _theme.Background,
                BorderColor = (style.Link ?? text).WithAlpha(0.6f),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            }));
        }

        return new StyleRule(new SelectorElement(type, null, null, null), properties);
    }

    private static StyleRule Rule(Type type, string? styleClass, Font font, Color? color = null)
    {
        var properties = new List<StyleProperty> { new("font", font) };
        if (color != null)
            properties.Add(new StyleProperty("font-color", color.Value));

        return new StyleRule(
            new SelectorElement(type, styleClass == null ? null : new[] { styleClass }, null, null),
            properties);
    }

    public static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    /// <summary>True when the last page laid out had markup the parser rejected.</summary>
    public bool HasErrors()
    {
        foreach (var control in Descendants(_page))
        {
            if (control is GuidebookError)
                return true;
        }

        return false;
    }
}
