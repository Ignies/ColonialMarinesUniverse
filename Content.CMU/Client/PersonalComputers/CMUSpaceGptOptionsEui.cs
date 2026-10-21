using System.Numerics;
using Content.Client.Eui;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// An admin rewriting a SpaceGPT question's answers before the ghosts vote: one line per answer, blank lines dropped.
/// Saving lets the question go to the ghosts; closing without saving leaves it held.
/// </summary>
[UsedImplicitly]
public sealed class CMUSpaceGptOptionsEui : BaseEui
{
    private DefaultWindow? _window;
    private readonly List<LineEdit> _options = new();

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is not CMUSpaceGptOptionsEuiState options)
            return;

        _window?.Close();
        _options.Clear();
        _window = new DefaultWindow
        {
            Title = Loc.GetString("cmu-spacegpt-admin-options-title", ("id", options.Poll)),
            MinSize = new Vector2(420, 200),
        };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 4 };
        box.AddChild(new Label { Text = Loc.GetString("cmu-spacegpt-admin-options-asked", ("name", options.Asker)) });
        box.AddChild(new Label { Text = $"\"{options.Question}\"", FontColorOverride = Color.LightGray });
        for (var i = 0; i < CMUSpaceGptLimits.Choices; i++)
        {
            var line = new LineEdit
            {
                Text = i < options.Options.Count ? options.Options[i] : string.Empty,
                PlaceHolder = Loc.GetString("cmu-spacegpt-admin-options-blank"),
                HorizontalExpand = true,
            };
            line.IsValid = text => text.Length <= CMUSpaceGptLimits.AnswerLength;
            _options.Add(line);
            box.AddChild(line);
        }

        var save = new Button { Text = Loc.GetString("cmu-spacegpt-admin-options-save") };
        save.OnPressed += _ => SendMessage(new CMUSpaceGptOptionsSaveMessage(_options.ConvertAll(o => o.Text)));
        box.AddChild(save);
        _window.Contents.AddChild(box);
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window?.Close();
        _window = null;
    }
}
