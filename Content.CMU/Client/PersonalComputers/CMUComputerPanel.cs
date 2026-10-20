using System.Numerics;
using Content.Client.CMU14.ColonyEconomy;
using Content.Client.Resources;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The front of the machine under the screen, a slim strip drawn in its casing: the data drive with its activity
/// light on the left, a vent across the middle, and on the right the ATM's card reader, the power light and the power
/// button. The reader is the ATM's own, lights, slot shadow and the card standing in it included, and so are the
/// status lights.
/// </summary>
/// <remarks>
/// The art is <c>fascia_&lt;casing&gt;.png</c>, <c>housing_&lt;casing&gt;.png</c> and <c>power.rsi</c> from
/// <c>Content.CMU/Design/PersonalComputers/PcPixelArt/pc_pixel_art.py</c>; the rectangles here mirror its LAYOUT.
/// </remarks>
public sealed partial class CMUComputerPanel : Control
{
    /// <summary>
    /// Screen pixels per art pixel at UI scale 1: the ATM window's own size, so the reader reads as well as the
    /// ATM's. The bezel above is drawn at 2; the panel's pillars are narrower to match it.
    /// </summary>
    public const float Zoom = 3f;

    // Art pixels: the height of the strip, its fixed ends, and the vent between them, which repeats.
    public const int ArtHeight = 22;
    public const int LeftEnd = 45;
    public const int RightEnd = 160;

    private static readonly ResPath Art = new("/Textures/CMU14/PersonalComputers/Machine");
    private static readonly ResPath Reader = new("/Textures/CMU14/ColonyEconomy/atm_reader.rsi");
    private static readonly ResPath Leds = new("/Textures/CMU14/ColonyEconomy/atm_leds.rsi");

    // In the left end.
    private static readonly UIBox2 DiskLedRect = UIBox2.FromDimensions(36, 6, 5, 5);
    // In the right end. The reader is taller than the strip: its housing stands up out of it over the bezel's
    // bottom edge, short of the glass. The reader keeps the ATM's rectangles round the housing, so the ATM's lights,
    // slot shadow and card line up on it as they do there.
    private static readonly UIBox2 HousingRect = UIBox2.FromDimensions(60, -8, 55, 30);
    private static readonly UIBox2 ReaderRect = UIBox2.FromDimensions(62, -18, 51, 42);
    private static readonly UIBox2 CardRect = UIBox2.FromDimensions(62, -32, 51, 48);
    private static readonly UIBox2 PowerLedRect = UIBox2.FromDimensions(119, 12, 5, 5);
    private static readonly UIBox2 PowerRect = UIBox2.FromDimensions(130, 3, 21, 17);

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IResourceCache _cache = default!;

    private readonly ICMUComputerHost? _host;
    private readonly Texture _fascia;
    private readonly Dictionary<Control, (UIBox2 Rect, bool Right)> _rects = new();
    private readonly ColonyAtmWindow.AtmSprite _readerLights;
    private readonly ColonyAtmWindow.ReaderCard _card;
    private readonly ColonyAtmWindow.ReaderButton _reader;
    private readonly ColonyAtmWindow.AtmSprite _powerLed;
    private readonly ColonyAtmWindow.AtmSprite _diskLed;
    private readonly PowerButton _power;
    private bool _cardKnown;
    private string? _cardShown;
    private float _busy;
    private float _nextBlip;

    public event Action? OnClicked;

    public CMUComputerPanel(CMUComputerCasing casing, ICMUComputerHost? host)
    {
        IoCManager.InjectDependencies(this);
        _host = host;
        var name = casing.ToString().ToLowerInvariant();
        _fascia = _cache.GetTexture(Art / $"fascia_{name}.png");

        var reader = _cache.GetResource<RSIResource>(Reader).RSI;
        var leds = _cache.GetResource<RSIResource>(Leds).RSI;
        _readerLights = new ColonyAtmWindow.AtmSprite(reader);
        _card = new ColonyAtmWindow.ReaderCard();
        var mouth = new ColonyAtmWindow.AtmSprite(reader);
        mouth.Show("bay_glass");
        _reader = new ColonyAtmWindow.ReaderButton
        {
            ToolTip = Loc.GetString("cmu-pc-panel-reader"),
            Click = ColonyAtmWindow.ReaderMode.Insert,
        };
        _reader.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            Pulse(0.6f);
            _host?.UseReader();
        };

        _powerLed = Light(leds, "cmu-pc-panel-power-led");
        _diskLed = Light(leds, "cmu-pc-panel-disk-led");
        _power = new PowerButton(_cache.GetResource<RSIResource>(Art / "power.rsi").RSI, name + "_")
        {
            ToolTip = Loc.GetString("cmu-pc-panel-power"),
        };
        _power.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            _host?.PressPower();
        };

        // The card stands in front of the recess with the slot's shadow over where it goes in; the reader as a whole
        // is a button on top, as on the ATM.
        Place(new Picture(_cache.GetTexture(Art / $"housing_{name}.png")), HousingRect, true);
        Place(_readerLights, ReaderRect, true);
        Place(_card, CardRect, true);
        Place(mouth, ReaderRect, true);
        Place(_reader, ReaderRect, true);
        Place(_powerLed, PowerLedRect, true);
        Place(_power, PowerRect, true);
        Place(_diskLed, DiskLedRect, false);
    }

    private static ColonyAtmWindow.AtmSprite Light(RSI rsi, string tooltip)
    {
        return new ColonyAtmWindow.AtmSprite(rsi)
        {
            ToolTip = Loc.GetString(tooltip),
            MouseFilter = MouseFilterMode.Pass,
        };
    }

    /// <summary>Puts a part on the panel at its art rectangle: in the left end, or the right if <paramref name="right"/>.</summary>
    private void Place(Control control, UIBox2 rect, bool right)
    {
        AddChild(control);
        _rects[control] = (rect, right);
    }

    /// <summary>Screen pixels per art pixel: <see cref="Zoom"/> at this UI scale, rounded down to a whole number.</summary>
    public static int ArtPixel(float uiScale)
    {
        return Math.Max(1, (int) MathF.Floor(Zoom * uiScale + 0.01f));
    }

    /// <summary>The panel's two fixed ends side by side, in UI units: the narrowest it can be.</summary>
    public static float MinUnitWidth(float uiScale)
    {
        return (LeftEnd + RightEnd) * ArtPixel(uiScale) / uiScale;
    }

    /// <summary>The panel's height in UI units at this UI scale.</summary>
    public static float UnitHeight(float uiScale)
    {
        return ArtHeight * ArtPixel(uiScale) / uiScale;
    }

    /// <summary>Keeps the disk light busy for a while: pages loading, files saving, the machine booting.</summary>
    public void Pulse(float seconds)
    {
        _busy = MathF.Max(_busy, seconds);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var on = _host?.On ?? true;
        var card = _host?.CardPrototype;

        // A running machine ticks over now and then even when nobody touches it.
        _busy -= args.DeltaSeconds;
        _nextBlip -= args.DeltaSeconds;
        if (on && _nextBlip <= 0)
        {
            _nextBlip = _random.NextFloat(2f, 7f);
            Pulse(_random.NextFloat(0.1f, 0.4f));
        }

        // Dark while the machine is off (the unlit strips and lamps are part of the panel's art), blinking for a card
        // while it runs, and steady green with one in.
        _readerLights.Show(!on ? null : card != null ? "lights_ok" : "lights_ready");
        _powerLed.Show(on ? "green" : null);
        _diskLed.Show(on && _busy > 0 ? "amber_busy" : null);
        _power.Lit = on;
        _reader.Click = card != null ? ColonyAtmWindow.ReaderMode.LogOff : ColonyAtmWindow.ReaderMode.Insert;

        if (_cardKnown && card == _cardShown)
            return;

        // A card already in when the window opens is simply there; one put in or taken out while it is open moves.
        if (card != null)
            _card.Insert(card, animate: _cardKnown);
        else if (_cardShown != null)
            _card.Eject();

        _cardKnown = true;
        _cardShown = card;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var p = ArtPixel(UIScale) / UIScale;
        foreach (var (child, (rect, _)) in _rects)
        {
            child.Measure(rect.Size * p);
        }

        return new Vector2(LeftEnd + RightEnd, ArtHeight) * p;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var p = ArtPixel(UIScale) / UIScale;
        var rightEnd = finalSize.X - RightEnd * p;
        foreach (var (child, (rect, right)) in _rects)
        {
            var origin = new Vector2(right ? rightEnd : 0, 0);
            child.Arrange(UIBox2.FromDimensions(origin + rect.TopLeft * p, rect.Size * p));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var k = ArtPixel(UIScale);
        var width = PixelSize.X;
        var height = ArtHeight * k;
        var right = width - RightEnd * k;
        var tile = CMUComputerMachine.Tile;
        CMUComputerMachine.DrawRepeated(handle, _fascia, new UIBox2(0, 0, LeftEnd, ArtHeight),
            new UIBox2(0, 0, LeftEnd * k, height), k, false, false);
        CMUComputerMachine.DrawRepeated(handle, _fascia, new UIBox2(LeftEnd, 0, LeftEnd + tile, ArtHeight),
            new UIBox2(LeftEnd * k, 0, right, height), k, true, false);
        CMUComputerMachine.DrawRepeated(handle, _fascia, new UIBox2(LeftEnd + tile, 0, _fascia.Width, ArtHeight),
            new UIBox2(right, 0, width, height), k, false, false);
    }

    /// <summary>A texture drawn over the whole control, for parts that never change.</summary>
    private sealed class Picture(Texture texture) : Control
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            handle.DrawTextureRect(texture, PixelSizeBox);
        }
    }

    /// <summary>
    /// The power key, drawn from its sprites: raised, hovered or pushed in, its mark lit green while the machine runs.
    /// A quick click still shows it travel, as the ATM's keys do.
    /// </summary>
    private sealed class PowerButton : Button
    {
        private const float ReleaseSeconds = 0.09f;

        private readonly ColonyAtmWindow.AtmSprite _sprite;
        private readonly string _prefix;
        private float _release;
        private bool _wasDown;

        /// <summary>The machine is on, so the mark is lit.</summary>
        public bool Lit { get; set; }

        public PowerButton(RSI rsi, string prefix)
        {
            _prefix = prefix;
            _sprite = new ColonyAtmWindow.AtmSprite(rsi);
            AddChild(_sprite);
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            // The button stylesheet tints by state, which would muddy the sprites.
            ModulateSelfOverride = Color.White;
            _sprite.Show(_prefix + "off");
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            var down = DrawMode == DrawModeEnum.Pressed;
            if (!down && _wasDown)
                _release = ReleaseSeconds;

            _wasDown = down;
            _release -= args.DeltaSeconds;
            var look = down || _release > 0f ? "_pressed" : DrawMode == DrawModeEnum.Hover ? "_hover" : string.Empty;
            _sprite.Show(_prefix + (Lit ? "on" : "off") + look);
        }
    }
}
