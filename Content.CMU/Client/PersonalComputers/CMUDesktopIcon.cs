using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

public enum CMUComputerApp : byte
{
    Browser,
    Files,
    System,
    Deeps,
    Mail,

    /// <summary>One of a colony console's own screens.</summary>
    Console,
}

/// <summary>
/// A desktop shortcut: the program's icon at twice its size, or a phosphor glyph when the OS has none.
/// Double click opens the program. A saved page link shows its browser's icon; right click removes it.
/// In Sevastolink it is a numbered main menu row that opens with one click, inverted under the mouse; on a
/// Company workstation, a Semiotic Standard tile: the icon in a rounded plate with its name under it.
/// </summary>
public sealed class CMUDesktopIcon : Control
{
    private static readonly Vector2 GlyphSize = new(32, 32);

    private readonly CMUComputerTheme _theme;
    private readonly Texture? _texture;
    private readonly string _label;
    private TimeSpan _lastClick;
    private bool _hovered;

    public readonly CMUComputerApp App;

    /// <summary>The saved page this icon opens, or null for a program.</summary>
    public readonly string? Link;
    public bool Selected;

    public event Action<CMUDesktopIcon>? OnSelect;
    public event Action<CMUComputerApp>? OnOpen;
    public event Action<CMUDesktopIcon>? OnRemove;

    public CMUDesktopIcon(CMUComputerTheme theme, CMUComputerApp app, string label, string? link = null)
    {
        _theme = theme;
        App = app;
        Link = link;
        _label = theme.Shell switch
        {
            CMUComputerShell.Seegson => Shorten(label.ToUpperInvariant(), 13),
            CMUComputerShell.Weyland => Shorten(label.ToUpperInvariant(), 11),
            _ => Shorten(label, 13),
        };
        MinSize = theme.Shell switch
        {
            CMUComputerShell.Seegson => new Vector2(150, 18),
            CMUComputerShell.Weyland => new Vector2(80, 66),
            _ => new Vector2(84, 70),
        };
        MouseFilter = MouseFilterMode.Stop;
        ToolTip = link == null ? label : $"{label}\n{link}";

        _texture = theme.AppIcon(app);
        OnMouseEntered += _ => _hovered = true;
        OnMouseExited += _ => _hovered = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIRightClick && Link != null)
        {
            args.Handle();
            OnRemove?.Invoke(this);
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        // A terminal menu: one press picks the entry.
        if (_theme.Shell == CMUComputerShell.Seegson)
        {
            OnSelect?.Invoke(this);
            OnOpen?.Invoke(App);
            return;
        }

        var now = IoCManager.Resolve<IGameTiming>().RealTime;
        if (now - _lastClick < TimeSpan.FromSeconds(0.4))
        {
            _lastClick = TimeSpan.Zero;
            OnOpen?.Invoke(App);
            return;
        }

        _lastClick = now;
        OnSelect?.Invoke(this);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        switch (_theme.Shell)
        {
            case CMUComputerShell.Seegson:
                DrawRow(handle);
                return;
            case CMUComputerShell.Weyland:
                DrawTile(handle);
                return;
        }

        var scale = UIScale;
        var glyph = GlyphSize * scale;
        var origin = new Vector2(MathF.Round((PixelWidth - glyph.X) / 2f), MathF.Round(4 * scale));
        var box = UIBox2.FromDimensions(origin, glyph);

        if (Selected)
            handle.DrawRect(new UIBox2(0, 0, PixelWidth, PixelHeight), _theme.Accent.WithAlpha(0.2f));

        if (_texture != null)
            handle.DrawTextureRect(_texture, box);
        else
            DrawGlyph(handle, box, scale);

        var font = _theme.Small;
        var width = 0f;
        foreach (var rune in _label.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, scale, out var metrics))
                width += metrics.Advance;
        }

        var textPos = new Vector2((PixelWidth - width) / 2f, box.Bottom + 4 * scale);
        if (Selected)
            handle.DrawRect(new UIBox2(textPos.X - 2 * scale, textPos.Y, textPos.X + width + 2 * scale, textPos.Y + font.GetLineHeight(scale)), _theme.Accent);

        handle.DrawString(font, textPos, _label, scale, Selected ? _theme.Background : _theme.Phosphor);
    }

    /// <summary>A Sevastolink menu row: its number, then its name, the whole row inverted when picked.</summary>
    private void DrawRow(DrawingHandleScreen handle)
    {
        var scale = UIScale;
        var lit = Selected || _hovered;
        var font = _theme.ShellFont;
        if (lit)
            handle.DrawRect(new UIBox2(0, 0, PixelWidth, PixelHeight), _theme.Phosphor);

        // The number in the terminal face: the pixel face's dotted zero reads as an eight.
        var number = (Parent == null ? 0 : GetPositionInParent()) + 1;
        var digits = _theme.GetFont(CMUNetFont.Terminal, 10);
        var colour = lit ? _theme.Background : _theme.Phosphor;
        handle.DrawString(digits, new Vector2(6 * scale, MathF.Round((PixelHeight - digits.GetLineHeight(scale)) / 2f)), $"{number:00}", scale, colour);
        var y = MathF.Round((PixelHeight - font.GetLineHeight(scale)) / 2f);
        handle.DrawString(font, new Vector2(36 * scale, y), _label, scale, colour);
        if (Link != null)
        {
            var mark = new Vector2(PixelWidth - 10 * scale, PixelHeight / 2f);
            handle.DrawRect(UIBox2.FromDimensions(mark - new Vector2(2, 2) * scale, new Vector2(4, 4) * scale), lit ? _theme.Background : _theme.Dim);
        }
    }

    /// <summary>
    /// A Semiotic Standard tile, the Company's own signage: the icon on a plate with cut corners and its name in
    /// capitals under it.
    /// </summary>
    private void DrawTile(DrawingHandleScreen handle)
    {
        var scale = UIScale;
        var plate = new Vector2(44, 40) * scale;
        var origin = new Vector2(MathF.Round((PixelWidth - plate.X) / 2f), MathF.Round(2 * scale));
        var box = UIBox2.FromDimensions(origin, plate);
        var lit = Selected || _hovered;
        var line = _theme.Accent.WithAlpha(lit ? 1f : 0.75f);

        // The plate's corners are cut, the nearest a line drawing gets to the standard's rounded rectangles.
        var cut = 5 * scale;
        var fill = lit ? _theme.Accent.WithAlpha(0.28f) : _theme.Accent.WithAlpha(0.08f);
        handle.DrawRect(new UIBox2(box.Left + cut, box.Top, box.Right - cut, box.Bottom), fill);
        handle.DrawRect(new UIBox2(box.Left, box.Top + cut, box.Left + cut, box.Bottom - cut), fill);
        handle.DrawRect(new UIBox2(box.Right - cut, box.Top + cut, box.Right, box.Bottom - cut), fill);
        handle.DrawLine(new Vector2(box.Left + cut, box.Top), new Vector2(box.Right - cut, box.Top), line);
        handle.DrawLine(new Vector2(box.Left + cut, box.Bottom), new Vector2(box.Right - cut, box.Bottom), line);
        handle.DrawLine(new Vector2(box.Left, box.Top + cut), new Vector2(box.Left, box.Bottom - cut), line);
        handle.DrawLine(new Vector2(box.Right, box.Top + cut), new Vector2(box.Right, box.Bottom - cut), line);
        handle.DrawLine(new Vector2(box.Left, box.Top + cut), new Vector2(box.Left + cut, box.Top), line);
        handle.DrawLine(new Vector2(box.Right - cut, box.Top), new Vector2(box.Right, box.Top + cut), line);
        handle.DrawLine(new Vector2(box.Left, box.Bottom - cut), new Vector2(box.Left + cut, box.Bottom), line);
        handle.DrawLine(new Vector2(box.Right - cut, box.Bottom), new Vector2(box.Right, box.Bottom - cut), line);

        var glyph = UIBox2.FromDimensions(box.Center - GlyphSize * scale / 2f, GlyphSize * scale);
        if (_texture != null)
            handle.DrawTextureRect(_texture, glyph);
        else
            DrawGlyph(handle, glyph, scale);

        var font = _theme.ShellSmall;
        var width = 0f;
        foreach (var rune in _label.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, scale, out var metrics))
                width += metrics.Advance;
        }

        var textPos = new Vector2(MathF.Round((PixelWidth - width) / 2f), box.Bottom + 4 * scale);
        if (Selected)
            handle.DrawRect(new UIBox2(textPos.X - 2 * scale, textPos.Y, textPos.X + width + 2 * scale, textPos.Y + font.GetLineHeight(scale)), _theme.Accent);

        handle.DrawString(font, textPos, _label, scale, Selected ? _theme.Background : _theme.Accent);
    }

    /// <summary>Cuts long page titles so the label stays under the icon.</summary>
    private static string Shorten(string label, int max)
    {
        return label.Length <= max ? label : label[..(max - 1)] + "~";
    }

    private void DrawGlyph(DrawingHandleScreen handle, UIBox2 box, float scale)
    {
        var c = _theme.Phosphor;
        var fill = _theme.Phosphor.WithAlpha(0.18f);
        var center = box.Center;
        switch (App)
        {
            case CMUComputerApp.Browser:
                var radius = box.Height / 2f - 1;
                handle.DrawCircle(center, radius, fill);
                handle.DrawCircle(center, radius, c, false);
                handle.DrawCircle(center, radius * 0.45f, c, false);
                handle.DrawLine(center - new Vector2(radius, 0), center + new Vector2(radius, 0), c);
                handle.DrawLine(center - new Vector2(0, radius), center + new Vector2(0, radius), c);
                break;
            case CMUComputerApp.Files:
                var body = new UIBox2(box.Left + 2 * scale, box.Top + 8 * scale, box.Right - 2 * scale, box.Bottom - 1 * scale);
                var tab = new UIBox2(body.Left, box.Top + 3 * scale, body.Left + body.Width * 0.4f, body.Top);
                handle.DrawRect(tab, c);
                handle.DrawRect(body, fill);
                handle.DrawRect(body, c, false);
                handle.DrawLine(new Vector2(body.Left, body.Top + 5 * scale), new Vector2(body.Right, body.Top + 5 * scale), c);
                break;
            default:
                var screen = new UIBox2(box.Left + 3 * scale, box.Top + 1 * scale, box.Right - 3 * scale, box.Bottom - 9 * scale);
                handle.DrawRect(screen, fill);
                handle.DrawRect(screen, c, false);
                for (var y = screen.Top + 5 * scale; y < screen.Bottom - 3 * scale; y += 5 * scale)
                    handle.DrawLine(new Vector2(screen.Left + 4 * scale, y), new Vector2(screen.Left + screen.Width * 0.6f, y), c);
                handle.DrawRect(new UIBox2(center.X - 3 * scale, screen.Bottom, center.X + 3 * scale, box.Bottom - 3 * scale), c);
                handle.DrawRect(new UIBox2(center.X - 11 * scale, box.Bottom - 3 * scale, center.X + 11 * scale, box.Bottom), c);
                break;
        }
    }
}
