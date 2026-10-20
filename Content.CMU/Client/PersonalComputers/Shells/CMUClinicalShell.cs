using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A clean corporate lab interface, Hyperdyne's, Prodigy's or LaSalle's: a slim bar along the top with the logo, the
/// open programs and the clock, a soft gradient behind the work, and a floating dock of round program buttons at
/// the bottom with the menu at its end. Windows are flat and quiet with a hairline under the title. Lettering keeps
/// its case: this is a product, not a terminal.
/// </summary>
public sealed class CMUClinicalShell(CMUComputerTheme theme) : CMUComputerShellStyle(theme)
{
    public override bool OpensOnSingleClick => true;

    public override Vector2 IconSize => new(66, 60);

    public override int LabelLength => 10;

    public override float WindowOffset => 40;

    public override string Letter(string text)
    {
        return text;
    }

    public override Container NewIconBox()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
    }

    public override void Build(BoxContainer desktop, CMUShellParts parts)
    {
        var bar = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(10, 3),
            SeparationOverride = 10,
        };
        if (Logo(18, 40) is { } logo)
            bar.AddChild(logo);

        var product = Label(Text(Theme.Os.ShellTitle, Theme.DisplayName), Theme.ShellFont, Theme.Phosphor);
        product.VerticalAlignment = Control.VAlignment.Center;
        bar.AddChild(product);
        parts.Tasks.Margin = new Thickness(10, 0);
        bar.AddChild(parts.Tasks);
        parts.Clock.VerticalAlignment = Control.VAlignment.Center;
        parts.Clock.FontColorOverride = Theme.Phosphor;
        bar.AddChild(parts.Clock);

        var menu = Button(Loc.GetString("cmu-os-shell-menu"));
        menu.StyleBoxOverride = Theme.ButtonBox(Theme.Accent);
        menu.Label.FontColorOverride = Theme.Background;
        menu.VerticalAlignment = Control.VAlignment.Center;
        menu.OnPressed += _ =>
        {
            parts.Clicked();
            parts.ToggleStartMenu();
        };

        var dock = new PanelContainer
        {
            HorizontalAlignment = Control.HAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Phosphor, 0.07f),
                BorderColor = Theme.Accent.WithAlpha(0.35f),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 10,
                    Children =
                    {
                        new ScrollContainer { VScrollEnabled = false, MaxWidth = 560, Children = { parts.Icons } },
                        menu,
                    },
                },
            },
        };

        desktop.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Phosphor, 0.06f),
                BorderColor = Theme.Accent.WithAlpha(0.3f),
                BorderThickness = new Thickness(0, 0, 0, 1),
            },
            Children = { bar },
        });
        desktop.AddChild(parts.Area);
        desktop.AddChild(new Control { MinHeight = 4 });
        desktop.AddChild(dock);
        desktop.AddChild(new Control { MinHeight = 6 });
    }

    public override void DrawBackground(DrawingHandleScreen handle, Control desktop, Texture? wallpaper)
    {
        if (wallpaper != null)
        {
            handle.DrawTextureRect(wallpaper, desktop.PixelSizeBox);
            return;
        }

        // A soft gradient, lighter at the top, in bands too fine to see.
        const int bands = 32;
        var top = Color.InterpolateBetween(Theme.Background, Theme.Phosphor, 0.06f);
        var bottom = Color.InterpolateBetween(Theme.Background, Theme.Accent, 0.1f);
        for (var i = 0; i < bands; i++)
        {
            var y0 = desktop.PixelHeight * i / bands;
            var y1 = desktop.PixelHeight * (i + 1) / bands;
            handle.DrawRect(new UIBox2(0, y0, desktop.PixelWidth, y1 + 1), Color.InterpolateBetween(top, bottom, i / (float) (bands - 1)));
        }

        DrawWatermark(handle, desktop, Text(Theme.Os.ShellTitle, Theme.DisplayName), Theme.Phosphor.WithAlpha(0.1f), CMUNetFont.Sans);
    }

    public override StyleBox WindowFrame()
    {
        return Theme.Box(Theme.Background, Theme.Accent.WithAlpha(0.3f), 1);
    }

    public override StyleBox TitleBar()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Color.InterpolateBetween(Theme.Background, Theme.Phosphor, 0.05f),
            BorderColor = Theme.Accent.WithAlpha(0.5f),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    public override Color TitleColour => Theme.Phosphor;

    public override string TitleButtonText(string glyph)
    {
        return glyph == "X" ? "x" : "-";
    }

    public override StyleBox TitleButtonBox()
    {
        return Theme.ButtonBox(Color.InterpolateBetween(Theme.Background, Theme.Phosphor, 0.05f));
    }

    public override Color TitleButtonColour => Theme.Accent;

    /// <summary>A soft shadow inside the bottom and right edges, so windows float over the gradient.</summary>
    public override void DrawWindow(DrawingHandleScreen handle, Control window)
    {
        var shade = 3 * window.UIScale;
        handle.DrawRect(new UIBox2(window.PixelWidth - shade, shade, window.PixelWidth, window.PixelHeight), Color.Black.WithAlpha(0.12f));
        handle.DrawRect(new UIBox2(shade, window.PixelHeight - shade, window.PixelWidth, window.PixelHeight), Color.Black.WithAlpha(0.12f));
    }

    /// <summary>A round button with the program inside and its name under it, ringed in the accent when picked.</summary>
    public override bool DrawIcon(DrawingHandleScreen handle, CMUDesktopIcon icon)
    {
        var scale = icon.UIScale;
        var lit = icon.Selected || icon.Hovered;
        var radius = 18 * scale;
        var centre = new Vector2(icon.PixelWidth / 2f, 3 * scale + radius);
        handle.DrawCircle(centre, radius, lit ? Theme.Accent.WithAlpha(0.35f) : Color.InterpolateBetween(Theme.Background, Theme.Phosphor, 0.1f));
        handle.DrawCircle(centre, radius, Theme.Accent.WithAlpha(lit ? 1f : 0.45f), false);
        DrawPicture(handle, icon, UIBox2.FromDimensions(centre - new Vector2(16, 16) * scale, new Vector2(32, 32) * scale));

        var font = Theme.ShellSmall;
        var text = new UIBox2(0, centre.Y + radius + 2 * scale, icon.PixelWidth, icon.PixelHeight);
        DrawCentred(handle, font, icon.Label, text, lit ? Theme.Accent : Theme.Phosphor, scale);
        return true;
    }
}
