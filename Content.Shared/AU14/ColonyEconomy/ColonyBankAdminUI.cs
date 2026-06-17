using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

// ── UI Key ────────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public enum ColonyBankAdminUi
{
    Key,
}

// ── BUI States ────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class ColonyBankAdminBuiState : BoundUserInterfaceState
{
    public List<BankAccountInfo> Accounts { get; }
    public List<BankTransaction> Transactions { get; }

    public ColonyBankAdminBuiState(List<BankAccountInfo> accounts, List<BankTransaction> transactions)
    {
        Accounts = accounts;
        Transactions = transactions;
    }
}

// ── BUI Messages ─────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class BankAdminCreditBuiMsg : BoundUserInterfaceMessage
{
    public int AccountNumber { get; }
    public int Amount { get; }
    public string? Note { get; }

    public BankAdminCreditBuiMsg(int accountNumber, int amount, string? note)
    {
        AccountNumber = accountNumber;
        Amount = amount;
        Note = note;
    }
}

[Serializable, NetSerializable]
public sealed class BankAdminDebitBuiMsg : BoundUserInterfaceMessage
{
    public int AccountNumber { get; }
    public int Amount { get; }
    public string? Note { get; }

    public BankAdminDebitBuiMsg(int accountNumber, int amount, string? note)
    {
        AccountNumber = accountNumber;
        Amount = amount;
        Note = note;
    }
}

[Serializable, NetSerializable]
public sealed class BankAdminUnlockCardBuiMsg : BoundUserInterfaceMessage
{
    public int AccountNumber { get; }
    public BankAdminUnlockCardBuiMsg(int accountNumber) => AccountNumber = accountNumber;
}
