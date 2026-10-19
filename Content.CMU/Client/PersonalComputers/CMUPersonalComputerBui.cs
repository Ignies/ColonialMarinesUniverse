using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.CMU14.PersonalComputers;

public sealed partial class CMUPersonalComputerBui : BoundUserInterface, ICMUNetBoardHost
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    private CMUPersonalComputerWindow? _window;
    private CMUComputerOsPrototype? _os;

    public event Action<CMUNetBoardStateMessage>? StateReceived;

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
        _window.Boot(_os, net.GetNetworks(computer), net.GetHomePage(computer), computer.Documents,
            EntMan.GetComponent<MetaDataComponent>(Owner).EntityName, this, computer.Colony, computer.ColonyName);
        Play(_os.BootSound, -4f, 1f);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is CMUNetBoardStateMessage state)
            StateReceived?.Invoke(state);
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
