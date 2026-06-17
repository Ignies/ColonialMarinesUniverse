using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class ColonyAtmBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ColonyAtmWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ColonyAtmWindow>();

        // Side buttons
        _window.BtnL1.OnPressed += _ => Send(AtmSideButton.L1);
        _window.BtnL2.OnPressed += _ => Send(AtmSideButton.L2);
        _window.BtnL3.OnPressed += _ => Send(AtmSideButton.L3);
        _window.BtnR1.OnPressed += _ => Send(AtmSideButton.R1);
        _window.BtnR2.OnPressed += _ => Send(AtmSideButton.R2);
        _window.BtnR3.OnPressed += _ => Send(AtmSideButton.R3);

        // Numpad digits
        _window.Btn0.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("0"));
        _window.Btn1.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("1"));
        _window.Btn2.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("2"));
        _window.Btn3.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("3"));
        _window.Btn4.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("4"));
        _window.Btn5.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("5"));
        _window.Btn6.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("6"));
        _window.Btn7.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("7"));
        _window.Btn8.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("8"));
        _window.Btn9.OnPressed += _ => SendPredictedMessage(new ColonyAtmDigitBuiMsg("9"));

        // Control keys
        _window.BtnConfirm.OnPressed   += _ => SendPredictedMessage(new ColonyAtmConfirmBuiMsg());
        _window.BtnCancel.OnPressed    += _ => SendPredictedMessage(new ColonyAtmCancelBuiMsg());
        _window.BtnBackspace.OnPressed += _ => SendPredictedMessage(new ColonyAtmBackspaceBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not ColonyAtmBuiState s)
            return;

        _window.UpdateDisplay(s);
    }

    private void Send(AtmSideButton btn)
        => SendPredictedMessage(new ColonyAtmSideButtonBuiMsg(btn));
}

