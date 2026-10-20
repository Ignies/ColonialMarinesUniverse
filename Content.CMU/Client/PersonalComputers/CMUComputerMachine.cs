using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The computer as a machine: a drawn bezel round the screen, with the OS desktop in the hole, and the front panel
/// under it. The art is pixel art in the colony ATM's style, drawn at a whole number of screen pixels per art pixel
/// so it stays crisp (2 for the bezel, 3 for the panel, whose card reader must read as well as the ATM's), and sliced so the machine fits any window: the bezel is a nine-slice whose corners stay as drawn
/// and whose edges repeat, and the panel is a three-slice that repeats its middle.
/// </summary>
/// <remarks>
/// The art and the numbers here come from <c>Content.CMU/Design/PersonalComputers/PcPixelArt/pc_pixel_art.py</c>
/// (FRAME, TILE, GLASS, PLATE_Y, FASCIA_H, GLASS_OFF); change one and you must change the other.
/// </remarks>
public sealed partial class CMUComputerMachine : Control
{
    /// <summary>Screen pixels per art pixel at UI scale 1, as the ATM is drawn.</summary>
    public const float Zoom = 2f;

    // Art pixels.
    public const int Tile = 32;
    public const int FrameLeft = 18, FrameTop = 20, FrameRight = 18, FrameBottom = 15;
    // The innermost pixels of the bezel are the tube's dark edge, laid over the screen.
    public const int Glass = 3;
    public const int PlateY = 2;
    public const int GlassOff = 22;

    private static readonly ResPath Art = new("/Textures/CMU14/PersonalComputers/Machine");

    [Dependency] private IResourceCache _cache = default!;

    private readonly Control _screen;
    private readonly Bezel _bezel;
    private CMUComputerPanel? _panel;

    public CMUComputerMachine(Control screen)
    {
        IoCManager.InjectDependencies(this);
        HorizontalExpand = true;
        VerticalExpand = true;
        _screen = screen;
        _bezel = new Bezel(_cache.GetTexture(Art / "glass_off.png"));
        AddChild(_screen);
        AddChild(_bezel);
    }

    /// <summary>Whether the machine is switched off, so the screen shows as a dead tube.</summary>
    public bool Off
    {
        get => _bezel.Off;
        set => _bezel.Off = value;
    }

    /// <summary>Builds the machine in a casing and puts the front panel under its screen.</summary>
    public void Build(CMUComputerCasing casing, CMUComputerPanel panel)
    {
        var name = casing.ToString().ToLowerInvariant();
        _bezel.Frame = _cache.GetTexture(Art / $"frame_{name}.png");
        _bezel.Plate = _cache.GetTexture(Art / $"plate_{name}.png");
        _panel?.Orphan();
        _panel = panel;
        // Last, so a card going into the reader is drawn over the bezel above it.
        AddChild(panel);
        InvalidateArrange();
    }

    /// <summary>
    /// Screen pixels per art pixel: <see cref="Zoom"/> at this UI scale, rounded down to a whole number so the art
    /// stays crisp without growing past its size.
    /// </summary>
    public static int ArtPixel(float uiScale)
    {
        return Math.Max(1, (int) MathF.Floor(Zoom * uiScale + 0.01f));
    }

    /// <summary>
    /// How much bigger the machine is than its screen, in UI units at this UI scale: the bezel round it and the panel
    /// under it. The screen runs under the glass's dark edge, so that is not counted.
    /// </summary>
    public static Vector2 Chrome(float uiScale)
    {
        var p = ArtPixel(uiScale) / uiScale;
        return new Vector2(FrameLeft + FrameRight - 2 * Glass, FrameTop + FrameBottom - 2 * Glass) * p
               + new Vector2(0, CMUComputerPanel.UnitHeight(uiScale));
    }

    /// <summary>The narrowest the machine can be, in UI units: its panel's two ends.</summary>
    public static float MinWidth(float uiScale)
    {
        return CMUComputerPanel.MinUnitWidth(uiScale);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var p = ArtPixel(UIScale) / UIScale;
        var chrome = new Vector2(FrameLeft + FrameRight, FrameTop + FrameBottom) * p + new Vector2(0, CMUComputerPanel.UnitHeight(UIScale));
        _screen.Measure(Vector2.Max(availableSize - chrome, Vector2.Zero));
        _bezel.Measure(availableSize);
        _panel?.Measure(new Vector2(availableSize.X, CMUComputerPanel.UnitHeight(UIScale)));
        // The screen's own minimum counts too, so the window can never squeeze the desktop under the bezel.
        return new Vector2(MathF.Max(chrome.X + _screen.DesiredSize.X, _panel?.DesiredSize.X ?? 0),
            chrome.Y + _screen.DesiredSize.Y);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var p = ArtPixel(UIScale) / UIScale;
        var bezelBottom = finalSize.Y - CMUComputerPanel.UnitHeight(UIScale);
        _screen.Arrange(new UIBox2(
            (FrameLeft - Glass) * p,
            (FrameTop - Glass) * p,
            finalSize.X - (FrameRight - Glass) * p,
            bezelBottom - (FrameBottom - Glass) * p));
        _bezel.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        _panel?.Arrange(new UIBox2(0, bezelBottom, finalSize.X, finalSize.Y));
        return finalSize;
    }

    /// <summary>
    /// Draws <paramref name="source"/> (in texture pixels) over <paramref name="dest"/> (in screen pixels) at
    /// <paramref name="scale"/> screen pixels per texture pixel, repeating it along the axes asked for and cutting the
    /// last copy short; along any other axis it is stretched to fit.
    /// </summary>
    internal static void DrawRepeated(DrawingHandleScreen handle, Texture texture, UIBox2 source, UIBox2 dest, int scale,
        bool alongX, bool alongY)
    {
        var stepX = alongX ? source.Width * scale : dest.Width;
        var stepY = alongY ? source.Height * scale : dest.Height;
        if (stepX <= 0 || stepY <= 0)
            return;

        for (var y = dest.Top; y < dest.Bottom; y += stepY)
        {
            var h = MathF.Min(stepY, dest.Bottom - y);
            for (var x = dest.Left; x < dest.Right; x += stepX)
            {
                var w = MathF.Min(stepX, dest.Right - x);
                var region = UIBox2.FromDimensions(source.Left, source.Top,
                    alongX ? w / scale : source.Width,
                    alongY ? h / scale : source.Height);
                handle.DrawTextureRectRegion(texture, new UIBox2(x, y, x + w, y + h), region);
            }
        }
    }

    /// <summary>
    /// Draws a nine-slice: the corners as drawn, the edges repeated along their length and, if asked, the middle
    /// stretched. Margins are in texture pixels, <paramref name="dest"/> in screen pixels.
    /// </summary>
    internal static void DrawNineSlice(DrawingHandleScreen handle, Texture texture, UIBox2 dest, int scale,
        int left, int top, int right, int bottom, bool middle)
    {
        var (w, h) = (texture.Width, texture.Height);
        var (l, t, r, b) = (left * scale, top * scale, right * scale, bottom * scale);
        var inner = new UIBox2(dest.Left + l, dest.Top + t, dest.Right - r, dest.Bottom - b);
        // Each column and row of the grid: where it is in the texture, then where it goes on screen.
        var cols = new (float S0, float S1, float D0, float D1)[]
        {
            (0, left, dest.Left, inner.Left),
            (left, w - right, inner.Left, inner.Right),
            (w - right, w, inner.Right, dest.Right),
        };
        var rows = new (float S0, float S1, float D0, float D1)[]
        {
            (0, top, dest.Top, inner.Top),
            (top, h - bottom, inner.Top, inner.Bottom),
            (h - bottom, h, inner.Bottom, dest.Bottom),
        };
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                if (row == 1 && col == 1 && !middle)
                    continue;

                var (sx0, sx1, dx0, dx1) = cols[col];
                var (sy0, sy1, dy0, dy1) = rows[row];
                if (dx1 <= dx0 || dy1 <= dy0)
                    continue;

                // The middle is a flat fill, so it is stretched; the edges repeat along their length.
                var middleTile = row == 1 && col == 1;
                DrawRepeated(handle, texture, new UIBox2(sx0, sy0, sx1, sy1), new UIBox2(dx0, dy0, dx1, dy1), scale,
                    col == 1 && !middleTile, row == 1 && !middleTile);
            }
        }
    }

    /// <summary>The bezel and its brand plate, over the screen, and the dead tube when the machine is off.</summary>
    private sealed class Bezel : Control
    {
        private readonly Texture _glassOff;

        public Texture? Frame;
        public Texture? Plate;
        public bool Off;

        public Bezel(Texture glassOff)
        {
            _glassOff = glassOff;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (Frame == null)
                return;

            var k = ArtPixel(UIScale);
            var width = PixelSize.X;
            var bottom = PixelSize.Y - CMUComputerPanel.ArtHeight * CMUComputerPanel.ArtPixel(UIScale);
            if (Off)
            {
                var tube = new UIBox2((FrameLeft - Glass) * k, (FrameTop - Glass) * k,
                    width - (FrameRight - Glass) * k, bottom - (FrameBottom - Glass) * k);
                DrawNineSlice(handle, _glassOff, tube, k, GlassOff, GlassOff, GlassOff, GlassOff, true);
            }

            DrawNineSlice(handle, Frame, new UIBox2(0, 0, width, bottom), k, FrameLeft, FrameTop, FrameRight, FrameBottom, false);
            if (Plate == null)
                return;

            // Centred on whole art pixels, so it lines up with the band's own pixels.
            var x = (width / k - Plate.Width) / 2 * k;
            handle.DrawTextureRect(Plate, UIBox2.FromDimensions(x, PlateY * k, Plate.Width * k, Plate.Height * k));
        }
    }
}
