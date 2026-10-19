using System.Numerics;
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
}

/// <summary>
/// A desktop shortcut: the program's icon at twice its size, or a phosphor glyph when the OS has none.
/// Double click opens the program. A saved page link shows its browser's icon; right click removes it.
/// </summary>
public sealed class CMUDesktopIcon : Control
{
    private static readonly Vector2 GlyphSize = new(32, 32);

    private readonly CMUComputerTheme _theme;
    private readonly Texture? _texture;
    private readonly string _label;
    private TimeSpan _lastClick;

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
        _label = Shorten(label);
        MinSize = new Vector2(84, 70);
        MouseFilter = MouseFilterMode.Stop;
        ToolTip = link == null ? label : $"{label}\n{link}";

        _texture = theme.AppIcon(app);
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

    /// <summary>Cuts long page titles so the label stays under the icon.</summary>
    private static string Shorten(string label)
    {
        const int max = 13;
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
