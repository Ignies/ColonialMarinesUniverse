using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// YouTape Reels: <c>&lt;CMUNetReels/&gt;</c>. Short tall tapes, one after another: each plays once and the next
/// comes on by itself, or flick with UP and DOWN. Beside the screen, the reel's channel, its likes and its hook.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetReels : BoxContainer, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly CMUTapePlayer _player = new(CMUTapePlayer.Portrait);
    private readonly Label _channel;
    private readonly Label _likes;
    private readonly Label _hook;
    private readonly List<int> _seen = new();
    private readonly Random _random = new();
    private int _at = -1;

    public CMUNetReels()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 10;
        Margin = new Thickness(0, 6);

        _channel = new Label { FontColorOverride = Color.White };
        _likes = new Label { FontColorOverride = Color.FromHex("#FF2A2A") };
        _hook = new Label { FontColorOverride = Color.FromHex("#AAAAAA") };
        var up = Button(Loc.GetString("cmu-tape-reel-up"));
        up.OnPressed += _ => Show(_at - 1);
        var down = Button(Loc.GetString("cmu-tape-reel-down"));
        down.OnPressed += _ => Show(_at + 1);

        AddChild(_player);
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            VerticalAlignment = VAlignment.Bottom,
            Children =
            {
                up,
                down,
                new Control { MinHeight = 10 },
                _channel,
                _likes,
                _hook,
            },
        });
        _player.OnEnded += () => Show(_at + 1);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_at < 0)
            Show(0);
    }

    /// <summary>Flicks to the next reel, as DOWN does.</summary>
    public void Next()
    {
        Show(_at + 1);
    }

    /// <summary>Shows the reel at this place in the feed, making new ones as the feed goes on.</summary>
    private void Show(int index)
    {
        if (index < 0 || !_prototypes.TryIndex(CMUNetTape.Grammar, out var grammar))
            return;

        while (_seen.Count <= index)
            _seen.Add(_random.Next());

        _at = index;
        var colony = Browser()?.ColonyName ?? Loc.GetString("cmu-tape-default-colony");
        var reel = CMUTapeGenerator.Reel(_seen[index], grammar, colony);
        _player.Load(reel);
        _channel.Text = $"@{reel.Channel}";
        _likes.Text = Loc.GetString("cmu-tape-reel-likes", ("likes", reel.Rewinds));
        _hook.Text = reel.Title;
    }

    private static Button Button(string text)
    {
        var button = new Button
        {
            Text = text,
            MinWidth = 70,
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#FF2A2A"),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
        };
        button.Label.FontColorOverride = Color.White;
        return button;
    }

    private CMUNetBrowser? Browser()
    {
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is CMUNetBrowser browser)
                return browser;
        }

        return null;
    }
}
