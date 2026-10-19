using Content.Client.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.CMU14.PersonalComputers;

public sealed partial class CMUPersonalComputerBui : BoundUserInterface, ICMUNetBoardHost, ICMUDeepHost, ICMUComputerHost
{
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    private CMUPersonalComputerWindow? _window;
    private CMUComputerOsPrototype? _os;

    public event Action<CMUNetBoardStateMessage>? StateReceived;
    public event Action<CMUDeepPageMessage>? PageReceived;
    public event Action<CMUCmbStateMessage>? CmbReceived;

    public CMUPersonalComputerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent(Owner, out CMUPersonalComputerComponent? computer)
            || !_prototype.TryIndex(computer.Os, out _os))
        {
            Close();
            return;
        }

        var net = EntMan.System<CMUExtraNetSystem>();
        _window = this.CreateWindow<CMUPersonalComputerWindow>();
        _window.OnKeyTyped += () => Play(_os.KeySound, -8f, _random.NextFloat(0.9f, 1.1f));
        _window.OnClicked += () => Play(_os.ClickSound, -6f, 1f);
        _window.Boot(_os, net.GetNetworks((Owner, computer)), net.GetHomePage(computer), computer.Documents,
            EntMan.GetComponent<MetaDataComponent>(Owner).EntityName, this, computer.Colony, computer.ColonyName,
            EntMan.HasComponent<CMUDeepWebAccessComponent>(Owner) ? this : null, this);
        Play(_os.BootSound, -4f, 1f);
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
            case CMUCmbStateMessage cmb:
                CmbReceived?.Invoke(cmb);
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

    public bool RequireLogin => Computer?.RequireLogin ?? false;

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

    public void Post(ProtoId<CMUNetSitePrototype> site, string title, string body)
    {
        SendMessage(new CMUNetBoardPostMessage(site, title, body));
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
