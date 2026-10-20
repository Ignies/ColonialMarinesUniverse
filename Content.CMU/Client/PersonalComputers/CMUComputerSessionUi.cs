using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>The screen a terminal waits on until an ID card is swiped: its number and a blinking prompt.</summary>
public sealed class CMUComputerLogin : BoxContainer
{
    private readonly Label _prompt;
    private float _blink;

    public CMUComputerLogin(CMUComputerTheme theme, int terminalId)
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        VerticalAlignment = VAlignment.Center;
        HorizontalAlignment = HAlignment.Center;
        SeparationOverride = 14;

        AddChild(theme.Label(theme.DisplayName, theme.GetFont(CMUNetFont.Display, 22, true), theme.Accent));
        AddChild(new CMUTerminalRule { Color = theme.Accent });
        AddChild(theme.Label(Loc.GetString("cmu-pc-login-title", ("id", terminalId)), theme.Bold));
        _prompt = theme.Label(Loc.GetString("cmu-pc-login-prompt"), theme.Bold, theme.Phosphor);
        AddChild(_prompt);
        foreach (var child in Children)
        {
            if (child is Label label)
                label.HorizontalAlignment = HAlignment.Center;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _blink += args.DeltaSeconds;
        _prompt.Visible = _blink % 1.2f < 0.8f;
    }
}

/// <summary>The multitool's radial menu on a computer: read the logs, hack the terminal, or retune its network.</summary>
public sealed class CMUComputerToolBui : BoundUserInterface
{
    private SimpleRadialMenu? _menu;

    public CMUComputerToolBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var options = new List<RadialMenuOptionBase>();
        if (cfg.GetCVar(CMUPersonalComputerCVars.Logs))
            options.Add(Option(CMUComputerToolAction.Logs, "cmu-pc-tool-logs", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))));

        if (cfg.GetCVar(CMUPersonalComputerCVars.DeepWeb)
            && EntMan.HasComponent<Shared.CMU14.PersonalComputers.DeepWeb.CMUDeepWebHackableComponent>(Owner))
            options.Add(Option(CMUComputerToolAction.Hack, "cmu-pc-tool-hack", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/zap.svg.192dpi.png"))));

        if (cfg.GetCVar(CMUPersonalComputerCVars.Retune)
            && EntMan.HasComponent<Shared.CMU14.PersonalComputers.DeepWeb.CMUDeepWebHackableComponent>(Owner)
            && EntMan.TryGetComponent(Owner, out CMUPersonalComputerComponent? computer))
        {
            var label = computer.Dark ? "cmu-pc-tool-retune-colony" : "cmu-pc-tool-retune-dark";
            options.Add(Option(CMUComputerToolAction.Retune, label, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/signal.svg.192dpi.png"))));
        }

        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();
    }

    private RadialMenuActionOption<CMUComputerToolAction> Option(CMUComputerToolAction action, string label, SpriteSpecifier icon)
    {
        return new RadialMenuActionOption<CMUComputerToolAction>(a => SendMessage(new CMUComputerToolMessage(a)), action)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(icon),
            ToolTip = Loc.GetString(label),
        };
    }
}

/// <summary>
/// A terminal's maintenance log, newest last, in a plain terminal window. Skilled engineers also get to wipe it,
/// which leaves its own line, or slip a made up line into it.
/// </summary>
public sealed class CMUComputerLogBui : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _lines;
    private BoxContainer? _tools;

    public CMUComputerLogBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.MinSize = new Vector2(520, 420);
        _lines = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(10), SeparationOverride = 2 };
        _tools = BuildTools();
        _window.Contents.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#050A06") },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Children =
                    {
                        new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _lines } },
                        _tools,
                    },
                },
            },
        });
    }

    private BoxContainer BuildTools()
    {
        var wipe = new Button { Text = Loc.GetString("cmu-pc-logs-wipe") };
        wipe.OnPressed += _ => SendMessage(new CMUComputerLogWipeMessage());
        var line = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-pc-logs-forge-placeholder") };
        line.IsValid = text => text.Length <= CMUComputerLimits.ForgedLength;
        var forge = new Button { Text = Loc.GetString("cmu-pc-logs-forge") };
        forge.OnPressed += _ =>
        {
            if (line.Text.Trim().Length == 0)
                return;

            SendMessage(new CMUComputerLogForgeMessage(line.Text));
            line.Text = string.Empty;
        };

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(10, 4),
            SeparationOverride = 6,
            Visible = false,
            Children = { wipe, line, forge },
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not CMUComputerLogState log || _window == null || _lines == null)
            return;

        _window.Title = Loc.GetString("cmu-pc-logs-title", ("id", log.TerminalId));
        if (_tools != null)
            _tools.Visible = log.CanTamper;
        _lines.RemoveAllChildren();
        if (log.Entries.Count == 0)
            _lines.AddChild(new Label { Text = Loc.GetString("cmu-pc-logs-empty"), FontColorOverride = Color.FromHex("#3FE05A") });

        foreach (var entry in log.Entries)
        {
            // The same shift clock as the desktop: the round starts at 06:00.
            var time = TimeSpan.FromHours(6) + entry.Time;
            _lines.AddChild(new Label
            {
                Text = $"[{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}] {entry.Text}",
                FontColorOverride = Color.FromHex("#3FE05A"),
            });
        }
    }
}
