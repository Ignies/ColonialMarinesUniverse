using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>The card terminal as pixel art; rectangles mirror <c>card_terminal_pixel_art.py</c>'s LAYOUT.</summary>
public sealed class ColonyCardTerminalWindow : BaseWindow
{
    private const string ArtDir = "/Textures/CMU14/ColonyEconomy/";
    private const float Px = 3f;
    private static readonly Vector2 ArtSize = new(112, 228);
    private const float NavHeight = 26f;
    private static readonly TimeSpan RecentEvent = TimeSpan.FromSeconds(1.5);

    private static readonly UIBox2 ReceiptRect = UIBox2.FromDimensions(16, 0, 80, 30);
    private static readonly UIBox2 PaperRect = UIBox2.FromDimensions(24, 2, 64, 25);
    private static readonly UIBox2 GlassRect = UIBox2.FromDimensions(16, 58, 80, 82);
    private static readonly UIBox2 LightsRect = UIBox2.FromDimensions(73, 36, 23, 5);
    private static readonly UIBox2 PadRect = UIBox2.FromDimensions(42, 30, 28, 17);
    private const int KeyX = 16, KeyY = 154, KeyW = 24, KeyH = 11, PitchX = 28, PitchY = 13;

    private static readonly Color Phosphor = Color.FromHex("#46ff77");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    public readonly Button Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9;
    public readonly Button Btn00, BtnMenu, BtnCancel, BtnClear, BtnEnter;
    public readonly OutlineButton BtnTap = new();
    public readonly OutlineButton BtnReceipt = new();

    public event Action<CardTerminalKey>? KeyPressed;
    public event Action? TapPressed;
    public event Action? TakeReceiptPressed;

    private readonly ArtLayout _layout = new();
    private readonly Label _title = new();
    private readonly TerminalScreen _screen;
    private readonly CrtScreenControl _crt;
    private readonly PixelSprite _glass, _lights, _pad, _receipt;
    private readonly Dictionary<CardTerminalKey, TerminalKey> _keys = new();

    private ColonyCardTerminalBuiState? _state;
    private TimeSpan? _seenTap, _seenApproved, _seenDeclined, _seenPrint;
    private bool _receiptOut;
    private float _lightsHold;

    public ColonyCardTerminalWindow()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;

        var cache = IoCManager.Resolve<IResourceCache>();
        Art(new TextureRect
        {
            Texture = cache.GetTexture(ArtDir + "terminal_base.png"),
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        }, UIBox2.FromDimensions(Vector2.Zero, ArtSize));

        var screenContent = new Control { MouseFilter = MouseFilterMode.Ignore };
        _glass = new PixelSprite(Rsi(cache, "terminal_screen"));
        _screen = new TerminalScreen(cache.GetFont("/Fonts/RobotoMono/RobotoMono-Regular.ttf", 13),
            cache.GetFont("/Fonts/RobotoMono/RobotoMono-Bold.ttf", 14), GlassRect.Width * Px);
        screenContent.AddChild(_glass);
        screenContent.AddChild(_screen);
        Art(screenContent, GlassRect);
        _crt = new CrtScreenControl { Source = screenContent, Phosphor = Phosphor };
        Art(_crt, GlassRect);

        _receipt = new PixelSprite(Rsi(cache, "terminal_receipt"));
        Art(_receipt, ReceiptRect);
        _lights = new PixelSprite(Rsi(cache, "terminal_lights"));
        Art(_lights, LightsRect);
        _pad = new PixelSprite(Rsi(cache, "terminal_pad"));
        Art(_pad, PadRect);

        Art(BtnTap, PadRect);
        BtnTap.Visible = false;
        BtnTap.OnPressed += _ => TapPressed?.Invoke();
        Art(BtnReceipt, PaperRect);
        BtnReceipt.Visible = false;
        BtnReceipt.OnPressed += _ =>
        {
            BtnReceipt.Visible = false;
            TakeReceiptPressed?.Invoke();
        };

        var keys = Rsi(cache, "terminal_keys");
        Btn1 = Key(keys, "1", CardTerminalKey.D1, 0, 0);
        Btn2 = Key(keys, "2", CardTerminalKey.D2, 1, 0);
        Btn3 = Key(keys, "3", CardTerminalKey.D3, 2, 0);
        Btn4 = Key(keys, "4", CardTerminalKey.D4, 0, 1);
        Btn5 = Key(keys, "5", CardTerminalKey.D5, 1, 1);
        Btn6 = Key(keys, "6", CardTerminalKey.D6, 2, 1);
        Btn7 = Key(keys, "7", CardTerminalKey.D7, 0, 2);
        Btn8 = Key(keys, "8", CardTerminalKey.D8, 1, 2);
        Btn9 = Key(keys, "9", CardTerminalKey.D9, 2, 2);
        BtnMenu = Key(keys, "menu", CardTerminalKey.Menu, 0, 3);
        Btn0 = Key(keys, "0", CardTerminalKey.D0, 1, 3);
        Btn00 = Key(keys, "00", CardTerminalKey.DoubleZero, 2, 3);
        BtnCancel = Key(keys, "cancel", CardTerminalKey.Cancel, 0, 4);
        BtnClear = Key(keys, "clear", CardTerminalKey.Clear, 1, 4);
        BtnEnter = Key(keys, "enter", CardTerminalKey.Enter, 2, 4);

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        body.AddChild(BuildNav());
        _layout.VerticalExpand = true;
        body.AddChild(_layout);
        AddChild(body);

        _lights.Show("off");
        _glass.Play("power_on", () => _glass.Show("on"));
        SetSize = ArtSize * Px + new Vector2(0, NavHeight);
    }

    public void SetCustomer(bool customer)
    {
        _title.Text = Loc.GetString(customer ? "cmu-terminal-nav-customer" : "cmu-terminal-nav-title");
    }

    private Control BuildNav()
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _title.StyleClasses.Add("windowTitle");
        _title.Margin = new Thickness(6, 0, 0, 0);
        _title.HorizontalExpand = true;
        _title.VAlign = Label.VAlignMode.Center;
        row.AddChild(_title);

        var close = new TextureButton
        {
            StyleClasses = { "windowCloseButton" },
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        close.OnPressed += _ => Close();
        row.AddChild(close);

        var bar = new PanelContainer { StyleClasses = { "windowHeader" }, MinHeight = NavHeight, SetHeight = NavHeight };
        bar.AddChild(row);
        return bar;
    }

    private Button Key(RSI rsi, string id, CardTerminalKey key, int col, int row)
    {
        var button = new TerminalKey(rsi, id + "_");
        _keys[key] = button;
        Art(button, UIBox2.FromDimensions(KeyX + col * PitchX, KeyY + row * PitchY, KeyW, KeyH));
        button.OnPressed += _ => KeyPressed?.Invoke(key);
        return button;
    }

    private static RSI Rsi(IResourceCache cache, string name)
        => cache.GetResource<RSIResource>($"{ArtDir}{name}.rsi").RSI;

    private void Art(Control control, UIBox2 grid)
        => _layout.Add(control, UIBox2.FromDimensions(grid.TopLeft * Px, grid.Size * Px));

    // ─── Sizing ────────────────────────────────────────────────────────────

    protected override void EnteredTree()
    {
        base.EnteredTree();

        // Crisp whole screen pixels per art pixel where it fits; shrunk to the screen where it doesn't.
        var scale = 1f;
        if (Parent is { } parent && parent.Size.Y > 0)
        {
            var fit = MathF.Min(parent.Size.X / (ArtSize.X * Px), (parent.Size.Y * 0.95f - NavHeight) / (ArtSize.Y * Px));
            scale = MathF.Min(1f, fit);
        }

        SetSize = ArtSize * Px * scale + new Vector2(0, NavHeight);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => DragMode.Move;

    // ─── State ─────────────────────────────────────────────────────────────

    public void UpdateState(ColonyCardTerminalBuiState s)
    {
        var opening = _state == null;
        _state = s;
        _screen.SetText(s.Header, s.Body, s.Input);

        foreach (var (key, button) in _keys)
        {
            button.Lit = key switch
            {
                <= CardTerminalKey.D9 or CardTerminalKey.DoubleZero => (s.LitKeys & CardTerminalKeys.Digits) != 0,
                CardTerminalKey.Menu => (s.LitKeys & CardTerminalKeys.Menu) != 0,
                CardTerminalKey.Cancel => (s.LitKeys & CardTerminalKeys.Cancel) != 0,
                CardTerminalKey.Clear => (s.LitKeys & CardTerminalKeys.Clear) != 0,
                _ => (s.LitKeys & CardTerminalKeys.Enter) != 0,
            };
            button.Ready = key == CardTerminalKey.Enter && (s.LitKeys & CardTerminalKeys.EnterReady) != 0;
        }

        BtnTap.Visible = s.TapReady;
        _pad.Show(s.TapReady ? "ready" : null);

        if (TakeEvent(s.TappedAt, ref _seenTap, opening))
            _lights.Play("reading", () => _lights.Show("off"));
        if (TakeEvent(s.ApprovedAt, ref _seenApproved, opening))
        {
            _lights.Show("approved");
            _lightsHold = 2f;
        }
        if (TakeEvent(s.DeclinedAt, ref _seenDeclined, opening))
            _lights.Play("declined", () => _lights.Play("declined", () => _lights.Show("off")));

        if (TakeEvent(s.ReceiptPrintedAt, ref _seenPrint, opening))
        {
            _receiptOut = true;
            BtnReceipt.Visible = true;
            _receipt.Play("print", () => _receipt.Show(_state?.ReceiptWaiting == true ? "presented" : null));
        }
        else if (s.ReceiptWaiting && !_receiptOut)
        {
            _receiptOut = true;
            BtnReceipt.Visible = true;
            _receipt.Show("presented");
        }
        else if (!s.ReceiptWaiting && _receiptOut)
        {
            _receiptOut = false;
            BtnReceipt.Visible = false;
            _receipt.Play("take", () => _receipt.Show(null));
        }
    }

    private bool TakeEvent(TimeSpan? at, ref TimeSpan? seen, bool opening)
    {
        if (at == null || at == seen)
            return false;

        seen = at;
        return !opening || _timing.CurTime - at.Value < RecentEvent;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _crt.Visible = _cfg.GetCVar(CCVars.CMUCrtMenuEffect);
        _screen.Scanlines = !_crt.Drawing;

        if (_lightsHold > 0f && (_lightsHold -= args.DeltaSeconds) <= 0f)
            _lights.Show("off");
    }

    // ─── Controls ──────────────────────────────────────────────────────────

    /// <summary>Places children on the art grid, scaled to whatever size the window has.</summary>
    private sealed class ArtLayout : Control
    {
        private readonly Dictionary<Control, UIBox2> _rects = new();

        public ArtLayout()
        {
            MouseFilter = MouseFilterMode.Ignore;
        }

        public void Add(Control child, UIBox2 rect)
        {
            AddChild(child);
            _rects[child] = rect;
        }

        private static float ScaleFor(Vector2 size)
        {
            var scale = MathF.Min(size.X / (ArtSize.X * Px), size.Y / (ArtSize.Y * Px));
            return float.IsFinite(scale) && scale > 0 ? scale : 1f;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var scale = ScaleFor(availableSize);
            foreach (var child in Children)
            {
                if (_rects.TryGetValue(child, out var rect))
                    child.Measure(rect.Size * scale);
            }

            return Vector2.Zero;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            var scale = ScaleFor(finalSize);
            var offset = (finalSize - ArtSize * Px * scale) / 2;
            foreach (var child in Children)
            {
                if (_rects.TryGetValue(child, out var rect))
                    child.Arrange(UIBox2.FromDimensions(offset + rect.TopLeft * scale, rect.Size * scale));
            }

            return finalSize;
        }
    }

    /// <summary>Loops an RSI state, or plays it once and hands over.</summary>
    private sealed class PixelSprite : Control
    {
        private readonly RSI _rsi;
        private Texture[] _frames = Array.Empty<Texture>();
        private float[] _delays = Array.Empty<float>();
        private int _frame;
        private float _elapsed;
        private Action? _then;
        private string? _current;

        public PixelSprite(RSI rsi)
        {
            _rsi = rsi;
            MouseFilter = MouseFilterMode.Ignore;
        }

        public void Show(string? state)
        {
            if (state == _current && _then == null)
                return;

            Set(state, null);
        }

        public void Play(string state, Action then) => Set(state, then);

        private void Set(string? state, Action? then)
        {
            _current = state;
            _then = then;
            _frame = 0;
            _elapsed = 0f;
            if (state != null && _rsi.TryGetState(state, out var rsiState))
            {
                _frames = rsiState.GetFrames(RsiDirection.South);
                _delays = rsiState.GetDelays();
            }
            else
            {
                _frames = Array.Empty<Texture>();
                _delays = Array.Empty<float>();
            }
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            if (_frames.Length == 0 || (_frames.Length == 1 && _then == null))
                return;

            _elapsed += args.DeltaSeconds;
            while (_elapsed >= _delays[_frame])
            {
                _elapsed -= _delays[_frame];
                if (_frame + 1 < _frames.Length)
                {
                    _frame++;
                    continue;
                }

                if (_then is { } then)
                {
                    _then = null;
                    then();
                    return;
                }

                _frame = 0;
            }
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (_frames.Length > 0)
                handle.DrawTextureRect(_frames[_frame], PixelSizeBox);
        }
    }

    /// <summary>A key drawn from its sprites: lit or dark, hovered, pressed, or pulsing when ready.</summary>
    private sealed class TerminalKey : Button
    {
        private readonly PixelSprite _sprite;
        private readonly string _prefix;
        private float _release;
        private bool _wasDown;

        public bool Lit { get; set; } = true;
        public bool Ready { get; set; }

        public TerminalKey(RSI rsi, string prefix)
        {
            _prefix = prefix;
            _sprite = new PixelSprite(rsi);
            AddChild(_sprite);
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            ModulateSelfOverride = Color.White;
            _sprite.Show(_prefix + "on");
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            var down = DrawMode == DrawModeEnum.Pressed;
            if (!down && _wasDown)
                _release = 0.09f;
            _wasDown = down;
            _release -= args.DeltaSeconds;

            var state = down || _release > 0f ? "pressed"
                : !Lit ? "off"
                : DrawMode == DrawModeEnum.Hover ? "hover"
                : Ready ? "ready"
                : "on";
            _sprite.Show(_prefix + state);
        }
    }

    /// <summary>A transparent click target with a light outline on hover: the tap pad, the receipt.</summary>
    public sealed class OutlineButton : Button
    {
        private static readonly Color Hover = new(1f, 0.95f, 0.8f, 0.35f);

        public OutlineButton()
        {
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            ModulateSelfOverride = Color.White;
            DefaultCursorShape = CursorShape.Hand;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            if (DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed)
                handle.DrawRect(PixelSizeBox, Hover, filled: false);
        }
    }

    /// <summary>The terminal's green screen: header, the server's text, the input line and a caret.</summary>
    private sealed class TerminalScreen : Control
    {
        private static readonly Color Green = Color.FromHex("#46ff77");
        private static readonly Color Rule = Color.FromHex("#1f9c43");
        private static readonly Color Bloom = Green.WithAlpha(0.13f);
        private static readonly Color Scan = new(0f, 0f, 0f, 0.22f);

        private readonly Font _font;
        private readonly Font _bold;
        private readonly float _widthVirtual;
        private string _header = string.Empty;
        private string _body = string.Empty;
        private string? _input;
        private float _caretTimer;
        private bool _caretOn = true;

        public bool Scanlines { get; set; } = true;

        public TerminalScreen(Font font, Font bold, float widthVirtual)
        {
            _font = font;
            _bold = bold;
            _widthVirtual = widthVirtual;
            RectClipContent = true;
            MouseFilter = MouseFilterMode.Ignore;
        }

        private int Columns
            => Math.Max(8, (int) ((_widthVirtual - 16f) / (_font.GetCharMetrics(new Rune('M'), 1f)?.Advance ?? 8)));

        public void SetText(string header, string body, string? input)
        {
            _header = header;
            _body = Wrap(body, Columns);
            _input = input;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            _caretTimer += args.DeltaSeconds;
            if (_caretTimer >= 0.5f)
            {
                _caretTimer -= 0.5f;
                _caretOn = !_caretOn;
            }
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var scale = UIScale * MathF.Max(0.5f, MathF.Floor(Size.X / _widthVirtual * 20f) / 20f);
            var inset = 8f * scale;
            var y = inset;

            Glow(handle, _bold, new Vector2(inset, y), _header, scale);
            y += _bold.GetLineHeight(scale);
            handle.DrawRect(new UIBox2(inset, y, PixelSize.X - inset, y + MathF.Max(1f, scale)), Rule);
            y += 4f * scale;

            var text = _input == null ? _body : $"{_body}\n> {_input}";
            Glow(handle, _font, new Vector2(inset, y), text, scale);

            if (_input != null && _caretOn)
            {
                var advance = _font.GetCharMetrics(new Rune('M'), scale)?.Advance ?? 8 * scale;
                var lineHeight = _font.GetLineHeight(scale);
                var lines = text.Split('\n');
                var caretX = inset + lines[^1].Length * advance;
                var caretY = y + (lines.Length - 1) * lineHeight;
                handle.DrawRect(new UIBox2(caretX, caretY + 2f * scale, caretX + advance, caretY + lineHeight), Green);
            }

            if (!Scanlines)
                return;

            var step = MathF.Max(2f, 3f * scale);
            for (var sy = 0f; sy < PixelSize.Y; sy += step)
                handle.DrawRect(new UIBox2(0f, sy, PixelSize.X, sy + scale), Scan);
        }

        private static void Glow(DrawingHandleScreen handle, Font font, Vector2 at, string text, float scale)
        {
            var d = MathF.Max(1f, scale);
            handle.DrawString(font, at + new Vector2(-d, 0), text, scale, Bloom);
            handle.DrawString(font, at + new Vector2(d, 0), text, scale, Bloom);
            handle.DrawString(font, at + new Vector2(0, -d), text, scale, Bloom);
            handle.DrawString(font, at + new Vector2(0, d), text, scale, Bloom);
            handle.DrawString(font, at, text, scale, Green);
        }

        private static string Wrap(string text, int columns)
        {
            var sb = new StringBuilder();
            foreach (var paragraph in text.Split('\n'))
            {
                if (sb.Length > 0)
                    sb.Append('\n');

                var col = 0;
                foreach (var word in paragraph.Split(' '))
                {
                    if (col > 0 && col + 1 + word.Length > columns)
                    {
                        sb.Append('\n');
                        col = 0;
                    }
                    else if (col > 0)
                    {
                        sb.Append(' ');
                        col++;
                    }

                    sb.Append(word);
                    col += word.Length;
                }
            }

            return sb.ToString();
        }
    }
}
