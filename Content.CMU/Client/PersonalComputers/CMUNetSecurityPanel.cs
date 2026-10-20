using System.Diagnostics.CodeAnalysis;
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
/// A live panel of the colony security network: <c>&lt;CMUNetSecurityPanel Kind="Map|Log"/&gt;</c>. It asks the
/// computer it runs on and refreshes itself while on screen; off the security network the server never answers
/// and the panel stays at its loading line.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetSecurityPanel : BoxContainer, IDocumentTag
{
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(10);
    private static readonly ResPath Pin = new("/Textures/Interface/NavMap/beveled_circle.png");
    private static readonly Color Online = Color.FromHex("#3FE05A");
    private static readonly Color LoggedIn = Color.FromHex("#8FC4FF");
    private static readonly Color Off = Color.FromHex("#6B7480");
    private static readonly Color Unpowered = Color.FromHex("#FFB000");
    private static readonly Color Dark = Color.FromHex("#E0303A");
    private static readonly Color Hacked = Color.FromHex("#C04CFF");

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;

    private CMUSecurityPanel _panel;
    private ICMUComputerHost? _host;
    private TimeSpan _next;

    public CMUNetSecurityPanel()
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

        AddChild(new Label { Text = Loc.GetString("cmu-security-loading") });
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

        _host.SecurityReceived += OnState;
        _next = TimeSpan.Zero;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_host != null)
            _host.SecurityReceived -= OnState;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_host == null || _timing.RealTime < _next)
            return;

        _next = _timing.RealTime + Refresh;
        _host.RequestSecurity(_panel);
    }

    private void OnState(CMUSecurityStateMessage state)
    {
        if (state.Panel != _panel)
            return;

        RemoveAllChildren();
        switch (_panel)
        {
            case CMUSecurityPanel.Log:
                ShowLog(state.Entries);
                break;
            case CMUSecurityPanel.Map:
                ShowMap(state);
                break;
        }
    }

    private void ShowLog(List<CMUSecurityEntry> entries)
    {
        if (entries.Count == 0)
            AddChild(new Label { Text = Loc.GetString("cmu-security-log-empty") });

        foreach (var entry in entries)
            AddChild(CMUNetCmbPanel.Plain(Line(entry)));
    }

    /// <summary>One log line: when, which terminal, where, what, and the card's name where known.</summary>
    private static string Line(CMUSecurityEntry entry)
    {
        var what = entry.Kind switch
        {
            CMUSecurityEvent.LoginRefused => Loc.GetString("cmu-security-event-refused"),
            CMUSecurityEvent.WentDark => Loc.GetString("cmu-security-event-dark"),
            CMUSecurityEvent.Rejoined => Loc.GetString("cmu-security-event-rejoined"),
            CMUSecurityEvent.HackStarted => Loc.GetString("cmu-security-event-hack"),
            CMUSecurityEvent.LogWiped => Loc.GetString("cmu-security-event-wiped"),
            CMUSecurityEvent.LogForged => Loc.GetString("cmu-security-event-forged"),
            CMUSecurityEvent.DeepLookup => Loc.GetString("cmu-security-event-lookup", ("address", entry.Detail ?? string.Empty)),
            _ => string.Empty,
        };

        var time = CMUNetCmbPanel.Clock(entry.Time);
        return entry.Name is { } name
            ? Loc.GetString("cmu-security-entry-named", ("time", time), ("id", entry.TerminalId), ("location", entry.Location), ("event", what), ("name", name))
            : Loc.GetString("cmu-security-entry", ("time", time), ("id", entry.TerminalId), ("location", entry.Location), ("event", what));
    }

    private static Color StatusColor(CMUSecurityTerminal terminal)
    {
        if (terminal.Status == CMUSecurityStatus.Dark)
            return Dark;

        if (terminal.Hacked)
            return Hacked;

        return terminal.Status switch
        {
            CMUSecurityStatus.LoggedIn => LoggedIn,
            CMUSecurityStatus.Off => Off,
            CMUSecurityStatus.Unpowered => Unpowered,
            _ => Online,
        };
    }

    private static string StatusName(CMUSecurityStatus status)
    {
        return status switch
        {
            CMUSecurityStatus.LoggedIn => Loc.GetString("cmu-security-status-logged-in"),
            CMUSecurityStatus.Off => Loc.GetString("cmu-security-status-off"),
            CMUSecurityStatus.Unpowered => Loc.GetString("cmu-security-status-unpowered"),
            CMUSecurityStatus.Dark => Loc.GetString("cmu-security-status-dark"),
            _ => Loc.GetString("cmu-security-status-online"),
        };
    }

    private Label Describe(CMUSecurityTerminal terminal)
    {
        var dark = terminal.Status == CMUSecurityStatus.Dark;
        var id = (dark, terminal.Hacked) switch
        {
            (true, true) => "cmu-security-map-terminal-dark-hacked",
            (true, false) => "cmu-security-map-terminal-dark",
            (false, true) => "cmu-security-map-terminal-hacked",
            _ => "cmu-security-map-terminal",
        };
        var text = Loc.GetString(id, ("id", terminal.TerminalId), ("status", StatusName(terminal.Status)), ("location", terminal.Location));
        return new Label { Text = text, FontColorOverride = StatusColor(terminal) };
    }

    private void ShowMap(CMUSecurityStateMessage state)
    {
        if (state.Grid is not { } netGrid || !_entities.TryGetEntity(netGrid, out var grid))
        {
            AddChild(new Label { Text = Loc.GetString("cmu-security-map-none") });
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
            var flagged = terminal.Status == CMUSecurityStatus.Dark || terminal.Hacked;
            map.TrackedEntities[terminal.Entity] = new NavMapBlip(
                new EntityCoordinates(grid.Value, terminal.Position), texture, StatusColor(terminal), flagged);
        }

        map.ForceNavMapUpdate();

        // Clicking a pin names the terminal and what it is doing under the map.
        var picked = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        map.TrackedEntitySelectedAction += entity =>
        {
            picked.RemoveAllChildren();
            foreach (var terminal in state.Terminals)
            {
                if (terminal.Entity == entity)
                    picked.AddChild(Describe(terminal));
            }
        };

        AddChild(new Label { Text = Loc.GetString("cmu-security-map-hint") });
        AddChild(new Label { Text = Loc.GetString("cmu-security-map-legend") });
        AddChild(map);
        AddChild(picked);

        // Dark and hacked terminals under the map, so they can be found on foot.
        var flaggedAny = false;
        foreach (var terminal in state.Terminals)
        {
            if (terminal.Status != CMUSecurityStatus.Dark && !terminal.Hacked)
                continue;

            if (!flaggedAny)
                AddChild(new Label { Text = Loc.GetString("cmu-security-map-check"), StyleClasses = { "LabelKeyText" } });

            flaggedAny = true;
            AddChild(Describe(terminal));
        }
    }
}
