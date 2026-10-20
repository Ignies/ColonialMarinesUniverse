using Content.Client.Resources;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Colours, fonts and widget styling of one OS, shared by the desktop, its windows and the browser.
/// </summary>
public sealed class CMUComputerTheme
{
    public readonly CMUComputerOsPrototype Os;
    public readonly Color Phosphor;
    public readonly Color Background;
    public readonly Color Accent;
    public readonly Color Dim;
    public readonly Font Font;
    public readonly Font Bold;
    public readonly Font Small;

    /// <summary>The desktop layout, from the OS.</summary>
    public readonly CMUComputerShell Shell;

    /// <summary>The shell's own lettering: menus, headers and window titles.</summary>
    public readonly Font ShellFont;

    public readonly Font ShellSmall;

    /// <summary>A colour between the phosphor and white, for what the shell lifts out: headers and highlights.</summary>
    public readonly Color Bright;

    /// <summary>How the newer shells lay out the desktop; null for the classic, Seegson and Weyland desktops.</summary>
    public readonly CMUComputerShellStyle? Style;

    /// <summary>The OS name as shown: the product name, or the colony's network name for OSes named after it.</summary>
    public string DisplayName;

    private static readonly ResPath IconRoot = new("/Textures/CMU14/PersonalComputers/Icons");

    private readonly IResourceCache _cache;
    private readonly Dictionary<CMUComputerApp, Texture?> _icons = new();

    public CMUComputerTheme(CMUComputerOsPrototype os, IResourceCache cache)
    {
        Os = os;
        _cache = cache;
        DisplayName = Loc.GetString(os.Name);
        Phosphor = os.Phosphor;
        Background = os.Background;
        Accent = os.Accent ?? os.Phosphor;
        Dim = Color.InterpolateBetween(os.Background, os.Phosphor, 0.45f);
        Font = GetFont(CMUNetFont.Mono, 12);
        Bold = GetFont(CMUNetFont.Mono, 12, true);
        Small = GetFont(CMUNetFont.Mono, 10);
        Shell = os.Shell;
        Bright = Color.InterpolateBetween(Phosphor, Color.White, 0.55f);
        (ShellFont, ShellSmall) = Shell switch
        {
            // Sevastolink's chunky, teletype lettering: a bitmap face, which keeps to its measured width.
            CMUComputerShell.Seegson => (GetFont(CMUNetFont.Pixel, 12, true), GetFont(CMUNetFont.Pixel, 11)),
            // The Company's clean corporate face.
            CMUComputerShell.Weyland => (GetFont(CMUNetFont.Display, 12, true), GetFont(CMUNetFont.Display, 10)),
            // Military readouts in a stencil-plain mono.
            CMUComputerShell.Tactical => (GetFont(CMUNetFont.Mono, 11, true), GetFont(CMUNetFont.Mono, 10)),
            // Official, formal lettering.
            CMUComputerShell.Ministry => (GetFont(CMUNetFont.Display, 12, true), GetFont(CMUNetFont.Display, 10)),
            // Clean product lettering.
            CMUComputerShell.Clinical => (GetFont(CMUNetFont.Sans, 11, true), GetFont(CMUNetFont.Sans, 10)),
            CMUComputerShell.Police => (GetFont(CMUNetFont.Display, 11, true), GetFont(CMUNetFont.Display, 10)),
            _ => (Bold, Small),
        };
        Style = CMUComputerShellStyle.Create(this);
    }

    /// <summary>The OS's first logo image, for the shell's header; null when it has none.</summary>
    public Texture? Logo
    {
        get
        {
            foreach (var logo in Os.Logos)
            {
                if (logo != CMUComputerOsPrototype.NameLogo && TryGetTexture(new ResPath(logo), out var texture))
                    return texture;
            }

            return null;
        }
    }

    /// <summary>The look a page gets when neither the site nor the page sets one.</summary>
    public CMUNetPageStyle DefaultPageStyle => new()
    {
        Background = Background,
        Text = Color.InterpolateBetween(Phosphor, Color.White, 0.15f),
        Heading = Color.InterpolateBetween(Phosphor, Color.White, 0.35f),
        Link = Phosphor,
        Font = CMUNetFont.Sans,
        BackgroundMode = CMUNetBackgroundMode.Stretch,
    };

    public Font GetFont(CMUNetFont font, int size, bool bold = false)
    {
        var path = (font, bold) switch
        {
            (CMUNetFont.Terminal, _) => "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf",
            (CMUNetFont.Mono, false) => "/Fonts/RobotoMono/RobotoMono-Regular.ttf",
            (CMUNetFont.Mono, true) => "/Fonts/RobotoMono/RobotoMono-Bold.ttf",
            (CMUNetFont.Display, false) => "/Fonts/NotoSansDisplay/NotoSansDisplay-Regular.ttf",
            (CMUNetFont.Display, true) => "/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf",
            (CMUNetFont.Pixel, false) => "/Fonts/Cozette/CozetteVector.ttf",
            (CMUNetFont.Pixel, true) => "/Fonts/Cozette/CozetteVectorBold.ttf",
            (_, true) => "/Fonts/NotoSans/NotoSans-Bold.ttf",
            _ => "/Fonts/NotoSans/NotoSans-Regular.ttf",
        };

        return _cache.GetFont(path, size);
    }

    public bool TryGetTexture(ResPath? path, out Texture texture)
    {
        texture = default!;
        // Checked first so a logo or icon that has not been drawn yet is not logged as a missing resource.
        if (path is not { } p
            || p.Extension != "png"
            || !_cache.ContentFileExists(p)
            || !_cache.TryGetResource<TextureResource>(p, out var resource))
            return false;

        texture = resource.Texture;
        return true;
    }

    public bool TryGetRsiState(ResPath? path, string state, out RSI.State rsiState)
    {
        rsiState = default!;
        return path is { } p
               && p.Extension == "rsi"
               && _cache.ContentFileExists(p / "meta.json")
               && _cache.TryGetResource<RSIResource>(p, out var rsi)
               && rsi.RSI.TryGetState(state, out rsiState!);
    }

    public StyleBoxFlat Box(Color background, Color? border = null, float thickness = 1)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border ?? Color.Transparent,
            BorderThickness = new Thickness(border == null ? 0 : thickness),
        };
    }

    /// <summary>A button face with room round its label, so filled buttons do not crowd their text.</summary>
    public StyleBoxFlat ButtonBox(Color background, Color? border = null)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border ?? Color.Transparent,
            BorderThickness = new Thickness(border == null ? 0 : 1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
    }

    /// <summary>A flat terminal button with a phosphor outline.</summary>
    public Button Button(string text, string? tooltip = null)
    {
        var button = new Button { Text = text, ToolTip = tooltip, MinWidth = 26 };
        button.Label.FontOverride = Bold;
        button.Label.FontColorOverride = Phosphor;
        button.StyleBoxOverride = ButtonBox(Background, Phosphor.WithAlpha(0.8f));
        // The UI stylesheet tints button faces per state; left alone it darkens filled buttons until their
        // labels cannot be read, so the face keeps its colour and hover dims it slightly instead.
        button.ModulateSelfOverride = Color.White;
        button.OnMouseEntered += _ => button.ModulateSelfOverride = new Color(0.82f, 0.82f, 0.82f);
        button.OnMouseExited += _ => button.ModulateSelfOverride = Color.White;
        return button;
    }

    /// <summary>
    /// The program's 16x16 icon: <c>Icons/&lt;os id&gt;_&lt;program&gt;.png</c>, else <c>Icons/&lt;program&gt;.png</c>.
    /// </summary>
    public Texture? AppIcon(CMUComputerApp app)
    {
        if (_icons.TryGetValue(app, out var cached))
            return cached;

        var name = app.ToString().ToLowerInvariant();
        Texture? icon = null;
        if (TryGetTexture(IconRoot / $"{Os.ID.ToLowerInvariant()}_{name}.png", out var specific))
            icon = specific;
        else if (TryGetTexture(IconRoot / $"{name}.png", out var generic))
            icon = generic;

        _icons[app] = icon;
        return icon;
    }

    /// <summary>A button with the program's icon before its label, for the start menu and taskbar.</summary>
    public Button AppButton(CMUComputerApp app, string text)
    {
        var button = Button(text);
        button.RemoveChild(button.Label);
        button.Label.ClipText = true;
        button.Label.HorizontalExpand = true;
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 5 };
        if (AppIcon(app) is { } icon)
            row.AddChild(new TextureRect { Texture = icon, Stretch = TextureRect.StretchMode.KeepCentered, MinSize = new(16, 16) });

        row.AddChild(button.Label);
        button.AddChild(row);
        return button;
    }

    public Label Label(string text, Font? font = null, Color? color = null)
    {
        return new Label { Text = text, FontOverride = font ?? Font, FontColorOverride = color ?? Phosphor };
    }
}
