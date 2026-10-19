using Content.Server.Administration.Logs;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Tools;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Sessions on personal computers. Every terminal gets a number for the round; a swipe of an ID card logs its
/// holder in (and the same card logs them out); the terminal keeps a log of logins and pages visited that
/// engineers read with a multitool. A multitool opens a radial menu: read the logs, or hack the terminal.
/// Every lookup of a live deep web address from a terminal that is not hacked is reported to the Marshals.
/// </summary>
public sealed partial class CMUComputerSessionSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";
    private const int LogSkill = 1;

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private CMUCmbSystem _cmb = default!;
    [Dependency] private CMUDeepAliasSystem _aliases = default!;
    [Dependency] private CMUDeepWebSystem _deep = default!;
    [Dependency] private SharedIdCardSystem _idCards = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private int _nextTerminal = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _nextTerminal = 1);
        SubscribeLocalEvent<CMUPersonalComputerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUPersonalComputerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUNetVisitMessage>(OnVisit);
            subs.Event<CMUComputerLogoutMessage>(OnLogout);
        });
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUComputerToolUiKey.Key, subs =>
        {
            subs.Event<CMUComputerToolMessage>(OnTool);
        });
    }

    /// <summary>The next terminal number this round, handed out when a computer is placed.</summary>
    public int NextTerminalId()
    {
        return _nextTerminal++;
    }

    public void AddLog(Entity<CMUPersonalComputerComponent> computer, string text)
    {
        var log = computer.Comp.Log;
        log.Add(new CMUComputerLogEntry(_timing.CurTime, text));
        if (log.Count > CMUComputerLimits.LogEntries)
            log.RemoveAt(0);
    }

    private void OnInteractUsing(Entity<CMUPersonalComputerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<MultitoolComponent>(args.Used))
        {
            args.Handled = true;
            _ui.OpenUi(ent.Owner, CMUComputerToolUiKey.Key, args.User);
            return;
        }

        if (!_idCards.TryGetIdCard(args.Used, out var card))
            return;

        args.Handled = true;
        var name = card.Comp.FullName ?? Name(card);
        if (ent.Comp.SessionUser == name)
        {
            Logout(ent, args.User);
            return;
        }

        ent.Comp.SessionUser = name;
        ent.Comp.SessionJob = card.Comp.LocalizedJobTitle;
        Dirty(ent);
        AddLog(ent, Loc.GetString("cmu-pc-log-login", ("name", name)));
        _popup.PopupEntity(Loc.GetString("cmu-pc-login-popup", ("name", name)), ent, args.User);
    }

    private void OnLogout(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerLogoutMessage args)
    {
        Logout(ent, args.Actor);
    }

    private void Logout(Entity<CMUPersonalComputerComponent> ent, EntityUid user)
    {
        if (ent.Comp.SessionUser is not { } name)
            return;

        AddLog(ent, Loc.GetString("cmu-pc-log-logout", ("name", name)));
        ent.Comp.SessionUser = null;
        ent.Comp.SessionJob = null;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("cmu-pc-logout-popup"), ent, user);
    }

    private void OnGetVerbs(Entity<CMUPersonalComputerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.OpenVerb || !args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-pc-open-terminal"),
            Act = () => _ui.OpenUi(ent.Owner, CMUPersonalComputerUiKey.Key, user),
        });
    }

    /// <summary>
    /// The browser reports every page it opens. Pages are logged; a live deep address looked up from a terminal
    /// that is not hacked raises an alert for the Marshals with this terminal's number.
    /// </summary>
    private void OnVisit(Entity<CMUPersonalComputerComponent> ent, ref CMUNetVisitMessage args)
    {
        var address = args.Address.Trim();
        if (address.Length == 0 || address.Length > CMUNetShortcutLimits.AddressLength)
            return;

        AddLog(ent, Loc.GetString(args.Found ? "cmu-pc-log-visit" : "cmu-pc-log-visit-failed", ("address", address)));

        var host = CMUExtraNetSystem.Split(address).Host;
        if (HasComp<CMUDeepWebAccessComponent>(ent) || !_aliases.TryGetSite(host, out _))
            return;

        var location = _areas.TryGetArea(ent.Owner, out _, out var area) ? area.Name : Name(ent);
        _cmb.Alert(ent, address, location);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} looked up deep address {address} from unhacked terminal #{ent.Comp.TerminalId}");
    }

    private void OnTool(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerToolMessage args)
    {
        var user = args.Actor;
        _ui.CloseUi(ent.Owner, CMUComputerToolUiKey.Key, user);
        switch (args.Action)
        {
            case CMUComputerToolAction.Logs:
                if (!_skills.HasSkill(user, EngineerSkill, LogSkill))
                {
                    _popup.PopupEntity(Loc.GetString("cmu-pc-logs-no-skill"), ent, user, PopupType.SmallCaution);
                    return;
                }

                AddLog(ent, Loc.GetString("cmu-pc-log-read", ("name", Name(user))));
                _ui.OpenUi(ent.Owner, CMUComputerLogUiKey.Key, user);
                _ui.SetUiState(ent.Owner, CMUComputerLogUiKey.Key,
                    new CMUComputerLogState(ent.Comp.TerminalId, new List<CMUComputerLogEntry>(ent.Comp.Log)));
                break;
            case CMUComputerToolAction.Hack:
                if (TryComp(ent, out CMUDeepWebHackableComponent? hackable) && _deep.TryStartHack((ent.Owner, hackable), user))
                    AddLog(ent, Loc.GetString("cmu-pc-log-tamper"));
                break;
        }
    }
}
