using System;
using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.AU14.ColonyEconomy;

/// <summary>
///     Borderless ATM terminal. The Weyland-Yutani ATM sprite is the entire window background;
///     transparent hotspots sit over the painted keypad, and the green CRT text is drawn into
///     the painted screen.
/// </summary>
public sealed class ColonyAtmWindow : BaseWindow
{
    // ─── Background art (679 x 899) ────────────────────────────────────────
    private const string BgPath        = "/Textures/_AU14/ColonyEconomy/atm_ui.png";
    private const string BgSkimmerPath = "/Textures/_AU14/ColonyEconomy/atm_ui_skimmer.png";
    private const float NativeW = 679f;
    private const float NativeH = 899f;
    private const float SF = 0.8f; // on-screen scale of the sprite

    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold    = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    // Screen text rectangle, in native sprite pixels.
    private const float ScreenX = 77f, ScreenY = 303f, ScreenW = 337f, ScreenH = 257f;

    private readonly Texture _bg1;
    private readonly Texture _bg2;
    private readonly TextureRect _bg;
    private readonly AtmScreenControl _screen;

    // Buttons accessed by the BUI.
    public readonly Button Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9;
    public readonly Button BtnEnter;
    public readonly Button BtnOk;
    public readonly Button BtnDel;

    public ColonyAtmWindow()
    {
        Resizable = false;
        // The window itself must catch clicks on empty areas so the frame can be dragged
        // (Control defaults to MouseFilter.Ignore). Keypad buttons still consume their own clicks.
        MouseFilter = MouseFilterMode.Stop;

        var cache = IoCManager.Resolve<IResourceCache>();
        _bg1 = cache.GetTexture(BgPath);
        _bg2 = cache.GetTexture(BgSkimmerPath);

        var bodyFont  = cache.GetFont(MonoRegular, 11);
        var boldFont  = cache.GetFont(MonoBold, 12);

        var layout = new LayoutContainer { MouseFilter = MouseFilterMode.Ignore };

        _bg = new TextureRect
        {
            Texture = _bg1,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        layout.AddChild(_bg);
        LayoutContainer.SetPosition(_bg, Vector2.Zero);
        _bg.SetSize = new Vector2(NativeW * SF, NativeH * SF);

        _screen = new AtmScreenControl(bodyFont, boldFont, ScreenW * SF);
        layout.AddChild(_screen);
        Place(_screen, ScreenX, ScreenY, ScreenW, ScreenH);

        // Keypad hotspots over the painted keys (centres measured from layout.png).
        Btn1 = Key(layout, 462, 468); Btn2 = Key(layout, 510, 468); Btn3 = Key(layout, 558, 468);
        Btn4 = Key(layout, 462, 522); Btn5 = Key(layout, 510, 522); Btn6 = Key(layout, 558, 522);
        Btn7 = Key(layout, 462, 575); Btn8 = Key(layout, 510, 575); Btn9 = Key(layout, 558, 575);
        BtnDel = Key(layout, 462, 627); Btn0 = Key(layout, 510, 627); BtnOk = Key(layout, 558, 627);
        BtnEnter = KeyRect(layout, 451, 678, 120, 55);

        AddChild(layout);

        MinSize = new Vector2(NativeW * SF, NativeH * SF);
        SetSize = new Vector2(NativeW * SF, NativeH * SF);
    }

    // ─── Layout helpers ────────────────────────────────────────────────────

    private static void Place(Control c, float x, float y, float w, float h)
    {
        LayoutContainer.SetPosition(c, new Vector2(x * SF, y * SF));
        c.SetSize = new Vector2(w * SF, h * SF);
    }

    private static Button Key(LayoutContainer layout, float cx, float cy)
        => KeyRect(layout, cx - 22f, cy - 25f, 44f, 50f);

    private static Button KeyRect(LayoutContainer layout, float x, float y, float w, float h)
    {
        var b = new KeyButton();
        layout.AddChild(b);
        Place(b, x, y, w, h);
        return b;
    }

    // The whole frame is draggable; buttons consume their own clicks first.
    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => DragMode.Move;

    // ─── State ─────────────────────────────────────────────────────────────

    public void UpdateDisplay(ColonyAtmBuiState s)
    {
        _bg.Texture = s.SkimmerInstalled ? _bg2 : _bg1;

        var header  = s.AccountNumber > 0 ? $"ACCT #{s.AccountNumber}" : "COLONY ATM";
        var balance = s.Screen >= AtmScreen.MainMenu && s.AccountNumber > 0 ? $"BAL ${s.Balance}" : string.Empty;
        var input   = IsInputScreen(s.Screen);

        _screen.SetState(header, balance, BuildBody(s), BuildBuffer(s), input);
    }

    private static string BuildBody(ColonyAtmBuiState s)
    {
        return s.Screen switch
        {
            AtmScreen.Welcome => "COLONY FINANCIAL TERMINAL\nv2.7  UN TREASURY\n\n1) REMOTE DEPOSIT\n\nInsert ID card for account access.",
            AtmScreen.PinEntry => Combine("ENTER PIN:", s.StatusMessage),
            AtmScreen.PinLocked => "** CARD LOCKED **\nToo many incorrect attempts. Please try again later.",
            AtmScreen.MainMenu => $"Welcome, {s.OwnerName}.\n\n1) WITHDRAW\n2) DEPOSIT\n3) TRANSFER\n4) REMOTE DEPOSIT\n5) EXIT",
            AtmScreen.Withdraw => Combine("WITHDRAW\nEnter amount:", s.StatusMessage),
            AtmScreen.WithdrawConfirm => $"{s.StatusMessage}\n\nENTER = confirm   DEL = cancel",
            AtmScreen.Deposit => Combine("DEPOSIT\nEnter amount:", s.StatusMessage),
            AtmScreen.RemoteDeposit => Combine("REMOTE DEPOSIT\nRecipient account #:", s.StatusMessage),
            AtmScreen.RemoteDepositAmount => s.StatusMessage,
            AtmScreen.RemoteDepositConfirm => $"{s.StatusMessage}\n\nENTER = confirm   DEL = cancel",
            AtmScreen.Transfer => Combine("TRANSFER\nRecipient account #:", s.StatusMessage),
            AtmScreen.TransferAmount => s.StatusMessage,
            AtmScreen.TransferConfirm => $"{s.StatusMessage}\n\nENTER = confirm   DEL = cancel",
            AtmScreen.Result => $"{s.StatusMessage}\n\nENTER to continue.",
            AtmScreen.SkimmerData => BuildSkimmer(s.SkimmerData),
            _ => string.Empty,
        };
    }

    private static string BuildBuffer(ColonyAtmBuiState s)
    {
        return s.Screen switch
        {
            AtmScreen.PinEntry => s.KeypadBuffer,
            AtmScreen.Withdraw or AtmScreen.Deposit
                or AtmScreen.RemoteDepositAmount or AtmScreen.TransferAmount
                    => s.KeypadBuffer.Length > 0 ? $"${s.KeypadBuffer}" : string.Empty,
            AtmScreen.RemoteDeposit or AtmScreen.Transfer
                    => s.KeypadBuffer.Length > 0 ? $"#{s.KeypadBuffer}" : string.Empty,
            _ => string.Empty,
        };
    }

    private static string BuildSkimmer(System.Collections.Generic.List<SkimmedAccount> accounts)
    {
        var sb = new StringBuilder("CAPTURED ACCOUNTS\n");
        if (accounts.Count == 0)
            sb.Append("(none)");
        foreach (var a in accounts)
            sb.Append($"#{a.AccountNumber} {a.Name} P{a.Pin}\n");
        return sb.ToString();
    }

    private static bool IsInputScreen(AtmScreen screen) =>
        screen is AtmScreen.PinEntry or AtmScreen.Withdraw or AtmScreen.Deposit
            or AtmScreen.RemoteDeposit or AtmScreen.RemoteDepositAmount
            or AtmScreen.Transfer or AtmScreen.TransferAmount;

    private static string Combine(string prompt, string status) =>
        string.IsNullOrEmpty(status) ? prompt : $"{prompt}\n{status}";

    // ─── Transparent keypad hotspot ────────────────────────────────────────

    private sealed class KeyButton : Button
    {
        private static readonly Color Hover = new(0.27f, 1f, 0.42f, 0.16f);
        private static readonly Color Press = new(0.27f, 1f, 0.42f, 0.34f);

        public KeyButton()
        {
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            if (DrawMode == DrawModeEnum.Pressed)
                handle.DrawRect(PixelSizeBox, Press);
            else if (DrawMode == DrawModeEnum.Hover)
                handle.DrawRect(PixelSizeBox, Hover);
        }
    }

    // ─── CRT screen renderer (text + caret + scanlines) ────────────────────

    private sealed class AtmScreenControl : Control
    {
        private static readonly Color Green = Color.FromHex("#46ff77");
        private static readonly Color Dim   = Color.FromHex("#1f9c43");
        private static readonly Color Scan  = new(0f, 0f, 0f, 0.16f);
        private static readonly Color Tint  = new(0.12f, 1f, 0.42f, 0.05f);

        private const float CharInterval = 0.022f;
        private const float CaretBlink   = 0.5f;

        private readonly Font _font;
        private readonly Font _bold;
        private readonly float _widthVirtual;
        private readonly float _advanceVirtual;

        private string _header = string.Empty;
        private string _balance = string.Empty;
        private string _body = string.Empty;   // already word-wrapped
        private string _buffer = string.Empty;
        private bool _input;

        private int _shown;
        private float _charTimer;
        private float _caretTimer;
        private bool _caretOn = true;

        public AtmScreenControl(Font font, Font bold, float widthVirtual)
        {
            _font = font;
            _bold = bold;
            _widthVirtual = widthVirtual;
            _advanceVirtual = font.GetCharMetrics(new System.Text.Rune('M'), 1f)?.Advance ?? 7f;
            RectClipContent = true;
            MouseFilter = MouseFilterMode.Ignore;
        }

        public void SetState(string header, string balance, string body, string buffer, bool input)
        {
            _header = header;
            _balance = balance;
            _buffer = buffer;
            _input = input;

            var wrapped = Wrap(body);
            if (wrapped != _body)
            {
                _body = wrapped;
                _shown = 0;
                _charTimer = 0f;
            }
        }

        private string Wrap(string text)
        {
            var maxCols = Math.Max(8, (int) ((_widthVirtual - 12f) / _advanceVirtual));
            var sb = new StringBuilder();
            var paragraphs = text.Split('\n');
            for (var p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                    sb.Append('\n');

                var col = 0;
                foreach (var word in paragraphs[p].Split(' '))
                {
                    if (col > 0 && col + 1 + word.Length > maxCols)
                    {
                        sb.Append('\n');
                        col = 0;
                    }
                    else if (col > 0)
                    {
                        sb.Append(' ');
                        col++;
                    }

                    if (word.Length > maxCols)
                    {
                        foreach (var ch in word)
                        {
                            if (col >= maxCols)
                            {
                                sb.Append('\n');
                                col = 0;
                            }
                            sb.Append(ch);
                            col++;
                        }
                    }
                    else
                    {
                        sb.Append(word);
                        col += word.Length;
                    }
                }
            }
            return sb.ToString();
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            var dt = args.DeltaSeconds;

            _caretTimer += dt;
            if (_caretTimer >= CaretBlink)
            {
                _caretTimer -= CaretBlink;
                _caretOn = !_caretOn;
            }

            if (_shown < _body.Length)
            {
                _charTimer += dt;
                while (_charTimer >= CharInterval && _shown < _body.Length)
                {
                    _charTimer -= CharInterval;
                    _shown++;
                }
            }
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var scale = UIScale;
            var inset = 6f * scale;
            var x = inset;
            var y = inset;

            // Header line + right-aligned balance.
            if (_header.Length > 0)
            {
                handle.DrawString(_bold, new Vector2(x, y), _header, scale, Green);
                if (_balance.Length > 0)
                {
                    var bw = TextWidth(_bold, _balance.Length, scale);
                    handle.DrawString(_bold, new Vector2(PixelSize.X - inset - bw, y), _balance, scale, Dim);
                }
                y += _bold.GetLineHeight(scale);
                handle.DrawRect(new UIBox2(x, y, PixelSize.X - inset, y + Math.Max(1f, scale)), Dim);
                y += 5f * scale;
            }

            // Body (typed out) plus the live input echo.
            var clamped = Math.Clamp(_shown, 0, _body.Length);
            var draw = _body[..clamped];
            if (_input && clamped >= _body.Length)
                draw += "\n> " + _buffer;

            handle.DrawString(_font, new Vector2(x, y), draw, scale, Green);

            // Block caret drawn at the end of the visible text.
            if (_caretOn)
            {
                var advance = _font.GetCharMetrics(new System.Text.Rune('M'), scale)?.Advance ?? (int) (_advanceVirtual * scale);
                var lineHeight = _font.GetLineHeight(scale);
                var lastNl = draw.LastIndexOf('\n');
                var lastLine = lastNl < 0 ? draw : draw[(lastNl + 1)..];
                var lineCount = 1;
                foreach (var ch in draw)
                {
                    if (ch == '\n')
                        lineCount++;
                }
                var caretX = x + lastLine.Length * advance;
                var caretY = y + (lineCount - 1) * lineHeight;
                handle.DrawRect(new UIBox2(caretX, caretY + 2f * scale, caretX + advance, caretY + lineHeight), Green);
            }

            // Phosphor tint + scanlines over the whole screen.
            var box = PixelSizeBox;
            handle.DrawRect(box, Tint);
            var step = Math.Max(2f, 3f * scale);
            for (var sy = 0f; sy < box.Bottom; sy += step)
                handle.DrawRect(new UIBox2(0f, sy, box.Right, sy + scale), Scan);
        }

        private static float TextWidth(Font font, int chars, float scale)
        {
            var advance = font.GetCharMetrics(new System.Text.Rune('M'), scale)?.Advance ?? 7;
            return chars * advance;
        }
    }
}
