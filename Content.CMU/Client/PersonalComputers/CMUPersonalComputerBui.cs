using Content.Client.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.CMU14.PersonalComputers;

public sealed partial class CMUPersonalComputerBui : BoundUserInterface, ICMUNetBoardHost, ICMUDeepHost, ICMUComputerHost
{
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    private CMUPersonalComputerWindow? _window;
    private CMUComputerOsPrototype? _os;

    public event Action<CMUNetBoardStateMessage>? StateReceived;
    public event Action<CMUDeepPageMessage>? PageReceived;
    public event Action<CMUMarketStateMessage>? MarketReceived;
    public event Action<CMUDeepToolResultMessage>? ToolResult;
    public event Action<CMUCmbStateMessage>? CmbReceived;
    public event Action<CMUMailStateMessage>? MailReceived;
    public event Action<CMUSpaceGptAnswerMessage>? SpaceGptAnswered;
    public event Action<CMUArcadeStateMessage>? ArcadeReceived;
    public event Action<CMUBetStateMessage>? BetsReceived;

    public CMUPersonalComputerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Enabled)
            || !EntMan.TryGetComponent(Owner, out CMUPersonalComputerComponent? computer)
            || !_prototype.TryIndex(computer.Os, out _os))
        {
            Close();
            return;
        }

        var net = EntMan.System<CMUExtraNetSystem>();
        _window = this.CreateWindow<CMUPersonalComputerWindow>();
        _window.OnKeyTyped += () => Play(_os.KeySound, -8f, _random.NextFloat(0.9f, 1.1f));
        _window.OnClicked += () => Play(_os.ClickSound, -6f, 1f);
        _window.OnBooting += () => Play(_os.BootSound, -4f, 1f);
        _window.Boot(_os, net.GetNetworks((Owner, computer)), net.GetHomePage(computer), computer.Documents,
            EntMan.GetComponent<MetaDataComponent>(Owner).EntityName, this, computer.Colony, computer.ColonyName,
            EntMan.HasComponent<CMUDeepWebAccessComponent>(Owner) && _cfg.GetCVar(CMUPersonalComputerCVars.DeepWeb) ? this : null,
            this,
            computer.Casing);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        switch (message)
        {
            case CMUNetBoardStateMessage state:
                StateReceived?.Invoke(state);
                break;
            case CMUDeepPageMessage page:
                PageReceived?.Invoke(page);
                break;
            case CMUMarketStateMessage market:
                MarketReceived?.Invoke(market);
                break;
            case CMUDeepToolResultMessage tool:
                ToolResult?.Invoke(tool);
                break;
            case CMUCmbStateMessage cmb:
                CmbReceived?.Invoke(cmb);
                break;
            case CMUMailStateMessage mail:
                MailReceived?.Invoke(mail);
                break;
            case CMUSpaceGptAnswerMessage answer:
                SpaceGptAnswered?.Invoke(answer);
                break;
            case CMUArcadeStateMessage arcade:
                ArcadeReceived?.Invoke(arcade);
                break;
            case CMUBetStateMessage bets:
                BetsReceived?.Invoke(bets);
                break;
        }
    }

    private static readonly List<string> NothingFound = new();
    private static readonly HashSet<ProtoId<CMUNetSitePrototype>> NothingUnlocked = new();
    private static readonly Dictionary<ProtoId<CMUNetSitePrototype>, string> NoAliases = new();
    private static readonly List<CMUNetShortcut> NoShortcuts = new();

    public IReadOnlyList<string> Discovered =>
        EntMan.TryGetComponent(Owner, out CMUDeepWebAccessComponent? deep) ? deep.Discovered : NothingFound;

    public IReadOnlySet<ProtoId<CMUNetSitePrototype>> Unlocked =>
        EntMan.TryGetComponent(Owner, out CMUDeepWebAccessComponent? deep) ? deep.Unlocked : NothingUnlocked;

    public IReadOnlyDictionary<ProtoId<CMUNetSitePrototype>, string> Aliases =>
        EntMan.TryGetComponent(Owner, out CMUDeepWebAccessComponent? deep) ? deep.Aliases : NoAliases;

    public bool IsInsurgent => EntMan.HasComponent<CLFMemberComponent>(_player.LocalEntity);

    private CMUPersonalComputerComponent? Computer =>
        EntMan.TryGetComponent(Owner, out CMUPersonalComputerComponent? computer) ? computer : null;

    public int TerminalId => Computer?.TerminalId ?? 0;

    public string? SessionUser => Computer?.SessionUser;

    public string? SessionJob => Computer?.SessionJob;

    public TimeSpan TraceStarted => Computer?.TraceStarted ?? TimeSpan.Zero;

    public TimeSpan TraceEnds => Computer?.TraceEnds ?? TimeSpan.Zero;

    public bool Carrier => Computer?.Carrier ?? true;

    public string? Platoon => Computer?.Platoon;

    public bool On => Computer?.On ?? false;

    public string? CardPrototype => Computer?.CardPrototype;

    public void PressPower()
    {
        SendMessage(new CMUComputerPowerButtonMessage());
    }

    private static readonly List<string> NoConsoleApps = new();

    public IReadOnlyList<string> ConsoleApps =>
        EntMan.TryGetComponent(Owner, out CMUComputerConsoleComponent? console)
            ? console.Apps.ConvertAll(a => a.Name)
            : NoConsoleApps;

    public void OpenConsoleApp(int app)
    {
        SendMessage(new CMUComputerOpenConsoleMessage(app));
    }

    public void UseReader()
    {
        SendMessage(new CMUComputerCardSlotMessage());
    }

    public void Trace(NetEntity terminal)
    {
        SendMessage(new CMUCmbTraceMessage(terminal));
    }

    private static readonly List<CMUComputerNote> NoNotes = new();

    public IReadOnlyList<CMUComputerNote> Notes => Computer?.Notes ?? NoNotes;

    public ProtoId<CMUWallpaperPrototype>? Wallpaper => Computer?.Wallpaper;

    public void RequestMail()
    {
        SendMessage(new CMUMailRequestMessage());
    }

    public void SendMail(string to, string subject, string body)
    {
        SendMessage(new CMUMailSendMessage(to, subject, body));
    }

    public void SaveNote(string name, string text)
    {
        SendMessage(new CMUNoteSaveMessage(name, text));
    }

    public void DeleteNote(int index)
    {
        SendMessage(new CMUNoteDeleteMessage(index));
    }

    public void SetWallpaper(ProtoId<CMUWallpaperPrototype>? wallpaper)
    {
        SendMessage(new CMUNetSetWallpaperMessage(wallpaper));
    }

    public void AskSpaceGpt(string question)
    {
        SendMessage(new CMUSpaceGptAskMessage(question));
    }

    public void RequestArcade(CMUArcadeGame game)
    {
        SendMessage(new CMUArcadeRequestMessage(game));
    }

    public void SubmitScore(CMUArcadeGame game, int score)
    {
        SendMessage(new CMUArcadeSubmitMessage(game, score));
    }

    public void RequestBets()
    {
        SendMessage(new CMUBetRequestMessage());
    }

    public void PlaceBet(int match, int entrant, int amount)
    {
        SendMessage(new CMUBetPlaceMessage(match, entrant, amount));
    }

    public bool RequireLogin => (Computer?.RequireLogin ?? false) && _cfg.GetCVar(CMUPersonalComputerCVars.Login);

    public void Visited(string address, bool found)
    {
        SendMessage(new CMUNetVisitMessage(address, found));
    }

    public void Logout()
    {
        SendMessage(new CMUComputerLogoutMessage());
    }

    public void RequestCmb(CMUCmbPanel panel)
    {
        SendMessage(new CMUCmbRequestMessage(panel));
    }

    public IReadOnlyList<CMUNetShortcut> Shortcuts =>
        EntMan.TryGetComponent(Owner, out CMUPersonalComputerComponent? computer) ? computer.Shortcuts : NoShortcuts;

    public void SaveShortcut(string address, string title)
    {
        SendMessage(new CMUNetSaveShortcutMessage(address, title));
    }

    public void RemoveShortcut(string address)
    {
        SendMessage(new CMUNetRemoveShortcutMessage(address));
    }

    public void RequestPage(string host)
    {
        SendMessage(new CMUDeepPageRequestMessage(host));
    }

    public void OpenCamera(string network)
    {
        SendMessage(new CMUDeepCameraMessage(network));
    }

    public void Hijack(string title, string body)
    {
        SendMessage(new CMUDeepHijackMessage(title, body));
    }

    public void RequestMarket()
    {
        SendMessage(new CMUMarketRequestMessage());
    }

    public void ListItem(string title, string description, int price, string drop)
    {
        SendMessage(new CMUMarketListMessage(title, description, price, drop));
    }

    public void Buy(int listing)
    {
        SendMessage(new CMUMarketBuyMessage(listing));
    }

    public void Sniff(int terminal)
    {
        SendMessage(new CMUDeepSniffMessage(terminal));
    }

    public void Loop(string network)
    {
        SendMessage(new CMUDeepLoopMessage(network));
    }

    public void Restore(ProtoId<CMUNetSitePrototype> site)
    {
        SendMessage(new CMUNetBoardRestoreMessage(site));
    }

    public TimeSpan NextScan =>
        EntMan.TryGetComponent(Owner, out CMUDeepWebAccessComponent? deep) ? deep.NextScan : TimeSpan.Zero;

    public void Scan()
    {
        SendMessage(new CMUDeepScanMessage());
    }

    public void Request(ProtoId<CMUNetSitePrototype> site)
    {
        SendMessage(new CMUNetBoardRequestMessage(site));
    }

    public void Post(ProtoId<CMUNetSitePrototype> site, string title, string body, int reward)
    {
        SendMessage(new CMUNetBoardPostMessage(site, title, body, reward));
    }

    public void Payout(ProtoId<CMUNetSitePrototype> site, int post, int comment)
    {
        SendMessage(new CMUNetBoardPayoutMessage(site, post, comment));
    }

    public void Withdraw(ProtoId<CMUNetSitePrototype> site, int post)
    {
        SendMessage(new CMUNetBoardWithdrawMessage(site, post));
    }

    public void Comment(ProtoId<CMUNetSitePrototype> site, int post, string body)
    {
        SendMessage(new CMUNetBoardCommentMessage(site, post, body));
    }

    public void Delete(ProtoId<CMUNetSitePrototype> site, int post)
    {
        SendMessage(new CMUNetBoardDeleteMessage(site, post));
    }

    private void Play(SoundSpecifier? sound, float volume, float pitch)
    {
        if (sound == null)
            return;

        EntMan.System<AudioSystem>().PlayGlobal(sound, Filter.Local(), false,
            AudioParams.Default.WithVolume(volume).WithPitchScale(pitch));
    }
}
