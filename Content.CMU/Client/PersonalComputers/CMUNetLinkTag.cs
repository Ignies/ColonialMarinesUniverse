using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.RichText;
using Content.Client.UserInterface.ControlExtensions;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// ExtraNet hyperlink for page documents: <c>[netlink="Housing" link="colony.seegson.net/housing"]</c>.
/// Without <c>link</c> the text is the address. Clicks go to the nearest <see cref="ILinkClickHandler"/>.
/// </summary>
[UsedImplicitly]
public sealed class CMUNetLinkTag : IMarkupTagHandler
{
    public string Name => "netlink";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!node.Value.TryGetString(out var text))
            return false;

        var link = text;
        if (node.Attributes.TryGetValue("link", out var linkParameter) && linkParameter.TryGetString(out var explicitLink))
            link = explicitLink;

        control = new CMUNetLinkLabel(text, link);
        return true;
    }
}

/// <summary>
/// Underlined link that inverts on hover. The page view sets its colours from the page style.
/// </summary>
public sealed class CMUNetLinkLabel : Label
{
    public readonly string Link;

    private bool _hovered;
    private Color _color = Color.White;
    private Color _background = Color.Black;

    public CMUNetLinkLabel(string text, string link)
    {
        Text = text;
        Link = link;
        ToolTip = link;
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
        FontColorOverride = Color.White;

        OnMouseEntered += _ => SetHovered(true);
        OnMouseExited += _ => SetHovered(false);
        OnKeyBindDown += OnKeyDown;
    }

    public void SetColors(Color color, Color background)
    {
        _color = color;
        _background = background;
        SetHovered(_hovered);
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        FontColorOverride = hovered ? _background : _color;
    }

    private void OnKeyDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (this.TryGetParentHandler<ILinkClickHandler>(out var handler))
            handler.HandleClick(Link);

        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_hovered)
            handle.DrawRect(PixelSizeBox, _color);
        else
            handle.DrawLine(new Vector2(0, PixelHeight - 1), new Vector2(PixelWidth, PixelHeight - 1), _color);

        base.Draw(handle);
    }
}
