using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The startup sequence: the tube warms up, then each OS logo builds in interlaced lines and holds. Under the
/// last one the boot log runs with a progress bar. A logo whose file does not exist yet is skipped, and with
/// none the OS name takes their place, animated the same way, so dropping a file at a logo path is all a new
/// logo needs. Clicking skips the sequence. A computer jacked into the deep web boots corrupted: intrusion lines
/// break into the log, characters scramble, the logo drops out and torn bands roll across the raster.
/// </summary>
public sealed class CMUComputerBootSplash : Control
{
    private const float WarmUp = 0.55f;
    private const float Reveal = 1.1f;
    private const float Hold = 1.0f;
    private const float Swap = 0.3f;
    private const float LineEvery = 0.32f;
    private const float FinalHold = 0.6f;
    private const float FadeOut = 0.35f;
    private const int RowHeight = 2;

    private readonly CMUComputerTheme _theme;
    private readonly List<Logo> _logos = new();
    private readonly string _name;
    private readonly Font _nameFont;
    private readonly List<string> _lines = new();
    private readonly float _logEnd;
    private readonly float _end;

    private const string GlitchChars = "#$%&@!?/|<>{}[]~^*";

    private readonly bool _corrupted;
    private readonly Random _glitch = new();
    private float _time;
    private bool _finished;

    public event Action? Finished;

    private readonly record struct Logo(Texture? Texture, RSI.State? State)
    {
        public Vector2 Size => State != null ? State.Size : Texture!.Size;
    }

    public CMUComputerBootSplash(CMUComputerTheme theme, bool corrupted = false)
    {
        _theme = theme;
        _corrupted = corrupted;
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Stop;

        foreach (var path in theme.Os.Logos)
        {
            var res = new ResPath(path);
            if (theme.TryGetRsiState(res, theme.Os.LogoState, out var state))
                _logos.Add(new Logo(null, state));
            else if (theme.TryGetTexture(res, out var texture))
                _logos.Add(new Logo(texture, null));
        }

        _name = Loc.GetString(theme.Os.Name);
        _nameFont = theme.GetFont(CMUNetFont.Display, 30, true);
        var intrusion = 1;
        foreach (var line in theme.Os.BootLines)
        {
            _lines.Add(Loc.GetString(line));
            if (corrupted && intrusion <= 4 && _lines.Count % 2 == 0)
                _lines.Add(Loc.GetString($"cmu-deep-boot-{intrusion++}"));
        }

        if (corrupted)
            _lines.Add(Loc.GetString("cmu-deep-boot-5"));

        var lastStart = WarmUp + Math.Max(0, _logos.Count - 1) * (Reveal + Hold + Swap);
        _logEnd = lastStart + Reveal + _lines.Count * LineEvery;
        _end = _logEnd + FinalHold + FadeOut;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        Finish();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        // Opening the terminal loads fonts and textures, and that hitch arrives as one long first frame.
        // Capped, it slows the sequence down instead of skipping it.
        _time += MathF.Min(args.DeltaSeconds, 0.1f);
        if (_time >= _end)
            Finish();
    }

    private void Finish()
    {
        if (_finished)
            return;

        _finished = true;
        Finished?.Invoke();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        var scale = UIScale;
        handle.DrawRect(PixelSizeBox, Color.Black);

        if (_time < WarmUp)
        {
            DrawWarmUp(handle, size, _time / WarmUp);
            return;
        }

        handle.DrawRect(PixelSizeBox, _theme.Background);

        // Which logo is up, and how long it has been up.
        var index = _logos.Count == 0
            ? 0
            : Math.Min(_logos.Count - 1, (int) ((_time - WarmUp) / (Reveal + Hold + Swap)));
        var last = index >= _logos.Count - 1;
        var local = _time - WarmUp - index * (Reveal + Hold + Swap);
        Logo? logo = _logos.Count == 0 ? null : _logos[index];

        var box = LogoBox(logo, size);
        var flicker = 0.9f + 0.1f * MathF.Sin(_time * 37f) * MathF.Max(0, 1 - local / Reveal);
        var pulse = local > Reveal ? 0.85f + 0.15f * MathF.Sin(_time * 2.4f) : 1f;
        if (_corrupted && _glitch.NextSingle() < 0.08f)
            pulse *= 0.25f;

        DrawLogo(handle, logo, box, scale, flicker * pulse);

        var reveal = Math.Clamp(local / Reveal, 0f, 1f);
        if (reveal < 1f)
            CoverUnrevealed(handle, box, reveal, scale);

        if (!last)
        {
            // An earlier logo dims away before the next one builds.
            var swap = (local - Reveal - Hold) / Swap;
            if (swap > 0)
                handle.DrawRect(PixelSizeBox, _theme.Background.WithAlpha(Math.Clamp(swap, 0f, 1f)));
            return;
        }

        DrawLog(handle, size, box, scale, local - Reveal);

        if (_corrupted)
            DrawTears(handle, size);

        var fade = (_time - _logEnd - FinalHold) / FadeOut;
        if (fade > 0)
            handle.DrawRect(PixelSizeBox, _theme.Background.WithAlpha(Math.Clamp(fade, 0f, 1f)));
    }

    /// <summary>A few bands torn out of the raster each frame: a bright smear and a dark gap under it.</summary>
    private void DrawTears(DrawingHandleScreen handle, Vector2i size)
    {
        var bands = _glitch.Next(0, 4);
        for (var i = 0; i < bands; i++)
        {
            var y = _glitch.NextSingle() * size.Y;
            var height = 2 + _glitch.NextSingle() * 10;
            var shift = (_glitch.NextSingle() - 0.5f) * size.X * 0.3f;
            handle.DrawRect(new UIBox2(shift, y, size.X + shift, y + height), _theme.Phosphor.WithAlpha(0.12f + _glitch.NextSingle() * 0.2f));
            handle.DrawRect(new UIBox2(0, y + height, size.X, y + height + 2), Color.Black.WithAlpha(0.6f));
        }
    }

    /// <summary>Scrambles a share of a line's characters, freshly each frame.</summary>
    private string Corrupt(string line)
    {
        if (!_corrupted)
            return line;

        var chars = line.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] != ' ' && _glitch.NextSingle() < 0.07f)
                chars[i] = GlitchChars[_glitch.Next(GlitchChars.Length)];
        }

        return new string(chars);
    }

    /// <summary>A dot stretches into a line across the middle, then the line opens into the full raster.</summary>
    private void DrawWarmUp(DrawingHandleScreen handle, Vector2i size, float t)
    {
        var bright = Color.InterpolateBetween(_theme.Phosphor, Color.White, 0.6f);
        var cy = size.Y / 2f;
        if (t < 0.5f)
        {
            var half = size.X / 2f * (t / 0.5f);
            handle.DrawRect(new UIBox2(size.X / 2f - half, cy - 1, size.X / 2f + half, cy + 1), bright);
            return;
        }

        var open = (t - 0.5f) / 0.5f;
        var halfHeight = MathF.Max(1, size.Y / 2f * open);
        handle.DrawRect(new UIBox2(0, cy - halfHeight, size.X, cy + halfHeight),
            Color.InterpolateBetween(bright, _theme.Background, open));
    }

    private UIBox2 LogoBox(Logo? logo, Vector2i size)
    {
        Vector2 drawn;
        if (logo is { } l)
        {
            // Logo art is pixel art: whole number upscales only, as large as fits 70% x 35% of the screen.
            var content = l.Size;
            var fit = MathF.Max(1, MathF.Floor(MathF.Min(size.X * 0.7f / content.X, size.Y * 0.35f / content.Y)));
            drawn = content * fit;
        }
        else
        {
            drawn = new Vector2(Measure(_nameFont, _name, UIScale), _nameFont.GetLineHeight(UIScale));
        }

        var topLeft = new Vector2(MathF.Round((size.X - drawn.X) / 2f), MathF.Round(size.Y * 0.36f - drawn.Y / 2f));
        return UIBox2.FromDimensions(topLeft, drawn);
    }

    private void DrawLogo(DrawingHandleScreen handle, Logo? logo, UIBox2 box, float scale, float brightness)
    {
        if (logo is { } l)
        {
            var texture = l.State != null ? CurrentFrame(l.State) : l.Texture!;
            handle.DrawTextureRect(texture, box, Color.White.WithAlpha(brightness));
            return;
        }

        // Cheap bloom: the name drawn a few times around itself, faintly, then sharp on top.
        var glow = _theme.Phosphor.WithAlpha(0.12f * brightness);
        foreach (var offset in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) })
            handle.DrawString(_nameFont, box.TopLeft + offset * scale, _name, scale, glow);

        handle.DrawString(_nameFont, box.TopLeft, _name, scale, _theme.Phosphor.WithAlpha(brightness));
        var underline = box.Bottom + 6 * scale;
        handle.DrawRect(new UIBox2(box.Left, underline, box.Right, underline + 2 * scale), _theme.Phosphor.WithAlpha(0.7f * brightness));
    }

    private Texture CurrentFrame(RSI.State state)
    {
        var frames = state.GetFrames(RsiDirection.South);
        var delays = state.GetDelays();
        if (frames.Length <= 1 || delays.Length == 0)
            return frames[0];

        var total = 0f;
        foreach (var delay in delays)
            total += delay;

        var t = _time % total;
        for (var i = 0; i < frames.Length && i < delays.Length; i++)
        {
            if (t < delays[i])
                return frames[i];

            t -= delays[i];
        }

        return frames[^1];
    }

    /// <summary>Even rows draw in top to bottom over the first half, odd rows over the second: an interlaced build.</summary>
    private void CoverUnrevealed(DrawingHandleScreen handle, UIBox2 box, float reveal, float scale)
    {
        var row = MathF.Max(1, RowHeight * scale);
        var top = box.Top - 2 * scale;
        var bottom = box.Bottom + 10 * scale;
        var rows = (int) MathF.Ceiling((bottom - top) / row);
        var evenShown = Math.Clamp(reveal * 2, 0, 1) * rows;
        var oddShown = Math.Clamp(reveal * 2 - 1, 0, 1) * rows;

        for (var i = 0; i < rows; i++)
        {
            var shown = i % 2 == 0 ? i < evenShown : i < oddShown;
            if (shown)
                continue;

            var y = top + i * row;
            handle.DrawRect(new UIBox2(box.Left - 4 * scale, y, box.Right + 4 * scale, y + row), _theme.Background);
        }
    }

    /// <summary>A solid progress bar under the logo, then the newest boot lines centred under it.</summary>
    private void DrawLog(DrawingHandleScreen handle, Vector2i size, UIBox2 logoBox, float scale, float elapsed)
    {
        if (elapsed < 0)
            return;

        var barWidth = MathF.Max(logoBox.Width, MathF.Min(size.X * 0.6f, 460 * scale));
        var left = (size.X - barWidth) / 2f;
        var y = logoBox.Bottom + 30 * scale;
        var progress = _lines.Count == 0 ? 1f : Math.Clamp(elapsed / (_lines.Count * LineEvery), 0f, 1f);
        handle.DrawRect(new UIBox2(left, y, left + barWidth, y + 12 * scale), _theme.Phosphor.WithAlpha(0.18f));
        handle.DrawRect(new UIBox2(left, y, left + barWidth * progress, y + 12 * scale), _theme.Phosphor);
        y += 30 * scale;

        var shown = Math.Min(_lines.Count, (int) MathF.Floor(elapsed / LineEvery) + 1);
        var lineHeight = _theme.Font.GetLineHeight(scale) + 3 * scale;
        const int visible = 3;
        for (var i = Math.Max(0, shown - visible); i < shown; i++)
        {
            var text = Corrupt(_lines[i]);
            var width = Measure(_theme.Font, text, scale);
            var color = i == shown - 1 ? _theme.Phosphor : _theme.Dim;
            handle.DrawString(_theme.Font, new Vector2((size.X - width) / 2f, y), text, scale, color);
            y += lineHeight;
        }
    }

    private static float Measure(Font font, string text, float scale)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, scale, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }
}
