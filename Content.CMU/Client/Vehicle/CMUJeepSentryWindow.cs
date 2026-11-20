using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Sentry;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// The jeep sentry's settings, for whoever holds the jeep's key: power, whether it goes for the
/// closest or the biggest threat, and per faction, whether it holds fire on them and whether it fires
/// on them anyway once they hold a gun.
/// </summary>
public sealed class CMUJeepSentryWindow : DefaultWindow
{
    public event Action<string, bool>? OnFriendly;
    public event Action<string, bool>? OnArmed;
    public event Action<CMUJeepSentryTargetMode>? OnMode;
    public event Action<bool>? OnPower;

    private readonly Label _sentry;
    private readonly Label _target;
    private readonly Button _power;
    private readonly Button _closest;
    private readonly Button _biggest;
    private readonly Dictionary<string, (CheckBox Friendly, CheckBox Armed)> _factions = new();
    private bool _updating;

    public CMUJeepSentryWindow()
    {
        Title = Loc.GetString("cmu-jeep-sentry-title");
        MinSize = new Vector2(380, 0);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        Contents.AddChild(root);

        _sentry = new Label();
        _target = new Label();
        root.AddChild(_sentry);
        root.AddChild(_target);

        _power = new Button { ToggleMode = true };
        _power.OnToggled += args =>
        {
            if (!_updating)
                OnPower?.Invoke(args.Pressed);
        };
        root.AddChild(_power);

        var modes = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        modes.AddChild(new Label { Text = Loc.GetString("cmu-jeep-sentry-mode") });
        var group = new ButtonGroup();
        _closest = ModeButton(group, "cmu-jeep-sentry-mode-closest", CMUJeepSentryTargetMode.Closest);
        _biggest = ModeButton(group, "cmu-jeep-sentry-mode-biggest", CMUJeepSentryTargetMode.Biggest);
        modes.AddChild(_closest);
        modes.AddChild(_biggest);
        root.AddChild(modes);

        root.AddChild(new Label { Text = Loc.GetString("cmu-jeep-sentry-factions"), FontColorOverride = Color.LightGray });
        var grid = new GridContainer { Columns = 3, HSeparationOverride = 12 };
        grid.AddChild(new Label { Text = Loc.GetString("cmu-jeep-sentry-faction") });
        grid.AddChild(new Label { Text = Loc.GetString("cmu-jeep-sentry-hold-fire") });
        grid.AddChild(new Label { Text = Loc.GetString("cmu-jeep-sentry-fire-if-armed") });
        foreach (var faction in SharedSentryTargetingSystem.SentryAllowedFactions.OrderBy(f => f))
        {
            var friendly = new CheckBox();
            var armed = new CheckBox();
            friendly.OnToggled += args =>
            {
                if (!_updating)
                    OnFriendly?.Invoke(faction, args.Pressed);
            };
            armed.OnToggled += args =>
            {
                if (!_updating)
                    OnArmed?.Invoke(faction, args.Pressed);
            };

            grid.AddChild(new Label { Text = faction });
            grid.AddChild(friendly);
            grid.AddChild(armed);
            _factions[faction] = (friendly, armed);
        }

        root.AddChild(grid);
        root.AddChild(new Label
        {
            Text = Loc.GetString("cmu-jeep-sentry-help"),
            FontColorOverride = Color.Gray,
        });
    }

    private Button ModeButton(ButtonGroup group, string text, CMUJeepSentryTargetMode mode)
    {
        var button = new Button { Text = Loc.GetString(text), ToggleMode = true, Group = group };
        button.OnPressed += _ =>
        {
            if (!_updating)
                OnMode?.Invoke(mode);
        };
        return button;
    }

    public void SetState(CMUJeepSentryMountComponent mount, string? sentryName, string? targetName)
    {
        _updating = true;
        _sentry.Text = sentryName == null
            ? Loc.GetString("cmu-jeep-sentry-none")
            : Loc.GetString("cmu-jeep-sentry-fitted", ("sentry", sentryName));
        _target.Text = targetName == null
            ? Loc.GetString("cmu-jeep-sentry-no-target")
            : Loc.GetString("cmu-jeep-sentry-tracking", ("target", targetName));

        _power.Pressed = mount.Powered;
        _power.Text = Loc.GetString(mount.Powered ? "cmu-jeep-sentry-power-on" : "cmu-jeep-sentry-power-off");
        _closest.Pressed = mount.Mode == CMUJeepSentryTargetMode.Closest;
        _biggest.Pressed = mount.Mode == CMUJeepSentryTargetMode.Biggest;

        foreach (var (faction, (friendly, armed)) in _factions)
        {
            var isFriendly = mount.FriendlyFactions.Contains(faction);
            friendly.Pressed = isFriendly;
            armed.Pressed = mount.ArmedHostileFactions.Contains(faction);
            armed.Disabled = !isFriendly;
        }

        _updating = false;
    }
}
