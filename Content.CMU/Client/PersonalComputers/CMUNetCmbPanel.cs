using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Client.Pinpointer.UI;
using Content.Shared.CMU14.PersonalComputers;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A live panel of the Marshals' network: <c>&lt;CMUNetCmbPanel Kind="Alerts|Map|Faxes|Records"/&gt;</c>.
/// It asks the computer it runs on and refreshes itself while on screen; on computers off the CMB network the
/// server never answers and the panel stays at its loading line.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetCmbPanel : BoxContainer, IDocumentTag
{
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(10);
    private static readonly ResPath Pin = new("/Textures/Interface/NavMap/beveled_circle.png");

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;

    private CMUCmbPanel _panel;
    private ICMUComputerHost? _host;
    private TimeSpan _next;

    public CMUNetCmbPanel()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 4;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Kind", out var kind) || !Enum.TryParse(kind, out _panel))
            return false;

        AddChild(new Label { Text = Loc.GetString("cmu-cmb-loading") });
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

        if (_host == null)
            return;

        _host.CmbReceived += OnState;
        _next = TimeSpan.Zero;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.CmbReceived -= OnState;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_host == null || _timing.RealTime < _next)
            return;

        _next = _timing.RealTime + Refresh;
        _host.RequestCmb(_panel);
    }

    private void OnState(CMUCmbStateMessage state)
    {
        if (state.Panel != _panel)
            return;

        RemoveAllChildren();
        switch (_panel)
        {
            case CMUCmbPanel.Alerts:
                ShowAlerts(state.Alerts);
                break;
            case CMUCmbPanel.Map:
                ShowMap(state);
                break;
            case CMUCmbPanel.Faxes:
                ShowFaxes(state.Faxes);
                break;
            case CMUCmbPanel.Records:
                ShowRecords(state.Records);
                break;
        }
    }

    internal static string Clock(TimeSpan time)
    {
        var shift = TimeSpan.FromHours(6) + time;
        return $"{shift.Hours:00}:{shift.Minutes:00}";
    }

    private void ShowAlerts(List<CMUCmbAlert> alerts)
    {
        if (alerts.Count == 0)
            AddChild(new Label { Text = Loc.GetString("cmu-cmb-alerts-empty") });

        foreach (var alert in alerts)
        {
            var line = alert.Trace != null
                ? Loc.GetString("cmu-cmb-trace-alert", ("time", Clock(alert.Time)), ("id", alert.TerminalId), ("location", alert.Location), ("result", alert.Trace))
                : Loc.GetString("cmu-cmb-alert", ("time", Clock(alert.Time)), ("id", alert.TerminalId), ("location", alert.Location), ("address", alert.Address));
            AddChild(Plain(line));
        }
    }

    private void ShowMap(CMUCmbStateMessage state)
    {
        if (state.Grid is not { } netGrid || !_entities.TryGetEntity(netGrid, out var grid))
        {
            AddChild(new Label { Text = Loc.GetString("cmu-cmb-map-none") });
            return;
        }

        var map = new NavMapControl
        {
            MapUid = grid,
            HorizontalExpand = true,
            SetHeight = 420,
        };
        var texture = _entities.System<SpriteSystem>().Frame0(new SpriteSpecifier.Texture(Pin));
        foreach (var terminal in state.Terminals)
        {
            var color = terminal.Alerted ? Color.FromHex("#E0303A") : Color.FromHex("#8FC4FF");
            map.TrackedEntities[terminal.Entity] = new NavMapBlip(
                new EntityCoordinates(grid.Value, terminal.Position), texture, color, terminal.Alerted);
        }

        map.ForceNavMapUpdate();
        map.TrackedEntitySelectedAction += terminal =>
        {
            if (terminal is { } picked)
                _host?.Trace(picked);
        };
        AddChild(new Label { Text = Loc.GetString("cmu-cmb-map-hint") });
        AddChild(map);

        // The alerted terminals under the map, so the numbers can be read off.
        foreach (var terminal in state.Terminals)
        {
            if (terminal.Alerted)
                AddChild(new Label { Text = Loc.GetString("cmu-cmb-map-terminal", ("id", terminal.TerminalId)), FontColorOverride = Color.FromHex("#E0303A") });
        }
    }

    private void ShowFaxes(List<CMUCmbFax> faxes)
    {
        if (faxes.Count == 0)
            AddChild(new Label { Text = Loc.GetString("cmu-cmb-faxes-empty") });

        foreach (var fax in faxes)
        {
            var body = Plain(fax.Content);
            body.Visible = false;
            var header = new Button
            {
                Text = Loc.GetString("cmu-cmb-fax", ("time", Clock(fax.Time)), ("from", fax.From), ("to", fax.To), ("title", fax.Title)),
                HorizontalExpand = true,
                ClipText = true,
                ToggleMode = true,
            };
            header.OnToggled += args => body.Visible = args.Pressed;
            AddChild(header);
            AddChild(body);
        }
    }

    private void ShowRecords(List<CMUCmbRecord> records)
    {
        if (records.Count == 0)
            AddChild(new Label { Text = Loc.GetString("cmu-cmb-records-empty") });

        foreach (var record in records)
        {
            AddChild(new Label { Text = Loc.GetString("cmu-cmb-record", ("name", record.Name), ("status", record.Status)), StyleClasses = { "LabelKeyText" } });
            if (record.Reason.Length > 0)
                AddChild(Plain(Loc.GetString("cmu-cmb-record-reason", ("reason", record.Reason))));
            if (record.History.Count > 0)
                AddChild(Plain(Loc.GetString("cmu-cmb-record-history", ("history", string.Join("; ", record.History)))));
            AddChild(new CMUTerminalRule { Color = Color.White.WithAlpha(0.15f), Margin = new Thickness(0, 4) });
        }
    }

    /// <summary>Player written text is shown as typed, never parsed as markup.</summary>
    internal static RichTextLabel Plain(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }
}
