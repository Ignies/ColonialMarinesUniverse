using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Ghost.Components;
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
/// Shows ghosts the questions colonists ask SpaceGPT, with the answers they can vote for. The window closes on
/// a vote or when voting ends. Ghosts can turn these off with <c>cmu.pc.spacegpt_ghost_polls</c>.
/// </summary>
public sealed partial class CMUSpaceGptGhostSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly List<(DefaultWindow Window, TimeSpan Ends)> _open = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUSpaceGptPollEvent>(OnPoll);
    }

    private void OnPoll(CMUSpaceGptPollEvent ev)
    {
        if (!_cfg.GetCVar(CMUSpaceGptCVars.GhostPolls) || !HasComp<GhostComponent>(_player.LocalEntity))
            return;

        var window = new DefaultWindow { Title = Loc.GetString("cmu-spacegpt-ghost-title"), MinSize = new Vector2(380, 120) };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 6 };
        box.AddChild(new Label { Text = Loc.GetString("cmu-spacegpt-ghost-intro") });
        var question = new RichTextLabel { HorizontalExpand = true };
        question.SetMessage(FormattedMessage.FromUnformatted($"\"{ev.Question}\""));
        box.AddChild(question);

        for (var i = 0; i < ev.Answers.Count; i++)
        {
            var index = i;
            var button = new Button { Text = ev.Answers[i], HorizontalExpand = true, ClipText = true };
            button.OnPressed += _ =>
            {
                RaiseNetworkEvent(new CMUSpaceGptVoteEvent(ev.Poll, index));
                window.Close();
            };
            box.AddChild(button);
        }

        window.Contents.AddChild(box);
        window.OpenToLeft();
        _open.Add((window, ev.Ends));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        for (var i = _open.Count - 1; i >= 0; i--)
        {
            var (window, ends) = _open[i];
            if (window.IsOpen && _timing.CurTime < ends)
                continue;

            window.Close();
            _open.RemoveAt(i);
        }
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
            _host.SpaceGptAnswered += OnAnswer;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.SpaceGptAnswered -= OnAnswer;
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
        _log.AddChild(new Label { Text = Loc.GetString("cmu-spacegpt-thinking") });
        _host.AskSpaceGpt(text);
    }

    private void OnAnswer(CMUSpaceGptAnswerMessage message)
    {
        _waiting = false;
        _ask.Disabled = false;
        if (_log.ChildCount > 0)
            _log.RemoveChild(_log.GetChild(_log.ChildCount - 1));

        _log.AddChild(Line(Loc.GetString("cmu-spacegpt-answer", ("answer", message.Answer))));
        _log.AddChild(new Label
        {
            Text = message.Votes > 0
                ? Loc.GetString("cmu-spacegpt-votes", ("votes", message.Votes))
                : Loc.GetString("cmu-spacegpt-no-votes"),
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
