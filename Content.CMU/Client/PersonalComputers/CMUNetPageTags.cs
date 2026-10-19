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
using Content.Client.Station;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
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

/// <summary>
/// A pop up drawn over the page inside the browser window, with a title bar and a close box:
/// <c>&lt;CMUNetPopup Title="..." Text="..." Link="host" LinkText="..." Icon="/Textures/..." X="0.6" Y="0.2"
/// Delay="1.5" Background="#202020" Accent="#FFD200"/&gt;</c>. X and Y place it as a fraction of the page view,
/// Delay is how long after the page opens it appears. Only Title and Text are required.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetPopup : PanelContainer, IDocumentTag
{
    [Dependency] private IResourceCache _cache = default!;

    public Vector2 Place = new(0.5f, 0.3f);
    public float Delay = 1f;
    public bool Closed;

    public CMUNetPopup()
    {
        IoCManager.InjectDependencies(this);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Title", out var title) || !args.TryGetValue("Text", out var text))
            return false;

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (args.TryGetValue("X", out var x) && float.TryParse(x, culture, out var px))
            Place.X = Math.Clamp(px, 0, 1);
        if (args.TryGetValue("Y", out var y) && float.TryParse(y, culture, out var py))
            Place.Y = Math.Clamp(py, 0, 1);
        if (args.TryGetValue("Delay", out var delay) && float.TryParse(delay, culture, out var pd))
            Delay = Math.Max(0, pd);

        var background = args.TryGetValue("Background", out var bg) && Color.TryFromHex(bg, out var b) ? b : Color.FromHex("#1A1A1A");
        var accent = args.TryGetValue("Accent", out var ac) && Color.TryFromHex(ac, out var a) ? a : Color.FromHex("#FFD200");

        // Text goes dark on light pop ups and light on dark ones, whatever page they sit on.
        var ink = background.R * 0.299f + background.G * 0.587f + background.B * 0.114f > 0.55f
            ? Color.FromHex("#141414")
            : Color.FromHex("#F2F2F2");
        var close = new Button { Text = "X", ModulateSelfOverride = Color.White };
        close.StyleBoxOverride = new StyleBoxFlat { BackgroundColor = background, BorderColor = Color.Black, BorderThickness = new Thickness(1) };
        close.Label.FontColorOverride = accent;
        close.OnPressed += _ =>
        {
            Closed = true;
            Visible = false;
        };

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, Margin = new Thickness(6) };
        if (args.TryGetValue("Icon", out var icon) && _cache.TryGetResource<TextureResource>(new ResPath(icon), out var texture))
        {
            body.AddChild(new TextureRect
            {
                Texture = texture.Texture,
                TextureScale = new Vector2(2, 2),
                VerticalAlignment = VAlignment.Top,
            });
        }

        var message = new RichTextLabel { HorizontalExpand = true };
        var formatted = new FormattedMessage();
        formatted.PushColor(ink);
        formatted.AddText(text);
        formatted.Pop();
        message.SetMessage(formatted);
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 6 };
        column.AddChild(message);
        if (args.TryGetValue("Link", out var link))
        {
            var label = new CMUNetLinkLabel(args.TryGetValue("LinkText", out var linkText) ? linkText : link, link)
            {
                HorizontalAlignment = HAlignment.Left,
                ClipText = true,
            };
            label.SetColors(accent, background);
            column.AddChild(label);
        }

        body.AddChild(column);
        PanelOverride = new StyleBoxFlat { BackgroundColor = background, BorderColor = accent, BorderThickness = new Thickness(2) };
        SetWidth = 250;
        MouseFilter = MouseFilterMode.Stop;
        Visible = false;
        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = accent },
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Horizontal,
                            Margin = new Thickness(4, 1, 1, 1),
                            Children =
                            {
                                new Label { Text = title, FontColorOverride = background, HorizontalExpand = true, ClipText = true },
                                close,
                            },
                        },
                    },
                },
                body,
            },
        });

        control = this;
        return true;
    }
}

/// <summary>
/// A product ad from a <see cref="Content.Shared.CMU14.PersonalComputers.DeepWeb.CMUDeepAdPrototype"/>:
/// <c>&lt;CMUNetAd Id="CMUDeepAdPowerLoader" Flip="true"/&gt;</c>. The product is the entity itself, drawn
/// from its sprite, on a banner in the ad's colours. Flip puts the product on the right.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetAd : Control, IDocumentTag
{
    private const string BadgeRsi = "/Textures/CMU14/PersonalComputers/Sites/Ads/badges.rsi";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private Color _top;
    private Color _bottom;
    private Color _accent;
    private ProtoId<StartingGearPrototype>? _gear;
    private SpriteView? _person;
    private EntityUid? _dummy;

    public CMUNetAd()
    {
        IoCManager.InjectDependencies(this);
        Margin = new Thickness(0, 6);
        HorizontalExpand = true;
        MinHeight = 104;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Id", out var id)
            || !_prototypes.TryIndex<Content.Shared.CMU14.PersonalComputers.DeepWeb.CMUDeepAdPrototype>(id, out var ad)
            || ad.Gear is { } gear && !_prototypes.HasIndex(gear)
            || ad.Gear == null && (ad.Entity is not { } entity || !_prototypes.HasIndex(entity)))
            return false;

        _top = ad.Top;
        _bottom = ad.Bottom;
        _accent = ad.Accent;

        _gear = ad.Gear;
        _person = new SpriteView(_entities)
        {
            Scale = new Vector2(ad.Scale, ad.Scale),
            OverrideDirection = Direction.South,
            MinSize = new Vector2(32 * ad.Scale, 32 * ad.Scale),
            VerticalAlignment = VAlignment.Center,
        };
        Control product = ad.Gear != null
            ? _person
            : new EntityPrototypeView(ad.Entity, _entities)
            {
                Scale = new Vector2(ad.Scale, ad.Scale),
                OverrideDirection = Direction.South,
                MinSize = new Vector2(32 * ad.Scale, 32 * ad.Scale),
                VerticalAlignment = VAlignment.Center,
            };

        var words = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            Children =
            {
                new Label { Text = Loc.GetString(ad.Brand).ToUpperInvariant(), StyleClasses = { "LabelKeyText" }, FontColorOverride = ad.Accent },
                new Label { Text = Loc.GetString(ad.Product), StyleClasses = { "LabelHeadingBigger" }, FontColorOverride = ad.Text, ClipText = true },
                new Label { Text = Loc.GetString(ad.Slogan), FontColorOverride = ad.Text.WithAlpha(0.85f), ClipText = true },
            },
        };

        var corner = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalAlignment = VAlignment.Center, SeparationOverride = 4 };
        if (ad.Badge.Length > 0)
        {
            var badge = new AnimatedTextureRect { HorizontalAlignment = HAlignment.Center };
            badge.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(new ResPath(BadgeRsi).RelativeTo(SpriteSpecifierSerializer.TextureRoot), ad.Badge));
            badge.DisplayRect.TextureScale = new Vector2(2, 2);
            corner.AddChild(badge);
        }

        if (ad.Price.Length > 0)
        {
            corner.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = ad.Accent, BorderColor = Color.Black, BorderThickness = new Thickness(1) },
                Children = { new Label { Text = ad.Price, StyleClasses = { "LabelHeading" }, FontColorOverride = ad.Bottom, Margin = new Thickness(6, 0) } },
            });
        }

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            Margin = new Thickness(10, 6),
        };
        var flip = args.TryGetValue("Flip", out var flipText) && bool.TryParse(flipText, out var flipped) && flipped;
        if (flip)
        {
            row.AddChild(words);
            row.AddChild(corner);
            row.AddChild(product);
        }
        else
        {
            row.AddChild(product);
            row.AddChild(words);
            row.AddChild(corner);
        }

        AddChild(row);
        control = this;
        return true;
    }

    // A dressed doll lives only while the ad is on screen, so popping the window out spawns it again.
    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_gear is not { } gear || _person == null || _dummy != null)
            return;

        var doll = _prototypes.Index(HumanoidCharacterProfile.DefaultSpecies).DollPrototype;
        var dummy = _entities.SpawnEntity(doll, MapCoordinates.Nullspace);
        _entities.System<StationSpawningSystem>().EquipStartingGear(dummy, gear);
        _dummy = dummy;
        _person.SetEntity(dummy);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_dummy is not { } dummy)
            return;

        _person?.SetEntity(null);
        _entities.QueueDeleteEntity(dummy);
        _dummy = null;
    }

    /// <summary>A gradient banner with a frame and faint scanlines, like a CRT commercial.</summary>
    protected override void Draw(DrawingHandleScreen handle)
    {
        var bands = 24;
        var height = PixelHeight / (float) bands;
        for (var i = 0; i < bands; i++)
        {
            var colour = Color.InterpolateBetween(_top, _bottom, i / (float) (bands - 1));
            handle.DrawRect(new UIBox2(0, i * height, PixelWidth, (i + 1) * height + 1), colour);
        }

        var line = Color.Black.WithAlpha(0.12f);
        for (var y = 0f; y < PixelHeight; y += 3 * UIScale)
            handle.DrawLine(new Vector2(0, y), new Vector2(PixelWidth, y), line);

        var frame = MathF.Max(1, MathF.Round(2 * UIScale));
        handle.DrawRect(new UIBox2(0, 0, PixelWidth, frame), _accent);
        handle.DrawRect(new UIBox2(0, PixelHeight - frame, PixelWidth, PixelHeight), _accent);
        handle.DrawRect(new UIBox2(0, 0, frame, PixelHeight), _accent);
        handle.DrawRect(new UIBox2(PixelWidth - frame, 0, PixelWidth, PixelHeight), _accent);
    }
}

/// <summary>
/// A wallpaper offered on a page: <c>&lt;CMUNetWallpaper Id="CMUWallpaperSynthwave"/&gt;</c>. Shows a preview
/// and a button that puts it on the desktop of the terminal reading the page.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetWallpaper : BoxContainer, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _cache = default!;

    private ProtoId<Content.Shared.CMU14.PersonalComputers.CMUWallpaperPrototype>? _id;

    public CMUNetWallpaper()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(0, 6);
        SeparationOverride = 4;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Id", out var id)
            || !_prototypes.TryIndex<Content.Shared.CMU14.PersonalComputers.CMUWallpaperPrototype>(id, out var wallpaper)
            || !_cache.TryGetResource<TextureResource>(wallpaper.Path, out var texture))
            return false;

        _id = wallpaper.ID;
        AddChild(new TextureRect
        {
            Texture = texture.Texture,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(320, 200),
            HorizontalAlignment = HAlignment.Left,
        });
        AddChild(new Label { Text = Loc.GetString(wallpaper.Name), StyleClasses = { "LabelKeyText" } });

        var set = new Button { Text = Loc.GetString("cmu-wallpaper-set"), HorizontalAlignment = HAlignment.Left };
        set.OnPressed += _ => Browser()?.Host?.SetWallpaper(_id);
        AddChild(set);
        control = this;
        return true;
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
