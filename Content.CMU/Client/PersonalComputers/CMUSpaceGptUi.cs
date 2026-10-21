using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Lets ghosts answer the questions colonists ask SpaceGPT, without throwing them in their face: a small button at
/// the top of the screen says SpaceGPT has questions, and how many. Clicking it lists every one waiting, each with
/// ANSWER, which shows the answers to vote for, IGNORE, and FOLLOW, which takes the ghost to the terminal that asked.
/// A question goes when it is answered, ignored or its vote ends. Ghosts can turn these off with
/// <c>cmu.pc.spacegpt_ghost_polls</c>.
/// </summary>
public sealed partial class CMUSpaceGptGhostSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    /// <summary>Questions waiting for this ghost, oldest first.</summary>
    private readonly List<CMUSpaceGptPollEvent> _waiting = new();
    private Button? _alert;
    private DefaultWindow? _window;
    private BoxContainer? _list;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUSpaceGptPollEvent>(OnPoll);
        SubscribeNetworkEvent<CMUSpaceGptCancelEvent>(ev => Forget(ev.Poll));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _alert?.Orphan();
        _window?.Close();
    }

    private void OnPoll(CMUSpaceGptPollEvent ev)
    {
        if (!_cfg.GetCVar(CMUSpaceGptCVars.GhostPolls) || !HasComp<GhostComponent>(_player.LocalEntity))
            return;

        _waiting.Add(ev);
        Refresh();
    }

    /// <summary>The button at the top of the screen, and the list if it is open.</summary>
    private void Refresh()
    {
        if (_waiting.Count == 0 || !HasComp<GhostComponent>(_player.LocalEntity))
        {
            _alert?.Orphan();
            _alert = null;
            _window?.Close();
            _window = null;
            return;
        }

        if (_alert == null)
        {
            _alert = new Button { MinWidth = 260 };
            _alert.OnPressed += _ => OpenList();
            _ui.PopupRoot.AddChild(_alert);
        }

        _alert.Text = _waiting.Count == 1
            ? Loc.GetString("cmu-spacegpt-ghost-alert")
            : Loc.GetString("cmu-spacegpt-ghost-alert-many", ("count", _waiting.Count));
        PlaceAlert();
        if (_window is { IsOpen: true })
            FillList();
    }

    private void PlaceAlert()
    {
        if (_alert != null)
            LayoutContainer.SetPosition(_alert, new Vector2((_ui.PopupRoot.Size.X - _alert.MinWidth) / 2f, 8));
    }

    /// <summary>Every question waiting, newest at the top.</summary>
    private void OpenList()
    {
        if (_window is { IsOpen: true })
        {
            _window.MoveToFront();
            return;
        }

        _window = new DefaultWindow { Title = Loc.GetString("cmu-spacegpt-ghost-title"), MinSize = new Vector2(440, 200), SetSize = new Vector2(460, 420) };
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 10 };
        _window.Contents.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _list } });
        _window.OnClose += () => _window = null;
        FillList();
        _window.OpenCentered();
    }

    private void FillList()
    {
        if (_list == null)
            return;

        _list.RemoveAllChildren();
        _list.AddChild(new Label { Text = Loc.GetString("cmu-spacegpt-ghost-intro") });
        for (var i = _waiting.Count - 1; i >= 0; i--)
            _list.AddChild(Entry(_waiting[i]));
    }

    /// <summary>One question: what was asked, then ANSWER, IGNORE and FOLLOW; ANSWER opens its answers in place.</summary>
    private Control Entry(CMUSpaceGptPollEvent poll)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var question = new RichTextLabel { HorizontalExpand = true };
        question.SetMessage(FormattedMessage.FromUnformatted($"\"{poll.Question}\""));
        box.AddChild(question);

        var answers = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Visible = false };
        for (var i = 0; i < poll.Answers.Count; i++)
        {
            var choice = i;
            var button = new Button { Text = poll.Answers[i], HorizontalExpand = true, ClipText = true };
            button.OnPressed += _ =>
            {
                RaiseNetworkEvent(new CMUSpaceGptVoteEvent(poll.Poll, choice));
                Forget(poll.Poll);
            };
            answers.AddChild(button);
        }

        var answer = new Button { Text = Loc.GetString("cmu-spacegpt-ghost-answer"), HorizontalExpand = true };
        var ignore = new Button { Text = Loc.GetString("cmu-spacegpt-ghost-ignore"), HorizontalExpand = true };
        var follow = new Button { Text = Loc.GetString("cmu-spacegpt-ghost-follow"), HorizontalExpand = true };
        answer.OnPressed += _ => answers.Visible = !answers.Visible;
        ignore.OnPressed += _ => Forget(poll.Poll);
        follow.OnPressed += _ => RaiseNetworkEvent(new GhostWarpToTargetRequestEvent(poll.Terminal));

        box.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children = { answer, ignore, follow },
        });
        box.AddChild(answers);
        return box;
    }

    /// <summary>Done with a question: answered, ignored or over.</summary>
    private void Forget(int poll)
    {
        _waiting.RemoveAll(p => p.Poll == poll);
        Refresh();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_waiting.Count == 0)
            return;

        // Moving from ghost back into a body takes the questions away.
        if (!HasComp<GhostComponent>(_player.LocalEntity))
        {
            _waiting.Clear();
            Refresh();
            return;
        }

        var now = _timing.CurTime;
        if (_waiting.RemoveAll(p => now >= p.Ends) > 0)
            Refresh();
        else
            PlaceAlert();
    }
}

/// <summary>
/// The SpaceGPT chat on its page: <c>&lt;CMUNetSpaceGpt/&gt;</c>. Questions go to the server; answers come back
/// after the dead have voted on them.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetSpaceGpt : BoxContainer, IDocumentTag
{
    private readonly BoxContainer _log;
    private readonly LineEdit _question;
    private readonly Button _ask;
    private ICMUComputerHost? _host;
    private bool _waiting;
    private Label? _thinking;
    private bool _held;
    private float _stall;
    private int _stallLine;

    /// <summary>What SpaceGPT pretends to be doing while an admin holds the question, a line every few seconds.</summary>
    private static readonly string[] Stalling =
    {
        "cmu-spacegpt-stall-1", "cmu-spacegpt-stall-2", "cmu-spacegpt-stall-3", "cmu-spacegpt-stall-4",
        "cmu-spacegpt-stall-5", "cmu-spacegpt-stall-6", "cmu-spacegpt-stall-7", "cmu-spacegpt-stall-8",
    };

    public CMUNetSpaceGpt()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 6;
        Margin = new Thickness(0, 6);

        _log = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
        _question = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-spacegpt-placeholder") };
        _question.IsValid = text => text.Length <= CMUSpaceGptLimits.QuestionLength;
        _question.OnTextEntered += _ => Ask();
        _ask = new Button { Text = Loc.GetString("cmu-spacegpt-ask") };
        _ask.OnPressed += _ => Ask();
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.35f), BorderColor = Color.White.WithAlpha(0.3f), BorderThickness = new Thickness(1) },
            MinHeight = 160,
            Children = { new ScrollContainer { HScrollEnabled = false, Children = { _log } } },
        });
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children = { _question, _ask },
        });
        _log.AddChild(new Label { Text = Loc.GetString("cmu-spacegpt-greeting") });
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is CMUNetBrowser browser)
            {
                _host = browser.Host;
                break;
            }
        }

        if (_host != null)
        {
            _host.SpaceGptAnswered += OnAnswer;
            _host.SpaceGptHeld += OnHeld;
        }
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
        {
            _host.SpaceGptAnswered -= OnAnswer;
            _host.SpaceGptHeld -= OnHeld;
        }
    }

    private void OnHeld(CMUSpaceGptHoldMessage message)
    {
        _held = message.Held && _waiting;
        _stall = 0;
        if (!_held && _thinking != null)
            _thinking.Text = Loc.GetString("cmu-spacegpt-thinking");
    }

    protected override void FrameUpdate(Robust.Shared.Timing.FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_held || _thinking == null)
            return;

        _stall -= args.DeltaSeconds;
        if (_stall > 0)
            return;

        _stall = 3f;
        _thinking.Text = Loc.GetString(Stalling[_stallLine++ % Stalling.Length]);
    }

    private void Ask()
    {
        var text = _question.Text.Trim();
        if (text.Length == 0 || _waiting || _host == null)
            return;

        _waiting = true;
        _ask.Disabled = true;
        _question.Text = string.Empty;
        _log.AddChild(Line(Loc.GetString("cmu-spacegpt-you", ("question", text))));
        _thinking = new Label { Text = Loc.GetString("cmu-spacegpt-thinking") };
        _log.AddChild(_thinking);
        _host.AskSpaceGpt(text);
    }

    private void OnAnswer(CMUSpaceGptAnswerMessage message)
    {
        _waiting = false;
        _held = false;
        _ask.Disabled = false;
        _thinking?.Orphan();
        _thinking = null;

        _log.AddChild(Line(Loc.GetString("cmu-spacegpt-answer", ("answer", message.Answer))));
        _log.AddChild(new Label
        {
            Text = message.Votes switch
            {
                > 0 => Loc.GetString("cmu-spacegpt-votes", ("votes", message.Votes)),
                < 0 => Loc.GetString("cmu-spacegpt-safeties"),
                _ => Loc.GetString("cmu-spacegpt-no-votes"),
            },
            FontColorOverride = Color.Gray,
        });
    }

    private static RichTextLabel Line(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }
}
