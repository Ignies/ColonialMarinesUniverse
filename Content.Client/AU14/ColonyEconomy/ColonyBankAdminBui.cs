using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class ColonyBankAdminBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ColonyBankAdminWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ColonyBankAdminWindow>();

        _window.OnCredit += (acct, amount, note) =>
            SendPredictedMessage(new BankAdminCreditBuiMsg(acct, amount, note));

        _window.OnDebit += (acct, amount, note) =>
            SendPredictedMessage(new BankAdminDebitBuiMsg(acct, amount, note));

        _window.OnUnlock += acct =>
            SendPredictedMessage(new BankAdminUnlockCardBuiMsg(acct));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not ColonyBankAdminBuiState s)
            return;

        _window.Populate(s);
    }
}
