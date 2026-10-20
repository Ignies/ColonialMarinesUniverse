using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A military tactical terminal, like the screens aboard a Colonial Marines ship: the crest and the force across the
/// top, the programs on function keys under it, a status column down the right with the terminal, the user and the
/// network, bracket-cornered windows, and a command prompt along the bottom.
/// </summary>
public sealed class CMUTacticalShell(CMUComputerTheme theme) : CMUComputerShellStyle(theme)
{
    private Label? _prompt;
    private Label? _network;
    private ICMUComputerHost? _host;
    private float _blink;

    public override bool HasStartMenu => false;

    public override bool OpensOnSingleClick => true;

    public override Vector2 IconSize => new(118, 22);

    public override int LabelLength => 11;

    public override Container NewIconBox()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
    }

    public override void Build(BoxContainer desktop, CMUShellParts parts)
    {
        _host = parts.Host;
        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 4),
            SeparationOverride = 10,
        };
        if (Logo(28, 48) is { } logo)
            header.AddChild(logo);

        var force = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalAlignment = Control.VAlignment.Center };
        force.AddChild(Label(Text(Theme.Os.ShellTitle, Theme.DisplayName.ToUpperInvariant()), Theme.ShellFont, Theme.Bright));
        force.AddChild(Label(Text(Theme.Os.ShellSubtitle, string.Empty), Theme.ShellSmall, Theme.Dim));
        header.AddChild(force);

        parts.Clock.VerticalAlignment = Control.VAlignment.Center;
        parts.Clock.FontColorOverride = Theme.Bright;
        parts.Clock.FontOverride = Theme.GetFont(CMUNetFont.Mono, 16, true);
        header.AddChild(parts.Clock);

        var keys = new ScrollContainer
        {
            VScrollEnabled = false,
            Margin = new Thickness(6, 3),
            Children = { parts.Icons },
        };

        var status = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 3,
        };
        status.AddChild(Label(Loc.GetString("cmu-os-shell-status"), Theme.ShellFont, Theme.Accent));
        status.AddChild(new CMUTerminalRule { Color = Theme.Accent.WithAlpha(0.6f), Margin = new Thickness(0, 0, 0, 3) });
        if (_host != null)
        {
            status.AddChild(Field(Loc.GetString("cmu-os-shell-field-terminal"), $"#{_host.TerminalId}"));
            status.AddChild(Field(Loc.GetString("cmu-os-shell-field-user"), (_host.SessionUser ?? "-").ToUpperInvariant()));
            status.AddChild(Field(Loc.GetString("cmu-os-shell-field-role"), (_host.SessionJob ?? "-").ToUpperInvariant()));
        }

        status.AddChild(Label(Loc.GetString("cmu-os-shell-field-network"), Theme.ShellSmall, Theme.Dim));
        _network = Label(string.Empty, Theme.ShellFont);
        status.AddChild(_network);
        status.AddChild(new Control { VerticalExpand = true });
        foreach (var control in parts.SessionButtons())
            status.AddChild(control);

        var column = new PanelContainer
        {
            MinWidth = 150,
            MaxWidth = 150,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.05f),
                BorderColor = Theme.Accent.WithAlpha(0.6f),
                BorderThickness = new Thickness(1, 0, 0, 0),
            },
            Children = { status },
        };

        _prompt = Label(string.Empty, Theme.ShellFont, Theme.Accent);
        _prompt.VerticalAlignment = Control.VAlignment.Center;
        _prompt.MinWidth = 260;
        parts.Tasks.Margin = new Thickness(6, 0, 0, 0);

        desktop.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.08f),
                BorderColor = Theme.Accent,
                BorderThickness = new Thickness(0, 0, 0, 1),
            },
            Children = { header },
        });
        desktop.AddChild(new PanelContainer { PanelOverride = Theme.Box(Theme.Background), Children = { keys } });
        desktop.AddChild(new CMUTerminalRule { Color = Theme.Accent.WithAlpha(0.6f) });
        desktop.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            Children = { parts.Area, column },
        });
        desktop.AddChild(new CMUTerminalRule { Color = Theme.Accent });
        desktop.AddChild(new PanelContainer
        {
            PanelOverride = Theme.Box(Theme.Background),
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(6, 3),
                    Children = { _prompt, parts.Tasks },
                },
            },
        });
    }

    /// <summary>A status field: its name small, its value under it.</summary>
    private Control Field(string name, string value)
    {
        var field = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        field.AddChild(Label(name, Theme.ShellSmall, Theme.Dim));
        var shown = Label(value, Theme.ShellFont);
        shown.ClipText = true;
        field.AddChild(shown);
        return field;
    }

    public override void FrameUpdate(float frameTime)
    {
        _blink += frameTime;
        if (_prompt != null)
        {
            var line = Text(Theme.Os.ShellStatus, string.Empty, ("id", _host?.TerminalId ?? 0));
            _prompt.Text = $"> {line}{(_blink % 1f < 0.55f ? "_" : " ")}";
        }

        if (_network != null)
        {
            var carrier = _host?.Carrier ?? true;
            _network.Text = Loc.GetString(carrier ? "cmu-os-shell-network-up" : "cmu-os-shell-network-down");
            _network.FontColorOverride = carrier ? Theme.Phosphor : Color.FromHex("#FF5040");
        }
    }

    public override void DrawBackground(DrawingHandleScreen handle, Control desktop, Texture? wallpaper)
    {
        handle.DrawRect(desktop.PixelSizeBox, Theme.Background);
        if (wallpaper != null)
        {
            handle.DrawTextureRect(wallpaper, desktop.PixelSizeBox);
            return;
        }

        // A plotting grid, finer than the colony's, with a heavier line every fourth.
        DrawGrid(handle, desktop, Theme.Accent.WithAlpha(0.035f), 16);
        DrawGrid(handle, desktop, Theme.Accent.WithAlpha(0.06f), 64);
        DrawWatermark(handle, desktop, Text(Theme.Os.ShellTitle, Theme.DisplayName).ToUpperInvariant(), Theme.Accent.WithAlpha(0.14f), CMUNetFont.Mono);
    }

    public override StyleBox WindowFrame()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Theme.Background,
            BorderColor = Theme.Accent.WithAlpha(0.45f),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 3,
            ContentMarginTopOverride = 3,
            ContentMarginRightOverride = 3,
            ContentMarginBottomOverride = 3,
        };
    }

    public override StyleBox TitleBar()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.12f),
            BorderColor = Theme.Accent.WithAlpha(0.6f),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    public override Color TitleColour => Theme.Bright;

    public override StyleBox TitleButtonBox()
    {
        return Theme.ButtonBox(Theme.Background, Theme.Accent.WithAlpha(0.7f));
    }

    /// <summary>Targeting brackets on the window's corners.</summary>
    public override void DrawWindow(DrawingHandleScreen handle, Control window)
    {
        var scale = window.UIScale;
        var arm = 12 * scale;
        var w = window.PixelWidth - 1;
        var h = window.PixelHeight - 1;
        var c = Theme.Accent;
        foreach (var (x, y, dx, dy) in new[] { (0f, 0f, 1f, 1f), (w, 0f, -1f, 1f), (0f, h, 1f, -1f), (w, h, -1f, -1f) })
        {
            for (var t = 0; t < 2; t++)
            {
                handle.DrawLine(new Vector2(x, y + dy * t), new Vector2(x + dx * arm, y + dy * t), c);
                handle.DrawLine(new Vector2(x + dx * t, y), new Vector2(x + dx * t, y + dy * arm), c);
            }
        }
    }

    /// <summary>A function key: its number in a cap, its program after it.</summary>
    public override bool DrawIcon(DrawingHandleScreen handle, CMUDesktopIcon icon)
    {
        var scale = icon.UIScale;
        var lit = icon.Selected || icon.Hovered;
        var box = new UIBox2(0, 0, icon.PixelWidth, icon.PixelHeight);
        handle.DrawRect(box, lit ? Theme.Accent.WithAlpha(0.3f) : Theme.Accent.WithAlpha(0.06f));
        handle.DrawRect(box, Theme.Accent.WithAlpha(lit ? 1f : 0.5f), false);

        var number = (icon.Parent == null ? 0 : icon.GetPositionInParent()) + 1;
        var cap = new UIBox2(2 * scale, 2 * scale, 26 * scale, icon.PixelHeight - 2 * scale);
        handle.DrawRect(cap, Theme.Accent);
        DrawCentred(handle, Theme.ShellSmall, $"F{number}", cap, Theme.Background, scale);

        var font = Theme.ShellSmall;
        var y = MathF.Round((icon.PixelHeight - font.GetLineHeight(scale)) / 2f);
        handle.DrawString(font, new Vector2(cap.Right + 5 * scale, y), icon.Label, scale, lit ? Theme.Bright : Theme.Phosphor);
        return true;
    }
}
