using System.Numerics;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

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

    public CMUNetPageView(CMUComputerTheme theme)
    {
        IoCManager.InjectDependencies(this);
        _theme = theme;
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
        _page.RemoveAllChildren();
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
        });
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
