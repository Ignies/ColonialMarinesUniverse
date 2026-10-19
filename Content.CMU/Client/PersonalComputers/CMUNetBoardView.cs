using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Talks to the server about boards for the computer the browser runs on. The BUI implements it; previews
/// have none, and boards then show as offline.
/// </summary>
public interface ICMUNetBoardHost
{
    event Action<CMUNetBoardStateMessage>? StateReceived;

    void Request(ProtoId<CMUNetSitePrototype> site);

    void Post(ProtoId<CMUNetSitePrototype> site, string title, string body);

    void Comment(ProtoId<CMUNetSitePrototype> site, int post, string body);

    void Delete(ProtoId<CMUNetSitePrototype> site, int post);
}

/// <summary>
/// A board's postings under its index page: a composer for the roles allowed to post, each posting with its
/// comments, and a comment line for everyone else. Player text is drawn as plain text, never as markup.
/// </summary>
public sealed class CMUNetBoardView : BoxContainer
{
    private readonly CMUComputerTheme _theme;
    private readonly CMUNetPageStyle _style;
    private readonly CMUNetSitePrototype _site;
    private readonly ICMUNetBoardHost? _host;
    private readonly BoxContainer _composerSlot;
    private readonly BoxContainer _posts;
    private readonly Dictionary<int, string> _drafts = new();
    private readonly Dictionary<int, LineEdit> _commentFields = new();

    private bool _composerOpen;

    public event Action? OnClicked;
    public event Action? OnKeyTyped;

    public CMUNetBoardView(CMUComputerTheme theme, CMUNetPageStyle style, CMUNetSitePrototype site, ICMUNetBoardHost? host)
    {
        _theme = theme;
        _style = style;
        _site = site;
        _host = host;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 10;

        _composerSlot = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        _posts = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 14 };
        AddChild(new CMUTerminalRule { Color = Link.WithAlpha(0.5f), Margin = new Thickness(0, 4) });
        AddChild(_composerSlot);
        AddChild(_posts);

        if (host == null)
        {
            _posts.AddChild(new Label { Text = Loc.GetString("cmu-net-board-offline") });
            return;
        }

        _posts.AddChild(new Label { Text = Loc.GetString("cmu-net-board-loading") });
        host.StateReceived += OnState;
        host.Request(site.ID);
    }

    private Color Text => _style.Text ?? _theme.Phosphor;
    private Color Heading => _style.Heading ?? Text;
    private Color Link => _style.Link ?? _theme.Phosphor;
    private Color Background => _style.Background ?? _theme.Background;

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.StateReceived -= OnState;
    }

    private void OnState(CMUNetBoardStateMessage state)
    {
        if (state.Site != _site.ID)
            return;

        foreach (var (id, field) in _commentFields)
            _drafts[id] = field.Text;

        _commentFields.Clear();
        BuildComposer(state.CanPost);
        _posts.RemoveAllChildren();

        if (state.Posts.Count == 0)
            _posts.AddChild(new Label { Text = Loc.GetString(_site.Board!.Empty) });

        foreach (var post in state.Posts)
            _posts.AddChild(BuildPost(post, state));
    }

    private void BuildComposer(bool canPost)
    {
        _composerSlot.RemoveAllChildren();
        if (!canPost)
            return;

        var label = Loc.GetString(_site.Board!.PostLabel);
        if (!_composerOpen)
        {
            var open = PageButton($"+ {label}");
            open.HorizontalAlignment = HAlignment.Left;
            open.OnPressed += _ =>
            {
                _composerOpen = true;
                BuildComposer(true);
            };
            _composerSlot.AddChild(open);
            return;
        }

        var title = Field(Loc.GetString("cmu-net-board-title-placeholder"), CMUNetBoardLimits.TitleLength);
        var body = new TextEdit
        {
            HorizontalExpand = true,
            MinHeight = 110,
            Placeholder = new Rope.Leaf(Loc.GetString("cmu-net-board-body-placeholder")),
        };
        body.OnTextChanged += _ => OnKeyTyped?.Invoke();

        var publish = PageButton(label);
        var cancel = PageButton(Loc.GetString("cmu-net-board-cancel"));
        publish.OnPressed += _ =>
        {
            var text = Rope.Collapse(body.TextRope);
            if (title.Text.Trim().Length == 0 || text.Trim().Length == 0)
                return;

            OnClicked?.Invoke();
            _host?.Post(_site.ID, title.Text, text);
            _composerOpen = false;
            BuildComposer(true);
        };
        cancel.OnPressed += _ =>
        {
            _composerOpen = false;
            BuildComposer(true);
        };

        _composerSlot.AddChild(new PanelContainer
        {
            PanelOverride = _theme.Box(Color.InterpolateBetween(Background, Link, 0.08f), Link.WithAlpha(0.6f)),
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Margin = new Thickness(8),
                    SeparationOverride = 6,
                    Children =
                    {
                        new Label { Text = label, StyleClasses = { "LabelKeyText" } },
                        title,
                        Framed(body),
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            SeparationOverride = 6,
                            Children = { publish, cancel },
                        },
                    },
                },
            },
        });
    }

    private Control BuildPost(CMUNetBoardPost post, CMUNetBoardStateMessage state)
    {
        var box = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 4 };

        var header = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
        header.AddChild(new Label { Text = post.Title, StyleClasses = { "LabelHeading" }, HorizontalExpand = true, ClipText = true });
        if (state.CanModerate)
        {
            var delete = PageButton(Loc.GetString("cmu-net-board-delete"));
            delete.OnPressed += _ =>
            {
                OnClicked?.Invoke();
                _host?.Delete(_site.ID, post.Id);
            };
            header.AddChild(delete);
        }

        box.AddChild(header);
        box.AddChild(new Label
        {
            Text = Loc.GetString("cmu-net-board-byline", ("author", post.Author), ("role", Role(post.Role)), ("time", Clock(post.Time))),
            FontColorOverride = Text.WithAlpha(0.65f),
        });
        box.AddChild(Plain(post.Body, Text));

        var comments = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(16, 4, 0, 0), SeparationOverride = 3 };
        foreach (var comment in post.Comments)
        {
            comments.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children =
                {
                    new Label
                    {
                        Text = Loc.GetString("cmu-net-board-comment-byline", ("author", comment.Author), ("role", Role(comment.Role)), ("time", Clock(comment.Time))),
                        FontColorOverride = Heading.WithAlpha(0.8f),
                    },
                    Plain(comment.Body, Text),
                },
            });
        }

        if (state.CanComment)
            comments.AddChild(CommentLine(post.Id));

        box.AddChild(comments);
        box.AddChild(new CMUTerminalRule { Color = Link.WithAlpha(0.25f), Margin = new Thickness(0, 6, 0, 0) });
        return box;
    }

    private Control CommentLine(int post)
    {
        var field = Field(Loc.GetString("cmu-net-board-comment-placeholder"), CMUNetBoardLimits.CommentLength);
        field.HorizontalExpand = true;
        if (_drafts.Remove(post, out var draft))
            field.Text = draft;

        _commentFields[post] = field;
        var send = PageButton(Loc.GetString("cmu-net-board-comment-send"));

        void Send()
        {
            if (field.Text.Trim().Length == 0)
                return;

            OnClicked?.Invoke();
            _host?.Comment(_site.ID, post, field.Text);
            field.Text = string.Empty;
        }

        field.OnTextEntered += _ => Send();
        send.OnPressed += _ => Send();
        return new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children = { field, send },
        };
    }

    private LineEdit Field(string placeholder, int max)
    {
        var field = new LineEdit { PlaceHolder = placeholder, HorizontalExpand = true };
        field.IsValid = text => text.Length <= max;
        field.OnTextChanged += _ => OnKeyTyped?.Invoke();
        return field;
    }

    private Control Framed(Control control)
    {
        return new PanelContainer
        {
            PanelOverride = _theme.Box(Background, Link.WithAlpha(0.6f)),
            HorizontalExpand = true,
            Children = { control },
        };
    }

    private Button PageButton(string text)
    {
        var button = _theme.Button(text);
        button.Label.FontColorOverride = Link;
        button.StyleBoxOverride = _theme.ButtonBox(Background, Link.WithAlpha(0.8f));
        return button;
    }

    /// <summary>Player text as plain text: markup in it shows as typed rather than being parsed.</summary>
    private static RichTextLabel Plain(string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        return label;
    }

    private static string Role(string role)
    {
        return role.Length == 0 ? Loc.GetString("cmu-net-board-no-role") : role;
    }

    /// <summary>The same shift clock as the taskbar: the round starts at 06:00.</summary>
    private static string Clock(TimeSpan time)
    {
        var shift = TimeSpan.FromHours(6) + time;
        return $"{shift.Hours:00}:{shift.Minutes:00}";
    }
}
