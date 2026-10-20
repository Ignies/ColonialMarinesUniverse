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
/// YouTape's player on a page: <c>&lt;CMUNetTape/&gt;</c>. Plays a random tape straight away, with buttons to play,
/// rewind and put on another, a shelf of more random tapes by title, and the broadcasting cameras on air now, each
/// one opening the TV network on the terminal. Add <c>Live="false"</c> to leave the live list out, and
/// <c>Shelf="0"</c> to leave the shelf out.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetTape : BoxContainer, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public static readonly ProtoId<CMUTapeGrammarPrototype> Grammar = "CMUTapeGrammar";

    private readonly CMUTapePlayer _player;
    private readonly Label _title;
    private readonly Label _byline;
    private readonly Button _play;
    private readonly BoxContainer _shelf;
    private readonly BoxContainer _live;
    private readonly Random _random = new();
    private int _shelfSize = 6;
    private bool _showLive = true;
    private ICMUComputerHost? _host;

    public CMUNetTape()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(0, 6);
        SeparationOverride = 4;

        _player = new CMUTapePlayer { HorizontalAlignment = HAlignment.Left };
        _title = new Label { FontColorOverride = Color.FromHex("#FF2A2A"), ClipText = true };
        _byline = new Label { FontColorOverride = Color.FromHex("#AAAAAA"), ClipText = true };

        var rewind = DeckButton(Loc.GetString("cmu-tape-rewind"));
        rewind.OnPressed += _ => _player.Rewind();
        _play = DeckButton(Loc.GetString("cmu-tape-pause-button"));
        _play.OnPressed += _ =>
        {
            _player.TogglePlay();
            UpdatePlay();
        };
        var next = DeckButton(Loc.GetString("cmu-tape-next"));
        next.OnPressed += _ => Play(_random.Next());

        _shelf = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2 };
        _live = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2 };

        AddChild(_player);
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children = { rewind, _play, next },
        });
        AddChild(_title);
        AddChild(_byline);
        _player.OnEnded += UpdatePlay;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        if (args.TryGetValue("Shelf", out var shelf) && int.TryParse(shelf, out var size))
            _shelfSize = Math.Clamp(size, 0, 12);

        _showLive = !args.TryGetValue("Live", out var live) || !live.Equals("false", StringComparison.OrdinalIgnoreCase);
        if (_shelfSize > 0)
        {
            AddChild(Heading(Loc.GetString("cmu-tape-shelf")));
            AddChild(_shelf);
        }

        if (_showLive)
        {
            AddChild(Heading(Loc.GetString("cmu-tape-live")));
            AddChild(_live);
        }

        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_player.Tape == null)
            Play(_random.Next());

        FillShelf();
        _host = Browser()?.Host;
        if (_showLive && _host != null)
        {
            _host.TapeLiveReceived += OnLive;
            _host.RequestTapeLive();
            SetLive(null);
        }
        else if (_showLive)
        {
            SetLive(new List<CMUTapeLiveFeed>());
        }
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.TapeLiveReceived -= OnLive;

        _host = null;
    }

    /// <summary>Plays the first tape of this genre from the seeds after this one, wound to a scene kind, for previews.</summary>
    public void Preview(string genre, string kind, int from)
    {
        for (var seed = from; seed < from + 500; seed++)
        {
            if (Make(seed) is not { } tape || tape.Genre != genre)
                continue;

            Play(seed);
            var at = 0f;
            foreach (var scene in tape.Scenes)
            {
                if (scene.Kind == kind)
                {
                    _player.Seek(at + scene.Duration * 0.6f);
                    return;
                }

                at += scene.Duration;
            }

            return;
        }
    }

    private void Play(int seed)
    {
        if (Make(seed) is not { } tape)
            return;

        _player.Load(tape);
        _title.Text = tape.Title;
        _byline.Text = Loc.GetString("cmu-tape-byline", ("channel", tape.Channel), ("rewinds", tape.Rewinds));
        UpdatePlay();
    }

    private CMUTape? Make(int seed)
    {
        if (!_prototypes.TryIndex(Grammar, out var grammar))
            return null;

        var colony = Browser()?.ColonyName ?? Loc.GetString("cmu-tape-default-colony");
        return CMUTapeGenerator.Generate(seed, grammar, colony);
    }

    private void UpdatePlay()
    {
        _play.Text = Loc.GetString(_player.Playing ? "cmu-tape-pause-button" : "cmu-tape-play-button");
    }

    /// <summary>More tapes, each a random seed shown by its title; picking one plays it.</summary>
    private void FillShelf()
    {
        _shelf.RemoveAllChildren();
        for (var i = 0; i < _shelfSize; i++)
        {
            var seed = _random.Next();
            if (Make(seed) is not { } tape)
                return;

            var pick = Styled(Loc.GetString("cmu-tape-shelf-entry", ("title", tape.Title), ("channel", tape.Channel)),
                Color.FromHex("#242424"), Color.FromHex("#EEEEEE"));
            pick.HorizontalExpand = true;
            pick.Label.HorizontalAlignment = HAlignment.Left;
            pick.OnPressed += _ =>
            {
                Play(seed);
                FillShelf();
            };
            _shelf.AddChild(pick);
        }
    }

    private void OnLive(CMUTapeLiveStateMessage message)
    {
        SetLive(message.Feeds);
    }

    /// <summary>Lists what is on air; null while the terminal is still asking.</summary>
    private void SetLive(List<CMUTapeLiveFeed>? feeds)
    {
        _live.RemoveAllChildren();
        var refresh = Styled(Loc.GetString("cmu-tape-live-refresh"), Color.FromHex("#242424"), Color.FromHex("#EEEEEE"));
        refresh.HorizontalAlignment = HAlignment.Left;
        refresh.OnPressed += _ =>
        {
            SetLive(null);
            _host?.RequestTapeLive();
        };

        if (feeds == null)
        {
            _live.AddChild(new Label { Text = Loc.GetString("cmu-tape-live-asking"), FontColorOverride = Color.FromHex("#AAAAAA") });
            return;
        }

        if (feeds.Count == 0)
            _live.AddChild(new Label { Text = Loc.GetString("cmu-tape-live-none"), FontColorOverride = Color.FromHex("#AAAAAA") });

        foreach (var feed in feeds)
        {
            var who = feed.Holder.Length > 0
                ? Loc.GetString("cmu-tape-live-feed", ("holder", feed.Holder), ("location", feed.Location))
                : Loc.GetString("cmu-tape-live-feed-unattended", ("location", feed.Location));
            var watch = DeckButton(Loc.GetString("cmu-tape-live-watch"));
            var camera = feed.Camera;
            watch.OnPressed += _ => _host?.WatchTapeLive(camera);
            _live.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                Children =
                {
                    new Label { Text = Loc.GetString("cmu-tape-live-dot"), FontColorOverride = Color.FromHex("#FF2A2A") },
                    new Label { Text = who, ClipText = true, HorizontalExpand = true },
                    watch,
                },
            });
        }

        _live.AddChild(refresh);
    }

    /// <summary>A button in YouTape's red, since page buttons have no face of their own.</summary>
    private static Button DeckButton(string text)
    {
        return Styled(text, Color.FromHex("#FF2A2A"), Color.White);
    }

    private static Button Styled(string text, Color face, Color ink)
    {
        var button = new Button
        {
            Text = text,
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = face,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
        };
        button.Label.FontColorOverride = ink;
        button.ModulateSelfOverride = Color.White;
        button.OnMouseEntered += _ => button.ModulateSelfOverride = new Color(0.85f, 0.85f, 0.85f);
        button.OnMouseExited += _ => button.ModulateSelfOverride = Color.White;
        return button;
    }

    private static Label Heading(string text)
    {
        return new Label { Text = text, FontColorOverride = Color.FromHex("#FF2A2A"), Margin = new Thickness(0, 6, 0, 0) };
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
