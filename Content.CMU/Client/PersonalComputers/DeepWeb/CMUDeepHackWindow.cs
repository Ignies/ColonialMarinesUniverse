using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// The access tuner: an oscilloscope where three dials bring the tuner's wave onto the terminal's carrier, then
/// a circuit board whose tiles turn to carry it to the core. A retune is only the dials. A trace bar runs across
/// the top the whole time. Every action goes to the server, which answers with the next state.
/// </summary>
public sealed partial class CMUDeepHackWindow : DefaultWindow
{
    public static readonly Color Green = Color.FromHex("#3FE05A");
    public static readonly Color Dim = Color.FromHex("#1F8A33");
    public static readonly Color Back = Color.FromHex("#050A06");
    public static readonly Color Amber = Color.FromHex("#FFB000");
    public static readonly Color Red = Color.FromHex("#E0303A");

    [Dependency] private IGameTiming _timing = default!;

    private readonly Font _font;
    private readonly Label _header;
    private readonly CMUDeepTraceBar _trace;
    private readonly Label _help;
    private readonly BoxContainer _stage;
    private readonly BoxContainer _log;
    private CMUDeepHackStage? _shownStage;
    private CMUDeepScope? _scope;
    private CMUDeepBoard? _board;
    private readonly int[] _dials = { 1, 1, 0 };

    public event Action<int, int, int>? OnLock;
    public event Action<int>? OnRotate;

    public CMUDeepHackWindow()
    {
        IoCManager.InjectDependencies(this);
        _font = IoCManager.Resolve<IResourceCache>().GetFont("/Fonts/RobotoMono/RobotoMono-Regular.ttf", 13);
        Title = Loc.GetString("cmu-deep-hack-title");
        MinSize = new Vector2(640, 560);

        _header = Text(Loc.GetString("cmu-deep-hack-header"), Green);
        _trace = new CMUDeepTraceBar { MinHeight = 18, HorizontalExpand = true };
        _help = Text(string.Empty, Dim);
        _stage = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        _log = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };

        Contents.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Back },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(14, 10),
                    SeparationOverride = 8,
                    Children = { _header, _trace, _help, _stage, new CMUTerminalRule { Color = Dim }, _log },
                },
            },
        });
    }

    public void UpdateState(CMUDeepHackState state)
    {
        _header.Text = Loc.GetString(!state.Retune ? "cmu-deep-hack-header"
            : state.ToDark ? "cmu-pc-retune-header-dark" : "cmu-pc-retune-header-colony");
        _trace.Set(state.TraceStarted, state.TraceEnds, state.LockedUntil, _font);

        _log.RemoveAllChildren();
        for (var i = Math.Max(0, state.Log.Count - 4); i < state.Log.Count; i++)
            _log.AddChild(Text(state.Log[i], Green));

        if (_timing.CurTime < state.LockedUntil)
        {
            _stage.RemoveAllChildren();
            _shownStage = null;
            _help.Text = string.Empty;
            _stage.AddChild(Text(Loc.GetString("cmu-deep-hack-locked-banner"), Red));
            return;
        }

        if (_shownStage != state.Stage)
        {
            _shownStage = state.Stage;
            _stage.RemoveAllChildren();
            if (state.Stage == CMUDeepHackStage.Tune)
                BuildTune();
            else
                BuildRoute();
        }

        _scope?.SetTarget(state.Frequency, state.Amplitude, state.Phase, state.Noise);
        _board?.SetState(state);
    }

    private void BuildTune()
    {
        _help.Text = Loc.GetString("cmu-deep-hack-tune-help");
        _board = null;
        _scope = new CMUDeepScope { MinHeight = 220, HorizontalExpand = true };
        _scope.SetTuner(_dials[0], _dials[1], _dials[2]);
        _stage.AddChild(_scope);

        var dials = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 18, Margin = new Thickness(0, 8) };
        dials.AddChild(Dial("cmu-deep-hack-frequency", 0, 1, CMUDeepHackPuzzle.MaxFrequency));
        dials.AddChild(Dial("cmu-deep-hack-amplitude", 1, 1, CMUDeepHackPuzzle.MaxAmplitude));
        dials.AddChild(Dial("cmu-deep-hack-phase", 2, 0, CMUDeepHackPuzzle.PhaseSteps - 1));

        var lockButton = TerminalButton(Loc.GetString("cmu-deep-hack-lock"));
        lockButton.OnPressed += _ => OnLock?.Invoke(_dials[0], _dials[1], _dials[2]);
        dials.AddChild(lockButton);
        _stage.AddChild(dials);
    }

    /// <summary>A dial as a value between two arrow buttons; it wraps around at either end.</summary>
    private Control Dial(string label, int index, int min, int max)
    {
        var value = Text(_dials[index].ToString(), Amber);
        value.MinWidth = 24;
        value.Align = Label.AlignMode.Center;

        void Turn(int by)
        {
            var range = max - min + 1;
            _dials[index] = (_dials[index] - min + by + range) % range + min;
            value.Text = _dials[index].ToString();
            _scope?.SetTuner(_dials[0], _dials[1], _dials[2]);
        }

        var down = TerminalButton("<");
        down.OnPressed += _ => Turn(-1);
        var up = TerminalButton(">");
        up.OnPressed += _ => Turn(1);
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children = { Text(Loc.GetString(label), Green), down, value, up },
        };
    }

    private void BuildRoute()
    {
        _help.Text = Loc.GetString("cmu-deep-hack-route-help");
        _scope = null;
        _board = new CMUDeepBoard(_font) { VerticalExpand = true, HorizontalExpand = true };
        _board.OnRotate += cell => OnRotate?.Invoke(cell);
        _stage.AddChild(_board);
    }

    private Label Text(string text, Color color)
    {
        return new Label { Text = text, FontOverride = _font, FontColorOverride = color };
    }

    private Button TerminalButton(string text)
    {
        var button = new Button { Text = text, ModulateSelfOverride = Color.White };
        button.StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Back, BorderColor = Green, BorderThickness = new Thickness(1) };
        button.Label.FontOverride = _font;
        button.Label.FontColorOverride = Green;
        return button;
    }
}

/// <summary>The security trace: fills as the trace closes in, turns red near the end.</summary>
public sealed partial class CMUDeepTraceBar : Control
{
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _started;
    private TimeSpan _ends;
    private TimeSpan _lockedUntil;
    private Font? _font;

    public CMUDeepTraceBar()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Set(TimeSpan started, TimeSpan ends, TimeSpan lockedUntil, Font font)
    {
        _started = started;
        _ends = ends;
        _lockedUntil = lockedUntil;
        _font = font;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var now = _timing.CurTime;
        var total = (_ends - _started).TotalSeconds;
        var done = now < _lockedUntil ? 1f : total <= 0 ? 0f : (float) Math.Clamp((now - _started).TotalSeconds / total, 0, 1);
        var color = done > 0.8f ? CMUDeepHackWindow.Red : done > 0.5f ? CMUDeepHackWindow.Amber : CMUDeepHackWindow.Green;

        handle.DrawRect(PixelSizeBox, CMUDeepHackWindow.Back);
        handle.DrawRect(new UIBox2(0, 0, PixelWidth * done, PixelHeight), color.WithAlpha(0.7f));
        handle.DrawRect(PixelSizeBox, CMUDeepHackWindow.Green, false);
        if (_font != null)
        {
            var text = Loc.GetString("cmu-deep-hack-trace", ("percent", (int) (done * 100)));
            handle.DrawString(_font, new Vector2(6 * UIScale, 1 * UIScale), text, UIScale, Color.White);
        }
    }
}

/// <summary>The oscilloscope: the carrier under static, the tuner's wave over it, and a grid.</summary>
public sealed class CMUDeepScope : Control
{
    private int _frequency = 1;
    private int _amplitude = 1;
    private int _phase;
    private float _noise;
    private int _tunerFrequency = 1;
    private int _tunerAmplitude = 1;
    private int _tunerPhase;
    private float _time;

    public void SetTarget(int frequency, int amplitude, int phase, float noise)
    {
        _frequency = frequency;
        _amplitude = amplitude;
        _phase = phase;
        _noise = noise;
    }

    public void SetTuner(int frequency, int amplitude, int phase)
    {
        _tunerFrequency = frequency;
        _tunerAmplitude = amplitude;
        _tunerPhase = phase;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, Color.FromHex("#020602"));
        var grid = CMUDeepHackWindow.Dim.WithAlpha(0.35f);
        for (var i = 1; i < 10; i++)
            handle.DrawLine(new Vector2(PixelWidth * i / 10f, 0), new Vector2(PixelWidth * i / 10f, PixelHeight), grid);
        for (var i = 1; i < 6; i++)
            handle.DrawLine(new Vector2(0, PixelHeight * i / 6f), new Vector2(PixelWidth, PixelHeight * i / 6f), grid);

        var middle = PixelHeight / 2f;
        var unit = PixelHeight / 2f / (CMUDeepHackPuzzle.MaxAmplitude + 1);
        var steps = Math.Max(64, (int) (PixelWidth / 2));

        // The carrier jitters with static that changes every frame, so it has to be read, not just copied.
        var noise = new System.Random((int) (_time * 12));
        var last = Vector2.Zero;
        for (var i = 0; i <= steps; i++)
        {
            var x = i / (float) steps;
            var y = CMUDeepHackPuzzle.Wave(_frequency, _amplitude, _phase, x) + (float) (noise.NextDouble() * 2 - 1) * _noise * 2.5f;
            var point = new Vector2(x * PixelWidth, middle - y * unit);
            if (i > 0)
                handle.DrawLine(last, point, CMUDeepHackWindow.Dim);
            last = point;
        }

        var matched = _frequency == _tunerFrequency && _amplitude == _tunerAmplitude && _phase == _tunerPhase;
        var tuner = matched ? Color.White : CMUDeepHackWindow.Amber;
        for (var i = 0; i <= steps; i++)
        {
            var x = i / (float) steps;
            var point = new Vector2(x * PixelWidth, middle - CMUDeepHackPuzzle.Wave(_tunerFrequency, _tunerAmplitude, _tunerPhase, x) * unit);
            if (i > 0)
                handle.DrawLine(last, point, tuner);
            last = point;
        }

        handle.DrawRect(PixelSizeBox, CMUDeepHackWindow.Green, false);
    }
}

/// <summary>The circuit board: trace tiles drawn as lines, lit where the carrier reaches. Click a tile to turn it.</summary>
public sealed class CMUDeepBoard : Control
{
    private readonly Font _font;
    private CMUDeepHackState? _state;
    private bool[] _powered = Array.Empty<bool>();

    public event Action<int>? OnRotate;

    public CMUDeepBoard(Font font)
    {
        _font = font;
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(400, 260);
    }

    public void SetState(CMUDeepHackState state)
    {
        _state = state;
        _powered = CMUDeepHackPuzzle.Powered(state.Cells, state.Width, state.Height, state.SourceRow);
    }

    /// <summary>Tiles are square and centred, with room on each side for the tuner and core labels.</summary>
    private (float Size, Vector2 Origin) Layout()
    {
        if (_state == null)
            return (0, Vector2.Zero);

        var margin = 70 * UIScale;
        var size = MathF.Min((PixelWidth - margin * 2) / _state.Width, PixelHeight / _state.Height);
        var origin = new Vector2((PixelWidth - size * _state.Width) / 2f, (PixelHeight - size * _state.Height) / 2f);
        return (size, origin);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || _state == null)
            return;

        var (size, origin) = Layout();
        var local = args.RelativePixelPosition - origin;
        var x = (int) MathF.Floor(local.X / size);
        var y = (int) MathF.Floor(local.Y / size);
        if (x < 0 || y < 0 || x >= _state.Width || y >= _state.Height)
            return;

        args.Handle();
        OnRotate?.Invoke(y * _state.Width + x);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_state == null)
            return;

        var (size, origin) = Layout();
        var thick = MathF.Max(2, size * 0.14f);
        for (var i = 0; i < _state.Cells.Count; i++)
        {
            var cell = _state.Cells[i];
            var box = UIBox2.FromDimensions(origin + new Vector2(i % _state.Width, i / _state.Width) * size, new Vector2(size, size));
            handle.DrawRect(box, cell.Locked ? Color.FromHex("#1A0606") : Color.FromHex("#030A04"));
            handle.DrawRect(box, CMUDeepHackWindow.Dim.WithAlpha(0.4f), false);

            var lit = i < _powered.Length && _powered[i];
            var color = cell.Locked ? CMUDeepHackWindow.Red : lit ? Color.White : CMUDeepHackWindow.Green;
            var mask = CMUDeepHackPuzzle.Connections(cell);
            var c = box.Center;
            if ((mask & CMUDeepHackPuzzle.North) != 0)
                handle.DrawRect(new UIBox2(c.X - thick / 2, box.Top, c.X + thick / 2, c.Y + thick / 2), color);
            if ((mask & CMUDeepHackPuzzle.South) != 0)
                handle.DrawRect(new UIBox2(c.X - thick / 2, c.Y - thick / 2, c.X + thick / 2, box.Bottom), color);
            if ((mask & CMUDeepHackPuzzle.East) != 0)
                handle.DrawRect(new UIBox2(c.X - thick / 2, c.Y - thick / 2, box.Right, c.Y + thick / 2), color);
            if ((mask & CMUDeepHackPuzzle.West) != 0)
                handle.DrawRect(new UIBox2(box.Left, c.Y - thick / 2, c.X + thick / 2, c.Y + thick / 2), color);
            if (mask != 0)
                handle.DrawRect(new UIBox2(c.X - thick, c.Y - thick, c.X + thick, c.Y + thick), color);
        }

        // The tuner feeds in from the left, the core waits on the right.
        var source = origin + new Vector2(0, (_state.SourceRow + 0.5f) * size);
        handle.DrawRect(new UIBox2(source.X - 18 * UIScale, source.Y - thick / 2, source.X, source.Y + thick / 2), Color.White);
        handle.DrawString(_font, source - new Vector2(68 * UIScale, 9 * UIScale), Loc.GetString("cmu-deep-hack-source"), UIScale, CMUDeepHackWindow.Amber);

        var core = origin + new Vector2(_state.Width * size, (_state.CoreRow + 0.5f) * size);
        var coreLit = _powered.Length > 0 && _powered[_state.CoreRow * _state.Width + _state.Width - 1];
        handle.DrawRect(new UIBox2(core.X, core.Y - thick / 2, core.X + 18 * UIScale, core.Y + thick / 2), coreLit ? Color.White : CMUDeepHackWindow.Dim);
        handle.DrawString(_font, core + new Vector2(22 * UIScale, -9 * UIScale), Loc.GetString("cmu-deep-hack-core"), UIScale, CMUDeepHackWindow.Amber);
    }
}
