using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Page style override: <c>&lt;CMUNetStyle Background="#0A1A2A" Text="#E0F0FF" Heading="#FFD060" Link="#7FD0FF"
/// Font="Sans" BackgroundImage="/Textures/..." BackgroundMode="Tile"/&gt;</c>. Every attribute is optional.
/// Draws nothing; the browser reads it before laying out the page.
/// </summary>
[UsedImplicitly]
public sealed class CMUNetStyle : Control, IDocumentTag
{
    public readonly CMUNetPageStyle Style = new();

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        foreach (var (key, value) in args)
        {
            switch (key)
            {
                case "Background" when Color.TryFromHex(value, out var c0):
                    Style.Background = c0;
                    break;
                case "Text" when Color.TryFromHex(value, out var c1):
                    Style.Text = c1;
                    break;
                case "Heading" when Color.TryFromHex(value, out var c2):
                    Style.Heading = c2;
                    break;
                case "Link" when Color.TryFromHex(value, out var c3):
                    Style.Link = c3;
                    break;
                case "Font" when Enum.TryParse<CMUNetFont>(value, out var font):
                    Style.Font = font;
                    break;
                case "BackgroundImage":
                    Style.BackgroundImage = new ResPath(value);
                    break;
                case "BackgroundMode" when Enum.TryParse<CMUNetBackgroundMode>(value, out var mode):
                    Style.BackgroundMode = mode;
                    break;
                default:
                    return false;
            }
        }

        Visible = false;
        control = this;
        return true;
    }
}

/// <summary>
/// An RSI state, animated if it has frames: <c>&lt;CMUNetSprite Rsi="/Textures/_RMC14/Structures/Machines/computer.rsi"
/// State="terminal1" Scale="2"/&gt;</c>.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetSprite : BoxContainer, IDocumentTag
{
    [Dependency] private IResourceCache _cache = default!;

    public CMUNetSprite()
    {
        IoCManager.InjectDependencies(this);
        Margin = new Thickness(0, 4);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Rsi", out var rsi) || !args.TryGetValue("State", out var state))
            return false;

        var path = new ResPath(rsi);
        var full = path.IsRooted ? path : SpriteSpecifierSerializer.TextureRoot / path;
        if (!_cache.TryGetResource<RSIResource>(full, out var resource) || !resource.RSI.TryGetState(state, out _))
            return false;

        var scale = 2f;
        if (args.TryGetValue("Scale", out var scaleText) && !float.TryParse(scaleText, System.Globalization.CultureInfo.InvariantCulture, out scale))
            return false;

        var relative = full.RelativeTo(SpriteSpecifierSerializer.TextureRoot);
        var sprite = new AnimatedTextureRect();
        sprite.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(relative, state));
        sprite.DisplayRect.TextureScale = new Vector2(scale, scale);
        AddChild(sprite);

        HorizontalAlignment = args.TryGetValue("Align", out var align) && Enum.TryParse<HAlignment>(align, out var h)
            ? h
            : HAlignment.Center;
        control = this;
        return true;
    }
}

/// <summary>Children blink on and off: <c>&lt;CMUNetBlink Rate="0.5"&gt;...&lt;/CMUNetBlink&gt;</c>.</summary>
[UsedImplicitly]
public sealed class CMUNetBlink : BoxContainer, IDocumentTag
{
    private float _rate = 0.6f;
    private float _timer;

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (args.TryGetValue("Rate", out var rate)
            && !float.TryParse(rate, System.Globalization.CultureInfo.InvariantCulture, out _rate))
            return false;

        Orientation = LayoutOrientation.Vertical;
        control = this;
        return true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _timer += args.DeltaSeconds;
        if (_timer < _rate)
            return;

        _timer = 0;
        Modulate = Modulate.A > 0.5f ? Color.Transparent : Color.White;
    }
}

/// <summary>Text scrolling across the page: <c>&lt;CMUNetMarquee Text="..." Speed="60" Color="#FFD060"/&gt;</c>.</summary>
[UsedImplicitly]
public sealed partial class CMUNetMarquee : Control, IDocumentTag
{
    private string _text = string.Empty;
    private float _speed = 60;
    private Color _color;
    private float _offset;
    private float _width;

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Text", out var text))
            return false;

        if (args.TryGetValue("Speed", out var speed)
            && !float.TryParse(speed, System.Globalization.CultureInfo.InvariantCulture, out _speed))
            return false;

        _color = DocumentParsingManager.CMUTextColor;
        if (args.TryGetValue("Color", out var color))
        {
            if (!Color.TryFromHex(color, out var c))
                return false;

            _color = c;
        }

        _text = text;
        RectClipContent = true;
        HorizontalExpand = true;
        Margin = new Thickness(0, 2, 0, 10);
        control = this;
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(0, MarqueeFont().GetLineHeight(1) + 2);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _offset += args.DeltaSeconds * _speed;
        // One full pass: enters at the right edge, leaves past the left one.
        if (_width > 0)
            _offset %= PixelWidth / UIScale + _width;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var font = MarqueeFont();
        var x = PixelWidth - _offset * UIScale;
        _width = handle.DrawString(font, new Vector2(x, 0), _text, UIScale, _color).X / UIScale;
    }

    private Font MarqueeFont()
    {
        return TryGetStyleProperty<Font>("font", out var font)
            ? font
            : UserInterfaceManager.ThemeDefaults.DefaultFont;
    }
}
