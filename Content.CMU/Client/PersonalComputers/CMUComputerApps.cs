using System.Linq;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The Mail program: the logged in user's inbox and sent mail on the left, the open mail on the right, and a
/// form to write one. Mailboxes belong to names, so this works from any terminal.
/// </summary>
public sealed class CMUComputerMailApp : BoxContainer
{
    private readonly CMUComputerTheme _theme;
    private readonly ICMUComputerHost _host;
    private readonly BoxContainer _list;
    private readonly BoxContainer _reader;
    private bool _showSent;
    private CMUMailStateMessage? _state;

    public event Action? OnClicked;
    public event Action? OnKeyTyped;

    public CMUComputerMailApp(CMUComputerTheme theme, ICMUComputerHost host)
    {
        _theme = theme;
        _host = host;
        Orientation = LayoutOrientation.Horizontal;
        VerticalExpand = true;

        var inbox = theme.Button(Loc.GetString("cmu-mail-inbox"));
        inbox.OnPressed += _ =>
        {
            _showSent = false;
            Click();
            Rebuild();
        };
        var sent = theme.Button(Loc.GetString("cmu-mail-sent"));
        sent.OnPressed += _ =>
        {
            _showSent = true;
            Click();
            Rebuild();
        };
        var write = theme.Button(Loc.GetString("cmu-mail-compose"));
        write.OnPressed += _ =>
        {
            Click();
            Compose(string.Empty, string.Empty);
        };

        _list = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2 };
        _reader = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 6, HorizontalExpand = true };

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 190,
            Margin = new Thickness(4),
            SeparationOverride = 4,
            Children =
            {
                new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 2, Children = { inbox, sent, write } },
                new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { _list } },
            },
        });
        AddChild(new CMUTerminalRule { Color = theme.Accent, MinWidth = 1, HorizontalExpand = false, VerticalExpand = true });
        AddChild(new ScrollContainer { HorizontalExpand = true, HScrollEnabled = false, Children = { _reader } });

        if (host.SessionUser == null)
            _reader.AddChild(theme.Label(Loc.GetString("cmu-mail-login")));
        else if (!host.Carrier)
            _reader.AddChild(theme.Label(Loc.GetString("cmu-mail-no-carrier")));
        else if (host.Dark)
            _reader.AddChild(theme.Label(Loc.GetString("cmu-mail-dark")));
    }

    private void Click()
    {
        OnClicked?.Invoke();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _host.MailReceived += OnState;
        _host.RequestMail();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _host.MailReceived -= OnState;
    }

    private void OnState(CMUMailStateMessage state)
    {
        _state = state;
        Rebuild();
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        if (_state == null)
            return;

        var mails = _showSent ? _state.Sent : _state.Inbox;
        if (mails.Count == 0)
            _list.AddChild(_theme.Label(Loc.GetString("cmu-mail-empty"), _theme.Font, _theme.Dim));

        foreach (var mail in mails)
        {
            var button = _theme.Button($"{(_showSent ? mail.To : mail.From)}: {mail.Subject}");
            button.HorizontalExpand = true;
            button.ClipText = true;
            button.Label.HorizontalAlignment = HAlignment.Left;
            button.OnPressed += _ =>
            {
                Click();
                Read(mail);
            };
            _list.AddChild(button);
        }
    }

    private void Read(CMUMailMessage mail)
    {
        _reader.RemoveAllChildren();
        _reader.AddChild(_theme.Label(mail.Subject, _theme.Bold, _theme.Accent));
        var time = TimeSpan.FromHours(6) + mail.Time;
        var clock = $"{time.Hours:00}:{time.Minutes:00}";
        _reader.AddChild(_theme.Label(_showSent
            ? Loc.GetString("cmu-mail-to-line", ("to", mail.To), ("time", clock))
            : Loc.GetString("cmu-mail-from-line", ("from", mail.From), ("time", clock)), _theme.Font, _theme.Dim));
        _reader.AddChild(new CMUTerminalRule { Color = _theme.Accent });

        var body = new RichTextLabel { HorizontalExpand = true };
        body.SetMessage(FormattedMessage.FromUnformatted(mail.Body));
        _reader.AddChild(body);

        if (!_showSent)
        {
            var reply = _theme.Button(Loc.GetString("cmu-mail-compose"));
            reply.HorizontalAlignment = HAlignment.Left;
            reply.OnPressed += _ =>
            {
                Click();
                Compose(mail.From, $"RE: {mail.Subject}");
            };
            _reader.AddChild(reply);
        }
    }

    private void Compose(string to, string subject)
    {
        _reader.RemoveAllChildren();
        var toField = new LineEdit { Text = to, PlaceHolder = Loc.GetString("cmu-mail-to"), HorizontalExpand = true };
        var subjectField = new LineEdit { Text = subject, PlaceHolder = Loc.GetString("cmu-mail-subject"), HorizontalExpand = true };
        subjectField.IsValid = text => text.Length <= CMUMailLimits.SubjectLength;
        var body = new TextEdit { HorizontalExpand = true, MinHeight = 140 };
        toField.OnTextChanged += _ => OnKeyTyped?.Invoke();
        subjectField.OnTextChanged += _ => OnKeyTyped?.Invoke();
        body.OnTextChanged += _ => OnKeyTyped?.Invoke();

        var send = _theme.Button(Loc.GetString("cmu-mail-send"));
        send.HorizontalAlignment = HAlignment.Left;
        send.OnPressed += _ =>
        {
            var text = Rope.Collapse(body.TextRope);
            if (toField.Text.Trim().Length == 0 || text.Trim().Length == 0)
                return;

            Click();
            _host.SendMail(toField.Text, subjectField.Text, text);
            _showSent = true;
            _reader.RemoveAllChildren();
        };

        _reader.AddChild(toField);
        _reader.AddChild(subjectField);
        _reader.AddChild(new PanelContainer { PanelOverride = _theme.Box(_theme.Background, _theme.Accent), Children = { body } });
        _reader.AddChild(send);
        if (_state is { Directory.Count: > 0 })
            _reader.AddChild(_theme.Label(Loc.GetString("cmu-mail-directory", ("names", string.Join(", ", _state.Directory))), _theme.Small, _theme.Dim));
    }
}

/// <summary>
/// The Files program: the OS and computer documents, plus notes saved on this terminal by whoever used it.
/// Notes can be written, read and deleted by anyone at the terminal, which makes them evidence as much as notes.
/// </summary>
public sealed partial class CMUComputerFilesApp : BoxContainer
{
    [Dependency] private IResourceManager _resources = default!;

    private readonly CMUComputerTheme _theme;
    private readonly ICMUComputerHost? _host;
    private readonly List<CMUComputerDocument> _documents;
    private readonly bool _notes;
    private readonly BoxContainer _list;
    private readonly CMUNetPageView _viewer;
    private readonly BoxContainer _pane;
    private string _shownNotes = string.Empty;

    public event Action? OnClicked;
    public event Action? OnKeyTyped;

    public CMUComputerFilesApp(CMUComputerTheme theme, ICMUComputerHost? host, List<CMUComputerDocument> documents, bool notes)
    {
        IoCManager.InjectDependencies(this);
        _theme = theme;
        _host = host;
        _documents = documents;
        _notes = notes && host != null;
        Orientation = LayoutOrientation.Horizontal;
        VerticalExpand = true;

        _list = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(4), SeparationOverride = 2 };
        _viewer = new CMUNetPageView(theme);
        _pane = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
        _pane.AddChild(_viewer);

        AddChild(new ScrollContainer { MinWidth = 160, HScrollEnabled = false, Children = { _list } });
        AddChild(new CMUTerminalRule { Color = theme.Accent, MinWidth = 1, HorizontalExpand = false, VerticalExpand = true });
        AddChild(_pane);
        Rebuild();
        if (_documents.Count > 0)
            ShowDocument(_documents[0]);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_notes)
            return;

        var shown = string.Join('\n', _host!.Notes.Select(n => $"{n.Name}\t{n.Author}\t{n.Text.Length}"));
        if (shown == _shownNotes)
            return;

        _shownNotes = shown;
        Rebuild();
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        foreach (var document in _documents)
        {
            var button = FileButton(document.Name);
            button.OnPressed += _ =>
            {
                OnClicked?.Invoke();
                ShowDocument(document);
            };
            _list.AddChild(button);
        }

        if (_notes)
        {
            for (var i = 0; i < _host!.Notes.Count; i++)
            {
                var index = i;
                var note = _host.Notes[i];
                var button = FileButton($"{note.Name}.TXT");
                button.OnPressed += _ =>
                {
                    OnClicked?.Invoke();
                    ShowNote(index, note);
                };
                _list.AddChild(button);
            }

            var add = _theme.Button(Loc.GetString("cmu-notes-new"));
            add.OnPressed += _ =>
            {
                OnClicked?.Invoke();
                Edit(string.Empty, string.Empty);
            };
            _list.AddChild(add);
        }
        else if (_documents.Count == 0)
            _list.AddChild(_theme.Label(Loc.GetString("cmu-pc-files-empty"), _theme.Font, _theme.Dim));
    }

    private Button FileButton(string name)
    {
        var button = _theme.Button(name);
        button.HorizontalExpand = true;
        button.ClipText = true;
        button.Label.HorizontalAlignment = HAlignment.Left;
        return button;
    }

    private void ShowDocument(CMUComputerDocument document)
    {
        _pane.RemoveAllChildren();
        _pane.AddChild(_viewer);
        using var reader = _resources.ContentFileReadText(document.Path);
        _viewer.ShowDocument(reader.ReadToEnd(), null);
    }

    private void ShowNote(int index, CMUComputerNote note)
    {
        _pane.RemoveAllChildren();
        var box = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 6 };
        box.AddChild(_theme.Label($"{note.Name}.TXT", _theme.Bold, _theme.Accent));
        if (note.Author.Length > 0)
            box.AddChild(_theme.Label(Loc.GetString("cmu-notes-by", ("author", note.Author)), _theme.Small, _theme.Dim));

        var text = new RichTextLabel { HorizontalExpand = true };
        text.SetMessage(FormattedMessage.FromUnformatted(note.Text));
        box.AddChild(text);

        var edit = _theme.Button(Loc.GetString("cmu-notes-save"));
        edit.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            Edit(note.Name, note.Text);
        };
        var delete = _theme.Button(Loc.GetString("cmu-notes-delete"));
        delete.OnPressed += _ =>
        {
            OnClicked?.Invoke();
            _host!.DeleteNote(index);
            _pane.RemoveAllChildren();
        };
        box.AddChild(new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, Children = { edit, delete } });
        _pane.AddChild(new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { box } });
    }

    private void Edit(string name, string text)
    {
        _pane.RemoveAllChildren();
        var nameField = new LineEdit { Text = name, PlaceHolder = Loc.GetString("cmu-notes-name"), HorizontalExpand = true };
        nameField.IsValid = t => t.Length <= CMUNoteLimits.NameLength;
        var body = new TextEdit { HorizontalExpand = true, VerticalExpand = true, MinHeight = 160 };
        body.TextRope = new Rope.Leaf(text);
        nameField.OnTextChanged += _ => OnKeyTyped?.Invoke();
        body.OnTextChanged += _ => OnKeyTyped?.Invoke();

        var save = _theme.Button(Loc.GetString("cmu-notes-save"));
        save.HorizontalAlignment = HAlignment.Left;
        save.OnPressed += _ =>
        {
            var content = Rope.Collapse(body.TextRope);
            if (nameField.Text.Trim().Length == 0 || content.Trim().Length == 0)
                return;

            OnClicked?.Invoke();
            _host!.SaveNote(nameField.Text, content);
            _pane.RemoveAllChildren();
        };

        _pane.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
            VerticalExpand = true,
            Children =
            {
                nameField,
                new PanelContainer { PanelOverride = _theme.Box(_theme.Background, _theme.Accent), VerticalExpand = true, Children = { body } },
                save,
            },
        });
    }
}
