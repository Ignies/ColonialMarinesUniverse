using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A police case management system, colony security's, the Rangers', NSPA's or the Marshals': the badge and the
/// agency across the top over a police checker, case file navigation down the left with the session's buttons
/// under it, windows with the agency's stripe down their side, and a ticker of notices running along the bottom.
/// </summary>
public sealed class CMUPoliceShell(CMUComputerTheme theme) : CMUComputerShellStyle(theme)
{
    public override bool HasStartMenu => false;

    public override bool OpensOnSingleClick => true;

    public override Vector2 IconSize => new(146, 24);

    public override int LabelLength => 15;

    public override Container NewIconBox()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    }

    public override void Build(BoxContainer desktop, CMUShellParts parts)
    {
        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 4),
            SeparationOverride = 10,
        };
        if (Logo(30, 48) is { } logo)
            header.AddChild(logo);

        var agency = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalAlignment = Control.VAlignment.Center };
        agency.AddChild(Label(Text(Theme.Os.ShellTitle, Theme.DisplayName).ToUpperInvariant(), Theme.ShellFont, Theme.Bright));
        agency.AddChild(Label(Text(Theme.Os.ShellSubtitle, string.Empty), Theme.ShellSmall, Theme.Dim));
        header.AddChild(agency);

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalAlignment = Control.VAlignment.Center };
        parts.Clock.HorizontalAlignment = Control.HAlignment.Right;
        parts.Clock.FontColorOverride = Theme.Bright;
        right.AddChild(parts.Clock);
        if (parts.Host != null)
        {
            var terminal = Label(Loc.GetString("cmu-os-shell-terminal", ("id", parts.Host.TerminalId)), Theme.ShellSmall, Theme.Dim);
            terminal.HorizontalAlignment = Control.HAlignment.Right;
            right.AddChild(terminal);
        }

        header.AddChild(right);

        var nav = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(4, 6), SeparationOverride = 3 };
        nav.AddChild(Label(Loc.GetString("cmu-os-shell-case-files"), Theme.ShellSmall, Theme.Dim));
        nav.AddChild(new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { parts.Icons } });
        nav.AddChild(new CMUTerminalRule { Color = Theme.Accent.WithAlpha(0.5f), Margin = new Thickness(0, 2) });
        if (parts.Host?.SessionUser is { } user)
        {
            var who = Label(Loc.GetString("cmu-os-shell-officer", ("name", user.ToUpperInvariant())), Theme.ShellSmall, Theme.Dim);
            who.ClipText = true;
            nav.AddChild(who);
        }

        foreach (var control in parts.SessionButtons())
            nav.AddChild(control);

        var column = new PanelContainer
        {
            MinWidth = 156,
            MaxWidth = 156,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.07f),
                BorderColor = Theme.Accent.WithAlpha(0.6f),
                BorderThickness = new Thickness(0, 0, 1, 0),
            },
            Children = { nav },
        };

        var notices = new List<string>();
        foreach (var notice in Theme.Os.ShellTicker)
            notices.Add(Loc.GetString(notice, ("id", parts.Host?.TerminalId ?? 0)));

        parts.Tasks.Margin = new Thickness(6, 2);
        desktop.AddChild(new PanelContainer
        {
            PanelOverride = Theme.Box(Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.16f)),
            Children = { header },
        });
        desktop.AddChild(new CMUPoliceChecker(Theme.Accent, Theme.Bright));
        desktop.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            Children = { column, parts.Area },
        });
        desktop.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Theme.Background,
                BorderColor = Theme.Accent.WithAlpha(0.6f),
                BorderThickness = new Thickness(0, 1, 0, 0),
            },
            Children = { parts.Tasks },
        });
        desktop.AddChild(new CMUPoliceTicker(Theme, string.Join("     ///     ", notices)));
    }

    public override StyleBox WindowFrame()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Theme.Background,
            BorderColor = Theme.Accent,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginTopOverride = 1,
            ContentMarginRightOverride = 1,
            ContentMarginBottomOverride = 1,
        };
    }

    public override StyleBox TitleBar()
    {
        return Theme.Box(Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.25f));
    }

    public override Color TitleColour => Theme.Bright;

    public override StyleBox TitleButtonBox()
    {
        return Theme.ButtonBox(Theme.Background, Theme.Accent.WithAlpha(0.7f));
    }

    /// <summary>The agency's stripe down the window's left side.</summary>
    public override void DrawWindow(DrawingHandleScreen handle, Control window)
    {
        handle.DrawRect(new UIBox2(0, 0, 5 * window.UIScale, window.PixelHeight), Theme.Accent);
    }

    /// <summary>A case file row: the program's icon, its name, and a bar on the left when picked.</summary>
    public override bool DrawIcon(DrawingHandleScreen handle, CMUDesktopIcon icon)
    {
        var scale = icon.UIScale;
        var lit = icon.Selected || icon.Hovered;
        if (lit)
        {
            handle.DrawRect(new UIBox2(0, 0, icon.PixelWidth, icon.PixelHeight), Theme.Accent.WithAlpha(0.22f));
            handle.DrawRect(new UIBox2(0, 0, 3 * scale, icon.PixelHeight), Theme.Accent);
        }

        var picture = UIBox2.FromDimensions(new Vector2(7 * scale, (icon.PixelHeight - 16 * scale) / 2f), new Vector2(16, 16) * scale);
        DrawPicture(handle, icon, picture);
        var font = Theme.ShellSmall;
        var y = MathF.Round((icon.PixelHeight - font.GetLineHeight(scale)) / 2f);
        handle.DrawString(font, new Vector2(picture.Right + 6 * scale, y), icon.Label, scale, lit ? Theme.Bright : Theme.Phosphor);
        return true;
    }
}

/// <summary>The police checker: a band of squares in two colours under the header.</summary>
public sealed class CMUPoliceChecker : Control
{
    private readonly Color _a;
    private readonly Color _b;

    public CMUPoliceChecker(Color a, Color b)
    {
        _a = a;
        _b = b;
        MinHeight = 8;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelHeight / 2f;
        var column = 0;
        for (var x = 0f; x < PixelWidth; x += size, column++)
        {
            handle.DrawRect(new UIBox2(x, 0, x + size, size), column % 2 == 0 ? _a : _b);
            handle.DrawRect(new UIBox2(x, size, x + size, PixelHeight), column % 2 == 0 ? _b : _a);
        }
    }
}

/// <summary>Notices running right to left along the bottom of the screen, round and round.</summary>
public sealed class CMUPoliceTicker : Control
{
    private const float Speed = 40;

    private readonly CMUComputerTheme _theme;
    private readonly string _text;
    private float _time;

    public CMUPoliceTicker(CMUComputerTheme theme, string text)
    {
        _theme = theme;
        _text = text;
        MinHeight = 18;
        RectClipContent = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, _theme.Accent);
        if (_text.Length == 0)
            return;

        var scale = UIScale;
        var font = _theme.ShellSmall;
        var width = CMUComputerShellStyle.Width(font, _text, scale);
        var run = PixelWidth + width;
        var x = PixelWidth - _time * Speed * scale % run;
        var y = MathF.Round((PixelHeight - font.GetLineHeight(scale)) / 2f);
        handle.DrawString(font, new Vector2(x, y), _text, scale, _theme.Background);
    }
}
