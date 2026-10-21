using Content.Server.Administration.Logs;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Tools;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Sessions on personal computers. Every terminal gets a number for the round and a front panel: a power button
/// and an ID card reader. A card left in the reader keeps its holder logged in; taking it out logs them out. The
/// terminal keeps a log of logins and pages visited that engineers read with a multitool. A multitool opens a radial menu: read the logs, hack the terminal,
/// or retune it off the colony network. Every lookup of a live deep web address from a terminal that is not hacked
/// is reported to the Marshals and colony security; refused cards and log tampering go to security. A dark terminal
/// keeps its own log but reports nothing.
/// </summary>
public sealed partial class CMUComputerSessionSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";
    private const int LogSkill = 1;
    private const int TamperSkill = 3;
    private static readonly SoundSpecifier CardInSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_card_in.wav");
    private static readonly SoundSpecifier CardOutSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_card_out.wav");
    private static readonly SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/CMU14/PersonalComputers/error.ogg");

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private CMUCmbSystem _cmb = default!;
    [Dependency] private CMUComputerPowerSystem _computerPower = default!;
    [Dependency] private CMUDeepAliasSystem _aliases = default!;
    [Dependency] private CMUDeepWebSystem _deep = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private CMUSecurityNetSystem _security = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
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
        SubscribeLocalEvent<CMUPersonalComputerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CMUPersonalComputerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUPersonalComputerComponent, EntInsertedIntoContainerMessage>(OnCardInserted);
        SubscribeLocalEvent<CMUPersonalComputerComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUNetVisitMessage>(OnVisit);
            subs.Event<CMUComputerLogoutMessage>(OnLogout);
            subs.Event<CMUComputerGuestLoginMessage>(OnGuestLogin);
            subs.Event<CMUComputerPowerButtonMessage>(OnPowerButton);
            subs.Event<CMUComputerCardSlotMessage>(OnCardSlot);
            subs.Event<CMUComputerOpenConsoleMessage>(OnOpenConsole);
            subs.Event<CMUComputerScreenMessage>(OnScreen);
        });
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUComputerToolUiKey.Key, subs =>
        {
            subs.Event<CMUComputerToolMessage>(OnTool);
        });
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUComputerLogUiKey.Key, subs =>
        {
            subs.Event<CMUComputerLogWipeMessage>(OnWipe);
            subs.Event<CMUComputerLogForgeMessage>(OnForge);
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

    /// <summary>Whether the terminal has power; see <see cref="CMUComputerPowerSystem"/>.</summary>
    public bool IsPowered(EntityUid computer)
    {
        return _computerPower.IsPowered(computer);
    }

    /// <summary>A session left open is there for anyone to see: the screen shows who is still logged in.</summary>
    private void OnExamined(Entity<CMUPersonalComputerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_cfg.GetCVar(CMUPersonalComputerCVars.Enabled))
            return;

        args.PushMarkup(Loc.GetString("cmu-pc-examine-terminal", ("id", ent.Comp.TerminalId)));
        if (ent.Comp.CardPrototype != null)
            args.PushMarkup(Loc.GetString("cmu-pc-examine-card"));

        // What security walks over to check: a dark terminal shows it on its front.
        if (_net.IsDark(ent.Comp))
            args.PushMarkup(Loc.GetString("cmu-pc-examine-offline"));

        if (!ent.Comp.On)
            args.PushMarkup(Loc.GetString("cmu-pc-examine-dark"));
        else if (ent.Comp.SessionUser is { } user)
            args.PushMarkup(Loc.GetString("cmu-pc-examine-session", ("name", user)));
        else if (ent.Comp.Guest)
            args.PushMarkup(Loc.GetString("cmu-pc-examine-guest"));
        else
            args.PushMarkup(Loc.GetString("cmu-pc-examine-login"));
    }

    /// <summary>Losing power switches the terminal off mid sentence. It stays off until someone presses the button.</summary>
    private void OnPowerChanged(Entity<CMUPersonalComputerComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered || IsPowered(ent))
            return;

        SwitchOff(ent);
        _ui.CloseUi(ent.Owner, CMUDeepHackUiKey.Key);
        _ui.CloseUi(ent.Owner, CMUComputerLogUiKey.Key);
    }

    /// <summary>Does nothing without power. Switching on logs in the holder of the card in the reader.</summary>
    private void OnPowerButton(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerPowerButtonMessage args)
    {
        if (ent.Comp.On)
        {
            SwitchOff(ent);
            return;
        }

        if (!IsPowered(ent))
            return;

        ent.Comp.On = true;
        ent.Comp.PoweredAt = _timing.CurTime;
        ent.Comp.Screen.Clear();
        Dirty(ent);
        if (GetCard(ent) is { } card)
            LogIn(ent, card, args.Actor);
    }

    private void SwitchOff(Entity<CMUPersonalComputerComponent> ent)
    {
        if (!ent.Comp.On)
            return;

        ent.Comp.On = false;
        ent.Comp.Screen.Clear();
        EndSession(ent);
        CloseConsoleApps(ent);
        Dirty(ent);
    }

    /// <summary>
    /// A console program opens the console's own interface in a window of its own, the way using the console did
    /// before it ran the OS. It needs the machine on and, with logins on, a card in the reader.
    /// </summary>
    private void OnOpenConsole(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerOpenConsoleMessage args)
    {
        if (!ent.Comp.On
            || _cfg.GetCVar(CMUPersonalComputerCVars.Login) && ent.Comp.RequireLogin && ent.Comp.SessionUser == null
            || !TryComp(ent, out CMUComputerConsoleComponent? console)
            || args.App < 0
            || args.App >= console.Apps.Count)
            return;

        var key = console.Apps[args.App].Key;
        // Consoles fill in their interface's state just before it opens.
        RaiseLocalEvent(ent, new BeforeActivatableUIOpenEvent(args.Actor));
        _ui.OpenUi(ent.Owner, key, args.Actor);
        if (_ui.IsUiOpen(ent.Owner, key, args.Actor))
            RaiseLocalEvent(ent, new AfterActivatableUIOpenEvent(args.Actor));
    }

    private void CloseConsoleApps(EntityUid computer)
    {
        if (!TryComp(computer, out CMUComputerConsoleComponent? console))
            return;

        foreach (var app in console.Apps)
            _ui.CloseUi(computer, app.Key);
    }

    /// <summary>The ID card sitting in the terminal's reader, if any.</summary>
    public EntityUid? GetCard(EntityUid computer)
    {
        return _container.TryGetContainer(computer, CMUPersonalComputerComponent.CardSlotId, out var slot) && slot is ContainerSlot cardSlot
            ? cardSlot.ContainedEntity
            : null;
    }

    private void OnCardSlot(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerCardSlotMessage args)
    {
        if (GetCard(ent) is { } card)
        {
            EjectCard(ent, card, args.Actor);
            return;
        }

        // The card the user would reach for, as at the ATM: one in hand first, else the one they wear.
        foreach (var held in _hands.EnumerateHeld(args.Actor))
        {
            if (!HasComp<IdCardComponent>(held))
                continue;

            TryInsertCard(ent, held, args.Actor);
            return;
        }

        if (_idCards.TryFindIdCard(args.Actor, out var worn))
        {
            TryInsertCard(ent, worn.Owner, args.Actor);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-pc-card-none"), ent, args.Actor);
    }

    public bool TryInsertCard(Entity<CMUPersonalComputerComponent> ent, EntityUid card, EntityUid user)
    {
        if (GetCard(ent) != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-pc-card-occupied"), ent, user);
            return false;
        }

        var slot = _container.EnsureContainer<ContainerSlot>(ent, CMUPersonalComputerComponent.CardSlotId);
        if (!_container.Insert(card, slot))
            return false;

        _audio.PlayPvs(CardInSound, ent);
        if (ent.Comp.On)
            LogIn(ent, card, user);

        return true;
    }

    public void EjectCard(Entity<CMUPersonalComputerComponent> ent, EntityUid card, EntityUid user)
    {
        if (!_container.TryGetContainer(ent, CMUPersonalComputerComponent.CardSlotId, out var slot) || !_container.Remove(card, slot))
            return;

        _hands.PickupOrDrop(user, card);
        _audio.PlayPvs(CardOutSound, ent);
    }

    private void OnCardInserted(Entity<CMUPersonalComputerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != CMUPersonalComputerComponent.CardSlotId)
            return;

        ent.Comp.CardPrototype = MetaData(args.Entity).EntityPrototype?.ID;
        Dirty(ent);
    }

    // However the card leaves, ejected, pulled out or deleted, the session goes with it.
    private void OnCardRemoved(Entity<CMUPersonalComputerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != CMUPersonalComputerComponent.CardSlotId || TerminatingOrDeleted(ent))
            return;

        ent.Comp.CardPrototype = null;
        EndSession(ent);
        Dirty(ent);
    }

    private void OnInteractUsing(Entity<CMUPersonalComputerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_cfg.GetCVar(CMUPersonalComputerCVars.Enabled))
            return;

        if (HasComp<MultitoolComponent>(args.Used))
        {
            args.Handled = true;
            _ui.OpenUi(ent.Owner, CMUComputerToolUiKey.Key, args.User);
            return;
        }

        if (!HasComp<IdCardComponent>(args.Used))
            return;

        args.Handled = true;
        TryInsertCard(ent, args.Used, args.User);
    }

    private void LogIn(Entity<CMUPersonalComputerComponent> ent, EntityUid cardUid, EntityUid user)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Login) || !_idCards.TryGetIdCard(cardUid, out var card))
            return;

        if (_accessReader.GetMainAccessReader(ent, out var reader)
            && !_accessReader.AreAccessTagsAllowed(_accessReader.FindAccessTags(cardUid), reader.Value.Comp))
        {
            var refused = card.Comp.FullName ?? Name(card);
            AddLog(ent, Loc.GetString("cmu-pc-log-denied", ("name", refused)));
            _security.Report(ent.Owner, CMUSecurityEvent.LoginRefused, refused);
            _popup.PopupEntity(Loc.GetString("cmu-pc-access-denied"), ent, user, PopupType.SmallCaution);
            _audio.PlayPvs(DeniedSound, ent);
            EjectCard(ent, cardUid, user);
            return;
        }

        var name = card.Comp.FullName ?? Name(card);
        var job = card.Comp.LocalizedJobTitle;
        var account = card.Comp.AccountNumber;
        // A card at a guest session turns it into this user's own, on a clean screen.
        if (ent.Comp.Guest)
        {
            ent.Comp.Guest = false;
            ent.Comp.Screen.Clear();
        }

        ent.Comp.SessionUser = name;
        ent.Comp.SessionJob = job;
        ent.Comp.SessionAccount = account;
        Dirty(ent);
        AddLog(ent, Loc.GetString("cmu-pc-log-login", ("name", name)));
        _popup.PopupEntity(Loc.GetString("cmu-pc-login-popup", ("name", name)), ent, user);
    }

    /// <summary>Logging off from the start menu hands the card back.</summary>
    /// <summary>Whether this terminal's login screen offers guest browsing.</summary>
    public bool GuestsAllowed(CMUPersonalComputerComponent computer)
    {
        return computer.AllowGuests
               ?? _prototype.TryIndex(computer.Os, out var os) && os.AllowGuests;
    }

    private void OnGuestLogin(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerGuestLoginMessage args)
    {
        if (!ent.Comp.On
            || ent.Comp.SessionUser != null
            || ent.Comp.Guest
            || !GuestsAllowed(ent.Comp))
            return;

        ent.Comp.Guest = true;
        ent.Comp.Screen.Clear();
        Dirty(ent);
        AddLog(ent, Loc.GetString("cmu-pc-log-guest"));
    }

    private void OnLogout(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerLogoutMessage args)
    {
        if (GetCard(ent) is { } card)
            EjectCard(ent, card, args.Actor);
        else
            EndSession(ent);
    }

    /// <summary>Keeps what the running session has open, for whoever looks at the terminal next.</summary>
    private void OnScreen(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerScreenMessage args)
    {
        if (!ent.Comp.On
            || _cfg.GetCVar(CMUPersonalComputerCVars.Login) && ent.Comp.RequireLogin && ent.Comp.SessionUser == null && !ent.Comp.Guest)
            return;

        var screen = new List<CMUComputerScreenWindow>();
        foreach (var window in args.Windows)
        {
            if (screen.Count >= CMUComputerScreenLimits.Windows)
                break;

            if (string.IsNullOrEmpty(window.App)
                || window.App.Length > CMUComputerScreenLimits.AppLength
                || window.Address is { Length: > CMUComputerScreenLimits.AddressLength })
                continue;

            screen.Add(new CMUComputerScreenWindow
            {
                App = window.App,
                Address = window.Address,
                Position = Clamp(window.Position),
                Size = Clamp(window.Size),
                Minimized = window.Minimized,
            });
        }

        ent.Comp.Screen = screen;
        Dirty(ent);
    }

    private static System.Numerics.Vector2 Clamp(System.Numerics.Vector2 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            return System.Numerics.Vector2.Zero;

        return System.Numerics.Vector2.Clamp(value, System.Numerics.Vector2.Zero,
            new System.Numerics.Vector2(CMUComputerScreenLimits.Extent));
    }

    private void EndSession(Entity<CMUPersonalComputerComponent> ent)
    {
        CloseConsoleApps(ent);
        if (ent.Comp.Guest)
        {
            ent.Comp.Guest = false;
            ent.Comp.Screen.Clear();
            Dirty(ent);
            AddLog(ent, Loc.GetString("cmu-pc-log-guest-out"));
        }

        if (ent.Comp.SessionUser is not { } name)
            return;

        AddLog(ent, Loc.GetString("cmu-pc-log-logout", ("name", name)));
        ent.Comp.Screen.Clear();
        ent.Comp.SessionUser = null;
        ent.Comp.SessionJob = null;
        ent.Comp.SessionAccount = 0;
        Dirty(ent);
    }

    private void OnGetVerbs(Entity<CMUPersonalComputerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.OpenVerb || !args.CanAccess || !args.CanInteract || !_cfg.GetCVar(CMUPersonalComputerCVars.Enabled))
            return;

        var uid = ent.Owner;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-pc-open-terminal"),
            Act = () => _ui.OpenUi(uid, CMUPersonalComputerUiKey.Key, user),
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

        // A dark terminal's pages stay in its own log; nothing goes out live.
        var host = CMUExtraNetSystem.Split(address).Host;
        if (HasComp<CMUDeepWebAccessComponent>(ent) || _net.IsDark(ent.Comp) || !_aliases.TryGetSite(host, out _))
            return;

        var location = _security.Location(ent);
        _cmb.Alert(ent, address, location);
        _security.Report(ent.Owner, CMUSecurityEvent.DeepLookup, detail: address);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} looked up deep address {address} from unhacked terminal #{ent.Comp.TerminalId}");
    }

    private bool CanTamper(EntityUid user)
    {
        return _cfg.GetCVar(CMUPersonalComputerCVars.LogTamper) && _skills.HasSkill(user, EngineerSkill, TamperSkill);
    }

    private void SendLog(Entity<CMUPersonalComputerComponent> ent, EntityUid user)
    {
        _ui.SetUiState(ent.Owner, CMUComputerLogUiKey.Key,
            new CMUComputerLogState(ent.Comp.TerminalId, new List<CMUComputerLogEntry>(ent.Comp.Log), CanTamper(user)));
    }

    /// <summary>Clears the log. The wipe itself always leaves a line, so a wiped terminal still looks wiped.</summary>
    private void OnWipe(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerLogWipeMessage args)
    {
        if (!CanTamper(args.Actor))
            return;

        ent.Comp.Log.Clear();
        AddLog(ent, Loc.GetString("cmu-pc-log-cleared"));
        _security.Report(ent.Owner, CMUSecurityEvent.LogWiped);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.Actor):player} wiped the log of terminal #{ent.Comp.TerminalId}");
        SendLog(ent, args.Actor);
    }

    private void OnForge(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerLogForgeMessage args)
    {
        var text = args.Text.Trim().Replace('\n', ' ');
        if (!CanTamper(args.Actor) || text.Length == 0)
            return;

        if (text.Length > CMUComputerLimits.ForgedLength)
            text = text[..CMUComputerLimits.ForgedLength];

        AddLog(ent, text);
        _security.Report(ent.Owner, CMUSecurityEvent.LogForged);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.Actor):player} forged a log line on terminal #{ent.Comp.TerminalId}: {text}");
        SendLog(ent, args.Actor);
    }

    private void OnTool(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerToolMessage args)
    {
        var user = args.Actor;
        _ui.CloseUi(ent.Owner, CMUComputerToolUiKey.Key, user);
        switch (args.Action)
        {
            case CMUComputerToolAction.Logs:
                if (!_cfg.GetCVar(CMUPersonalComputerCVars.Logs))
                    return;

                if (!_skills.HasSkill(user, EngineerSkill, LogSkill))
                {
                    _popup.PopupEntity(Loc.GetString("cmu-pc-logs-no-skill"), ent, user, PopupType.SmallCaution);
                    return;
                }

                AddLog(ent, Loc.GetString("cmu-pc-log-read", ("name", Name(user))));
                _ui.OpenUi(ent.Owner, CMUComputerLogUiKey.Key, user);
                SendLog(ent, user);
                break;
            case CMUComputerToolAction.Hack or CMUComputerToolAction.Retune when !IsPowered(ent):
                _popup.PopupEntity(Loc.GetString("cmu-pc-no-power"), ent, user, PopupType.SmallCaution);
                break;
            case CMUComputerToolAction.Hack:
                if (TryComp(ent, out CMUDeepWebHackableComponent? hackable) && _deep.TryStartHack((ent.Owner, hackable), user))
                    AddLog(ent, Loc.GetString("cmu-pc-log-tamper"));
                break;
            case CMUComputerToolAction.Retune:
                if (TryComp(ent, out CMUDeepWebHackableComponent? tunable) && _deep.TryStartRetune((ent.Owner, tunable), user))
                    AddLog(ent, Loc.GetString("cmu-pc-log-tamper"));
                break;
        }
    }
}
