using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using JetBrains.Annotations;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A tape's title as a link: <c>&lt;CMUNetTapeLink Title="My Working Joe Learns To Wave" Channel="hydro_hannah"
/// Genre="bunk" Number="3."/&gt;</c>. Clicking it plays that tape in the page's <see cref="CMUNetTape"/> player and
/// scrolls up to it. The tape is made from its title, so the same link always plays the same tape.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetTapeLink : Label, IDocumentTag
{
    [Dependency] private IResourceCache _cache = default!;

    private string _title = string.Empty;
    private string _channel = string.Empty;
    private string? _genre;
    private bool _hovered;
    private readonly Color _colour = Color.FromHex("#FF2A2A");

    public CMUNetTapeLink()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
        FontColorOverride = _colour;
        Margin = new Thickness(0, 8, 0, 2);
        OnMouseEntered += _ => _hovered = true;
        OnMouseExited += _ => _hovered = false;
        OnKeyBindDown += args =>
        {
            if (args.Function != Robust.Shared.Input.EngineKeyFunctions.UIClick)
                return;

            args.Handle();
            Activate();
        };
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Title", out var title))
            return false;

        _title = title;
        _channel = args.GetValueOrDefault("Channel", string.Empty);
        _genre = args.GetValueOrDefault("Genre");
        var number = args.TryGetValue("Number", out var n) ? $"{n} " : string.Empty;
        Text = $"{number}{title}";
        FontOverride = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 15);
        ToolTip = Loc.GetString("cmu-tape-link-tooltip");
        control = this;
        return true;
    }

    /// <summary>Plays the tape, as clicking the link does.</summary>
    public void Activate()
    {
        Control? root = this;
        while (root.Parent != null && root is not CMUNetBrowser)
            root = root.Parent;

        if (Find<CMUNetTape>(root) is not { } player)
            return;

        player.PlayFixed(_title, _channel, _genre);
        for (var parent = player.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is not ScrollContainer scroll)
                continue;

            var offset = player.GlobalPosition.Y - scroll.GlobalPosition.Y + scroll.GetScrollValue().Y;
            scroll.SetScrollValue(new Vector2(0, offset));
            break;
        }
    }

    private static T? Find<T>(Control control) where T : Control
    {
        if (control is T found)
            return found;

        foreach (var child in control.Children)
        {
            if (Find<T>(child) is { } inner)
                return inner;
        }

        return null;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_hovered)
            handle.DrawLine(new Vector2(0, PixelHeight - 1), new Vector2(PixelWidth, PixelHeight - 1), _colour);

        base.Draw(handle);
    }
}
