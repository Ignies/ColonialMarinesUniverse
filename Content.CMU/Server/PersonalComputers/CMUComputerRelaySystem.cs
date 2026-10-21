using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Insurgency;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Insurgent relay bugs. A bug is planted on a public terminal with an access tuner (see
/// <see cref="DeepWeb.CMUDeepWebSystem.TryStartBug"/>); from a hacked terminal the insurgents then route their deep
/// web traffic through it, so every lookup lands on the relay's own log and reaches the Marshals and colony security
/// under the relay's number, location and whoever is logged in there, never the insurgent's terminal. The relay's
/// screen corrupts more and more until someone formats it with the same tuner, which clears the bug. Routing only
/// carries while the relay is powered, switched on and on the colony network. The admin log always records the real
/// actor and terminal: only the in-game Marshal and security systems are fooled.
/// </summary>
public sealed class CMUComputerRelaySystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";

    /// <summary>Formatting a terminal to clear a bug is basic IT: the lowest engineering rung.</summary>
    private const int FormatSkill = 1;

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUCmbSystem _cmb = default!;
    [Dependency] private CMUComputerSessionSystem _sessions = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private CMUSecurityNetSystem _security = default!;
    [Dependency] private InsurgencyFactionApplySystem _factions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMURelayListRequestMessage>(OnRelayListRequest);
            subs.Event<CMURelayRouteMessage>(OnRelayRoute);
        });
    }

    /// <summary>Installs the relay bug on a terminal, once the tuner minigame is solved. Covert: security is not told.</summary>
    public void Plant(Entity<CMUPersonalComputerComponent> terminal, EntityUid user)
    {
        var relay = EnsureComp<CMUComputerRelayComponent>(terminal);
        relay.PlantedAt = _timing.CurTime;
        relay.Faction = _factions.GetActiveFaction()?.Metadata.Title;
        Dirty(terminal.Owner, relay);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} planted an insurgent relay bug on terminal #{terminal.Comp.TerminalId}");
    }

    /// <summary>
    /// Formats the terminal with a tuner: clears any relay bug and the corruption it leaves, and wipes the desktop.
    /// Only a bugged terminal is wiped, so a clean one cannot be formatted out of spite.
    /// </summary>
    public bool Format(Entity<CMUPersonalComputerComponent> terminal, EntityUid user)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Relay))
            return false;

        if (!_skills.HasSkill(user, EngineerSkill, FormatSkill))
        {
            _popup.PopupEntity(Loc.GetString("cmu-relay-format-no-skill"), terminal, user, PopupType.SmallCaution);
            return false;
        }

        if (!HasComp<CMUComputerRelayComponent>(terminal))
        {
            _popup.PopupEntity(Loc.GetString("cmu-relay-format-nothing"), terminal, user);
            return false;
        }

        RemComp<CMUComputerRelayComponent>(terminal);

        // A format wipes the desktop: whatever is on screen, saved pages, downloaded games, notes and the wallpaper.
        terminal.Comp.Screen.Clear();
        terminal.Comp.Shortcuts.Clear();
        terminal.Comp.Downloads.Clear();
        terminal.Comp.Notes.Clear();
        terminal.Comp.Wallpaper = null;
        Dirty(terminal);

        _sessions.AddLog(terminal, Loc.GetString("cmu-relay-format-log"));
        _ui.CloseUi(terminal.Owner, CMUPersonalComputerUiKey.Key);
        _popup.PopupEntity(Loc.GetString("cmu-relay-format-done"), terminal, user, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} formatted terminal #{terminal.Comp.TerminalId}, clearing an insurgent relay bug");
        return true;
    }

    /// <summary>
    /// A deep lookup from a hacked terminal that is routing through a live relay: it lands on the relay instead, so
    /// the Marshals and security are sent to the relay and see its user. Returns whether it was routed.
    /// </summary>
    public bool RouteLookup(Entity<CMUPersonalComputerComponent> source, string address, EntityUid actor)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Relay)
            || !TryComp(source, out CMUDeepWebAccessComponent? deep)
            || deep.Relay is not { } relayNet
            || !TryGetEntity(relayNet, out var relayUid)
            || !TryComp(relayUid, out CMUPersonalComputerComponent? relay)
            || !HasComp<CMUComputerRelayComponent>(relayUid))
            return false;

        var relayEnt = new Entity<CMUPersonalComputerComponent>(relayUid.Value, relay);
        if (!IsOnline(relayEnt))
            return false;

        _sessions.AddLog(relayEnt, Loc.GetString("cmu-pc-log-visit", ("address", address)));
        _cmb.Alert(relayEnt, address, _security.Location(relayUid.Value));
        _security.Report(relayUid.Value, CMUSecurityEvent.DeepLookup, detail: address);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} looked up deep address {address} routed through insurgent relay terminal #{relay.TerminalId}, from terminal #{source.Comp.TerminalId}");
        return true;
    }

    /// <summary>A relay only carries while it is powered, switched on and reaching the colony network.</summary>
    private bool IsOnline(Entity<CMUPersonalComputerComponent> relay)
    {
        return relay.Comp.On
            && relay.Comp.Carrier
            && !_net.IsDark(relay.Comp)
            && _sessions.IsPowered(relay);
    }

    private void OnRelayListRequest(Entity<CMUPersonalComputerComponent> ent, ref CMURelayListRequestMessage args)
    {
        if (CanRoute(ent, args.Actor, out var deep))
            SendRelayList(ent, deep, args.Actor);
    }

    private void OnRelayRoute(Entity<CMUPersonalComputerComponent> ent, ref CMURelayRouteMessage args)
    {
        if (!CanRoute(ent, args.Actor, out var deep))
            return;

        NetEntity? route = null;
        if (args.Terminal is { } target && TryGetEntity(target, out var targetUid) && HasComp<CMUComputerRelayComponent>(targetUid))
            route = target;

        deep.Relay = route;
        Dirty(ent.Owner, deep);
        SendRelayList(ent, deep, args.Actor);
    }

    /// <summary>Only an insurgent at a hacked terminal picks a relay to route through.</summary>
    private bool CanRoute(Entity<CMUPersonalComputerComponent> ent, EntityUid actor, [NotNullWhen(true)] out CMUDeepWebAccessComponent? deep)
    {
        deep = null;
        return _cfg.GetCVar(CMUPersonalComputerCVars.Relay)
            && HasComp<CLFMemberComponent>(actor)
            && TryComp(ent, out deep);
    }

    private void SendRelayList(Entity<CMUPersonalComputerComponent> ent, CMUDeepWebAccessComponent deep, EntityUid actor)
    {
        var relays = new List<CMURelayInfo>();
        var query = EntityQueryEnumerator<CMUComputerRelayComponent, CMUPersonalComputerComponent>();
        while (query.MoveNext(out var uid, out _, out var pc))
        {
            var net = GetNetEntity(uid);
            relays.Add(new CMURelayInfo(
                net,
                pc.TerminalId,
                _security.Location(uid),
                pc.SessionUser,
                IsOnline((uid, pc)),
                deep.Relay == net));
        }

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key, new CMURelayListMessage(relays), actor);
    }
}
