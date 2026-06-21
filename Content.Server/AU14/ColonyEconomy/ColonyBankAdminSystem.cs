using Content.Shared.Access.Components;
using Content.Shared.AU14.ColonyEconomy;
using Robust.Server.GameObjects;

namespace Content.Server.AU14.ColonyEconomy;

/// <summary>
///     Powers the admin bank console. Lists all accounts, full transaction log,
///     and allows admins to credit/debit accounts or unlock locked cards.
/// </summary>
public sealed partial class ColonyBankAdminSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ColonyBankSystem _bank = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyBankAdminComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ColonyBankAdminComponent, BankAdminCreditBuiMsg>(OnCredit);
        SubscribeLocalEvent<ColonyBankAdminComponent, BankAdminDebitBuiMsg>(OnDebit);
        SubscribeLocalEvent<ColonyBankAdminComponent, BankAdminUnlockCardBuiMsg>(OnUnlock);
    }

    private void OnUiOpened(EntityUid uid, ColonyBankAdminComponent comp, BoundUIOpenedEvent args)
    {
        RefreshUi(uid);
    }

    private void OnCredit(EntityUid uid, ColonyBankAdminComponent comp, BankAdminCreditBuiMsg msg)
    {
        if (msg.Amount <= 0) return;

        var found = _bank.FindAccount(msg.AccountNumber);
        if (found == null) return;

        found.Value.card.AccountBalance += msg.Amount;
        Dirty(found.Value.uid, found.Value.card);
        _bank.LogTransaction(found.Value.card.AccountNumber, found.Value.card.FullName ?? "Unknown",
            BankTransactionType.AdminCredit, msg.Amount, note: msg.Note ?? "Admin credit");

        RefreshAllAdminUi();
    }

    private void OnDebit(EntityUid uid, ColonyBankAdminComponent comp, BankAdminDebitBuiMsg msg)
    {
        if (msg.Amount <= 0) return;

        var found = _bank.FindAccount(msg.AccountNumber);
        if (found == null) return;

        var deduct = Math.Min(msg.Amount, found.Value.card.AccountBalance);
        found.Value.card.AccountBalance -= deduct;
        Dirty(found.Value.uid, found.Value.card);
        _bank.LogTransaction(found.Value.card.AccountNumber, found.Value.card.FullName ?? "Unknown",
            BankTransactionType.AdminDebit, deduct, note: msg.Note ?? "Admin debit");

        RefreshAllAdminUi();
    }

    private void OnUnlock(EntityUid uid, ColonyBankAdminComponent comp, BankAdminUnlockCardBuiMsg msg)
    {
        var found = _bank.FindAccount(msg.AccountNumber);
        if (found == null) return;

        found.Value.card.PinLockedUntil = null;
        found.Value.card.PinAttempts = 0;
        Dirty(found.Value.uid, found.Value.card);

        RefreshAllAdminUi();
    }

    private void RefreshUi(EntityUid uid)
    {
        var state = new ColonyBankAdminBuiState(_bank.GetAllAccounts(), _bank.GetTransactions());
        _ui.SetUiState(uid, ColonyBankAdminUi.Key, state);
    }

    private void RefreshAllAdminUi()
    {
        var state = new ColonyBankAdminBuiState(_bank.GetAllAccounts(), _bank.GetTransactions());
        var query = EntityQueryEnumerator<ColonyBankAdminComponent>();
        while (query.MoveNext(out var uid, out _))
            _ui.SetUiState(uid, ColonyBankAdminUi.Key, state);
    }
}
