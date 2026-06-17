using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Screens/states the ATM can be in. Drives the UI layout sent to the client.
/// </summary>
[Serializable, NetSerializable]
public enum AtmScreen : byte
{
    Welcome,
    PinEntry,
    PinLocked,
    MainMenu,
    Withdraw,
    WithdrawConfirm,
    Deposit,
    DepositAmount,
    RemoteDeposit,
    RemoteDepositAccountNum,
    RemoteDepositAmount,
    RemoteDepositConfirm,
    Transfer,
    TransferAccountNum,
    TransferAmount,
    TransferConfirm,
    Result,
    SkimmerData,
}

/// <summary>
///     One of the 6 side buttons on the ATM UI (3 left, 3 right).
/// </summary>
[Serializable, NetSerializable]
public enum AtmSideButton : byte
{
    L1, L2, L3,
    R1, R2, R3,
}

/// <summary>
///     A captured account entry stored in a network tap skimmer.
/// </summary>
[Serializable, NetSerializable]
public sealed class SkimmedAccount
{
    public int AccountNumber;
    public string Name = string.Empty;
    public int Pin;
}

/// <summary>
///     Transaction types for the bank transaction log.
/// </summary>
[Serializable, NetSerializable]
public enum BankTransactionType : byte
{
    Withdraw,
    Deposit,
    RemoteDeposit,
    Transfer,
    TransferReceive,
    Salary,
    AdminCredit,
    AdminDebit,
}

/// <summary>
///     A single entry in the bank transaction log.
/// </summary>
[Serializable, NetSerializable]
public sealed class BankTransaction
{
    public TimeSpan Timestamp;
    public int AccountNumber;
    public string AccountName = string.Empty;
    public BankTransactionType Type;
    public int Amount;
    public int? RecipientAccount;
    public string? Note;
}

/// <summary>
///     Snapshot of an account for the admin bank console.
/// </summary>
[Serializable, NetSerializable]
public sealed class BankAccountInfo
{
    public int AccountNumber;
    public string Name = string.Empty;
    public string JobTitle = string.Empty;
    public int Balance;
}
