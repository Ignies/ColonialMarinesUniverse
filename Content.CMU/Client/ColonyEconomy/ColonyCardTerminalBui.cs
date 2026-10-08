using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed class ColonyCardTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly SoundSpecifier KeySound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_key.wav");

    private ColonyCardTerminalWindow? _window;
    private int _saleId;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ColonyCardTerminalWindow>();
        _window.SetCustomer(Equals(UiKey, ColonyCardTerminalUi.Customer));
        _window.KeyPressed += key =>
        {
            EntMan.System<SharedAudioSystem>().PlayGlobal(KeySound, Filter.Local(), false,
                AudioParams.Default.WithVolume(-6f).WithPitchScale(0.9f + (int) key % 10 * 0.03f));
            SendMessage(new ColonyCardTerminalKeyMsg(key, _saleId));
        };
        _window.TapPressed += () => SendMessage(new ColonyCardTerminalTapMsg(_saleId));
        _window.TakeReceiptPressed += () => SendMessage(new ColonyCardTerminalTakeReceiptMsg());

        if (State is ColonyCardTerminalBuiState state)
            UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ColonyCardTerminalBuiState s || _window == null)
            return;

        _saleId = s.SaleId;
        _window.UpdateState(s);
    }
}
