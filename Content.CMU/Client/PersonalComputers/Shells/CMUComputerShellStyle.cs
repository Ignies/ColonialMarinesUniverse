using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The pieces every desktop has, made by <see cref="CMUComputerDesktop"/> and arranged by a shell: the area the
/// windows open in, the program icons, the open programs' buttons, the clock and the session's own buttons.
/// </summary>
public sealed class CMUShellParts
{
    public required CMUComputerTheme Theme;

    /// <summary>Where program windows, the start menu and the trace warning go.</summary>
    public required LayoutContainer Area;

    /// <summary>The program and saved link icons, in the container the shell asked for.</summary>
    public required Container Icons;

    /// <summary>A button per open program.</summary>
    public required BoxContainer Tasks;

    public required Label Clock;

    /// <summary>Log off, when the terminal takes cards, then shut down.</summary>
    public required Func<List<Control>> SessionButtons;

    public required Action ToggleStartMenu;

    /// <summary>Plays the OS's click.</summary>
    public required Action Clicked;

    public ICMUComputerHost? Host;
}

/// <summary>
/// How an OS lays out its desktop, frames its windows and draws its icons, for the shells added after the first
/// three: one per <see cref="CMUComputerShell"/>, each in its own file, asked by the desktop, its windows and icons. Whatever the
/// shell, the same icons open the same programs in the same windows. A shell keeps no state of its own beyond what
/// it builds into the desktop, so one is made per theme.
/// </summary>
public abstract class CMUComputerShellStyle
{
    protected readonly CMUComputerTheme Theme;

    protected CMUComputerShellStyle(CMUComputerTheme theme)
    {
        Theme = theme;
    }

    /// <summary>The shell's style, or null for the classic, Seegson and Weyland desktops, which the desktop lays out itself.</summary>
    public static CMUComputerShellStyle? Create(CMUComputerTheme theme)
    {
        return theme.Shell switch
        {
            CMUComputerShell.Tactical => new CMUTacticalShell(theme),
            CMUComputerShell.Ministry => new CMUMinistryShell(theme),
            CMUComputerShell.Clinical => new CMUClinicalShell(theme),
            CMUComputerShell.Police => new CMUPoliceShell(theme),
            _ => null,
        };
    }

    // Desktop

    /// <summary>The container the icons go in: a grid on the wallpaper, a menu, a dock, a tab strip.</summary>
    public virtual Container NewIconBox()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    }

    /// <summary>Adds the shell's header, menus, bars and the window area to the desktop, top to bottom.</summary>
    public abstract void Build(BoxContainer desktop, CMUShellParts parts);

    /// <summary>Per frame, for what the shell animates: a blinking cursor, a ticker, a glitch.</summary>
    public virtual void FrameUpdate(float frameTime)
    {
    }

    /// <summary>Whether the shell has a start menu; without one, its menu is always on screen.</summary>
    public virtual bool HasStartMenu => true;

    public virtual string StartMenuHeading => Theme.DisplayName;

    /// <summary>How far from the left of the window area the first program window opens.</summary>
    public virtual float WindowOffset => 14;

    /// <summary>
    /// The desktop's background under the windows: the wallpaper when there is one, else the shell's own. The
    /// default is a faint grid with the OS name in a corner, like an idle workstation.
    /// </summary>
    public virtual void DrawBackground(DrawingHandleScreen handle, Control desktop, Texture? wallpaper)
    {
        handle.DrawRect(desktop.PixelSizeBox, Theme.Background);
        if (wallpaper != null)
        {
            handle.DrawTextureRect(wallpaper, desktop.PixelSizeBox);
            return;
        }

        DrawGrid(handle, desktop, Theme.Phosphor.WithAlpha(0.05f), 32);
        DrawWatermark(handle, desktop, Theme.DisplayName, Theme.Phosphor.WithAlpha(0.18f));
    }

    // Lettering

    /// <summary>Labels and titles as the shell letters them: in capitals, unless it is the classic desktop.</summary>
    public virtual string Letter(string text)
    {
        return text.ToUpperInvariant();
    }

    /// <summary>A button in the shell's lettering.</summary>
    public virtual Button Button(string text)
    {
        var button = Theme.Button(Letter(text));
        button.Label.FontOverride = Theme.ShellFont;
        return button;
    }

    /// <summary>A label in the shell's lettering and colour.</summary>
    public Label Label(string text, Font? font = null, Color? colour = null)
    {
        return Theme.Label(text, font ?? Theme.ShellFont, colour ?? Theme.Phosphor);
    }

    // Windows

    /// <summary>The box round a program window, with room inside for anything <see cref="DrawWindow"/> adds.</summary>
    public virtual StyleBox WindowFrame()
    {
        return Theme.Box(Theme.Background, Theme.Accent, 2);
    }

    public virtual StyleBox TitleBar()
    {
        return Theme.Box(Theme.Accent);
    }

    public virtual Color TitleColour => Theme.Background;

    public virtual Font TitleFont => Theme.ShellFont;

    /// <summary>The minimize and close buttons' text, from "_" and "X".</summary>
    public virtual string TitleButtonText(string glyph)
    {
        return glyph;
    }

    public virtual StyleBox TitleButtonBox()
    {
        return Theme.ButtonBox(Theme.Background);
    }

    public virtual Color TitleButtonColour => Theme.Accent;

    /// <summary>Drawn over a window's own frame, under its contents: inner rules, corner marks, stripes.</summary>
    public virtual void DrawWindow(DrawingHandleScreen handle, Control window)
    {
    }

    // Icons

    public virtual Vector2 IconSize => new(84, 70);

    /// <summary>Menus open on one click; desktop icons on a double click.</summary>
    public virtual bool OpensOnSingleClick => false;

    /// <summary>Longest label before it is cut.</summary>
    public virtual int LabelLength => 13;

    /// <summary>Draws an icon the shell's way; false leaves it to the desktop icon's own drawing.</summary>
    public virtual bool DrawIcon(DrawingHandleScreen handle, CMUDesktopIcon icon)
    {
        return false;
    }

    // Helpers for the shells

    protected static void DrawGrid(DrawingHandleScreen handle, Control control, Color colour, float spacing)
    {
        var step = spacing * control.UIScale;
        for (var x = 0f; x < control.PixelWidth; x += step)
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, control.PixelHeight), colour);
        for (var y = 0f; y < control.PixelHeight; y += step)
            handle.DrawLine(new Vector2(0, y), new Vector2(control.PixelWidth, y), colour);
    }

    /// <summary>The OS name large in the bottom right corner, as big as fits in 70% of the width.</summary>
    protected void DrawWatermark(DrawingHandleScreen handle, Control control, string text, Color colour, CMUNetFont face = CMUNetFont.Display)
    {
        var scale = control.UIScale;
        Font font = default!;
        var width = 0f;
        foreach (var size in new[] { 22, 18, 15, 12 })
        {
            font = Theme.GetFont(face, size, true);
            width = Width(font, text, scale);
            if (width <= control.PixelWidth * 0.7f)
                break;
        }

        handle.DrawString(font, new Vector2(control.PixelWidth - width - 16 * scale, control.PixelHeight - 80 * scale), text, scale, colour);
    }

    public static float Width(Font font, string text, float scale)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, scale, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }

    /// <summary>A rectangle's outline with its corners cut, the nearest a line drawing gets to rounded corners.</summary>
    protected static void DrawCutBox(DrawingHandleScreen handle, UIBox2 box, float cut, Color line, Color? fill = null)
    {
        if (fill is { } f)
        {
            handle.DrawRect(new UIBox2(box.Left + cut, box.Top, box.Right - cut, box.Bottom), f);
            handle.DrawRect(new UIBox2(box.Left, box.Top + cut, box.Left + cut, box.Bottom - cut), f);
            handle.DrawRect(new UIBox2(box.Right - cut, box.Top + cut, box.Right, box.Bottom - cut), f);
        }

        handle.DrawLine(new Vector2(box.Left + cut, box.Top), new Vector2(box.Right - cut, box.Top), line);
        handle.DrawLine(new Vector2(box.Left + cut, box.Bottom), new Vector2(box.Right - cut, box.Bottom), line);
        handle.DrawLine(new Vector2(box.Left, box.Top + cut), new Vector2(box.Left, box.Bottom - cut), line);
        handle.DrawLine(new Vector2(box.Right, box.Top + cut), new Vector2(box.Right, box.Bottom - cut), line);
        handle.DrawLine(new Vector2(box.Left, box.Top + cut), new Vector2(box.Left + cut, box.Top), line);
        handle.DrawLine(new Vector2(box.Right - cut, box.Top), new Vector2(box.Right, box.Top + cut), line);
        handle.DrawLine(new Vector2(box.Left, box.Bottom - cut), new Vector2(box.Left + cut, box.Bottom), line);
        handle.DrawLine(new Vector2(box.Right - cut, box.Bottom), new Vector2(box.Right, box.Bottom - cut), line);
    }

    /// <summary>The icon's program picture centred in a box: its texture, else its phosphor glyph.</summary>
    protected static void DrawPicture(DrawingHandleScreen handle, CMUDesktopIcon icon, UIBox2 box)
    {
        if (icon.Texture != null)
            handle.DrawTextureRect(icon.Texture, box);
        else
            icon.DrawGlyph(handle, box, icon.UIScale);
    }

    /// <summary>Text centred in a box, horizontally and vertically.</summary>
    protected static void DrawCentred(DrawingHandleScreen handle, Font font, string text, UIBox2 box, Color colour, float scale)
    {
        var width = Width(font, text, scale);
        var pos = new Vector2(MathF.Round(box.Center.X - width / 2f), MathF.Round(box.Center.Y - font.GetLineHeight(scale) / 2f));
        handle.DrawString(font, pos, text, scale, colour);
    }

    /// <summary>The header's logo, sized to the bar; null when the OS has none.</summary>
    protected TextureRect? Logo(float height, float minWidth = 40)
    {
        if (Theme.Logo is not { } logo)
            return null;

        return new TextureRect
        {
            Texture = logo,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MinSize = new Vector2(minWidth, height / 2f),
            MaxHeight = height,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    /// <summary>A localized string from the OS, or the fallback when the OS leaves it unset.</summary>
    protected string Text(LocId? id, string fallback, params (string, object)[] args)
    {
        return id is { } loc ? Loc.GetString(loc, args) : fallback;
    }
}
