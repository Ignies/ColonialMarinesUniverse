using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A state ministry terminal, the UPP's or the Three World Empire's: a filled banner with the crest, the state's name
/// and its motto between marks, the programs as large directorate tabs under it with the session's buttons at the
/// end, heavy double framed windows, and a directive line along the bottom that cycles the OS's notices.
/// </summary>
public sealed class CMUMinistryShell(CMUComputerTheme theme) : CMUComputerShellStyle(theme)
{
    private Label? _directive;
    private ICMUComputerHost? _host;
    private float _time;

    /// <summary>How long each notice stays on the directive line.</summary>
    private const float NoticeSeconds = 7f;

    public override bool HasStartMenu => false;

    public override bool OpensOnSingleClick => true;

    public override Vector2 IconSize => new(104, 26);

    public override int LabelLength => 12;

    public override Container NewIconBox()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
    }

    public override void Build(BoxContainer desktop, CMUShellParts parts)
    {
        _host = parts.Host;
        var mark = Theme.Os.ShellMark;
        var banner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(10, 4),
            SeparationOverride = 12,
            HorizontalAlignment = Control.HAlignment.Center,
        };
        if (Logo(30, 40) is { } logo)
            banner.AddChild(logo);

        var words = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalAlignment = Control.VAlignment.Center };
        var title = Label(Text(Theme.Os.ShellTitle, Theme.DisplayName).ToUpperInvariant(), Theme.GetFont(CMUNetFont.Display, 14, true), Theme.Background);
        title.HorizontalAlignment = Control.HAlignment.Center;
        words.AddChild(title);
        if (Theme.Os.ShellSubtitle is { } subtitle)
        {
            var motto = Label($"{mark}  {Loc.GetString(subtitle)}  {mark}", Theme.ShellSmall, Theme.Background);
            motto.HorizontalAlignment = Control.HAlignment.Center;
            words.AddChild(motto);
        }

        banner.AddChild(words);

        var tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 4, 6, 0),
            SeparationOverride = 6,
        };
        tabs.AddChild(new ScrollContainer
        {
            VScrollEnabled = false,
            HorizontalExpand = true,
            Children = { parts.Icons },
        });
        foreach (var control in parts.SessionButtons())
        {
            control.HorizontalExpand = false;
            tabs.AddChild(control);
        }

        _directive = Label(string.Empty, Theme.ShellSmall, Theme.Bright);
        _directive.HorizontalExpand = true;
        _directive.ClipText = true;
        _directive.VerticalAlignment = Control.VAlignment.Center;
        parts.Tasks.HorizontalExpand = false;
        parts.Tasks.Margin = new Thickness(6, 0);
        parts.Clock.VerticalAlignment = Control.VAlignment.Center;
        parts.Clock.FontColorOverride = Theme.Bright;

        desktop.AddChild(new PanelContainer { PanelOverride = Theme.Box(Theme.Accent), Children = { banner } });
        desktop.AddChild(new CMUTerminalRule { Color = Theme.Bright });
        desktop.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.1f),
                BorderColor = Theme.Accent,
                BorderThickness = new Thickness(0, 0, 0, 2),
            },
            Children = { tabs },
        });
        desktop.AddChild(parts.Area);
        desktop.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.18f),
                BorderColor = Theme.Accent,
                BorderThickness = new Thickness(0, 2, 0, 0),
            },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(8, 3),
                    Children = { _directive, parts.Tasks, parts.Clock },
                },
            },
        });
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_directive == null)
            return;

        _time += frameTime;
        var notices = Theme.Os.ShellTicker;
        _directive.Text = notices.Count > 0
            ? Loc.GetString(notices[(int) (_time / NoticeSeconds) % notices.Count], ("id", _host?.TerminalId ?? 0))
            : Text(Theme.Os.ShellStatus, string.Empty, ("id", _host?.TerminalId ?? 0));
    }

    public override void DrawBackground(DrawingHandleScreen handle, Control desktop, Texture? wallpaper)
    {
        handle.DrawRect(desktop.PixelSizeBox, Theme.Background);
        if (wallpaper != null)
        {
            handle.DrawTextureRect(wallpaper, desktop.PixelSizeBox);
            return;
        }

        // Broad state stripes behind the work, faint, with the crest's colour along the bottom.
        var stripe = 48 * desktop.UIScale;
        for (var x = (float) -desktop.PixelHeight; x < desktop.PixelWidth; x += stripe * 2)
        {
            for (var t = 0f; t < stripe; t += 1)
                handle.DrawLine(new Vector2(x + t, desktop.PixelHeight), new Vector2(x + t + desktop.PixelHeight, 0), Theme.Accent.WithAlpha(0.012f));
        }

        DrawWatermark(handle, desktop, Text(Theme.Os.ShellTitle, Theme.DisplayName).ToUpperInvariant(), Theme.Accent.WithAlpha(0.16f));
    }

    public override StyleBox WindowFrame()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Theme.Background,
            BorderColor = Theme.Accent,
            BorderThickness = new Thickness(2),
            ContentMarginLeftOverride = 5,
            ContentMarginTopOverride = 5,
            ContentMarginRightOverride = 5,
            ContentMarginBottomOverride = 5,
        };
    }

    public override StyleBox TitleBar()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.22f),
            BorderColor = Theme.Accent,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    public override Color TitleColour => Theme.Bright;

    public override StyleBox TitleButtonBox()
    {
        return Theme.ButtonBox(Theme.Accent);
    }

    public override Color TitleButtonColour => Theme.Background;

    /// <summary>The second, inner frame in the bright colour.</summary>
    public override void DrawWindow(DrawingHandleScreen handle, Control window)
    {
        var inset = 3.5f * window.UIScale;
        handle.DrawRect(new UIBox2(inset, inset, window.PixelWidth - inset, window.PixelHeight - inset), Theme.Bright.WithAlpha(0.5f), false);
    }

    /// <summary>A directorate tab: filled in the state's colour when picked, its name centred.</summary>
    public override bool DrawIcon(DrawingHandleScreen handle, CMUDesktopIcon icon)
    {
        var scale = icon.UIScale;
        var lit = icon.Selected || icon.Hovered;
        var box = new UIBox2(0, 0, icon.PixelWidth, icon.PixelHeight);
        handle.DrawRect(box, lit ? Theme.Accent : Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.12f));
        handle.DrawRect(new UIBox2(0, 0, icon.PixelWidth, 2 * scale), Theme.Bright.WithAlpha(lit ? 0.9f : 0.35f));

        var picture = UIBox2.FromDimensions(new Vector2(5 * scale, (icon.PixelHeight - 16 * scale) / 2f), new Vector2(16, 16) * scale);
        DrawPicture(handle, icon, picture);
        var text = new UIBox2(picture.Right + 2 * scale, 0, icon.PixelWidth, icon.PixelHeight);
        DrawCentred(handle, Theme.ShellSmall, icon.Label, text, lit ? Theme.Background : Theme.Bright, scale);
        return true;
    }
}
