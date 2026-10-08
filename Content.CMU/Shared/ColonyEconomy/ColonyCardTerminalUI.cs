using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

[Serializable, NetSerializable]
public enum ColonyCardTerminalUi : byte
{
    Merchant,
    Customer,
}

[Flags]
[Serializable, NetSerializable]
public enum CardTerminalKeys : byte
{
    None = 0,
    Digits = 1 << 0,
    Menu = 1 << 1,
    Cancel = 1 << 2,
    Clear = 1 << 3,
    Enter = 1 << 4,
    EnterReady = 1 << 5,
}

[Serializable, NetSerializable]
public enum CardTerminalKey : byte
{
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    DoubleZero,
    Menu,
    Cancel,
    Clear,
    Enter,
}

/// <summary>The screen as the server composes it, so both windows show the same machine.</summary>
[Serializable, NetSerializable]
public sealed class ColonyCardTerminalBuiState(
    string header,
    string body,
    string? input,
    CardTerminalKeys litKeys,
    bool tapReady,
    TimeSpan? tappedAt,
    TimeSpan? approvedAt,
    TimeSpan? declinedAt,
    bool receiptWaiting,
    TimeSpan? receiptPrintedAt,
    int saleId) : BoundUserInterfaceState
{
    public string Header { get; } = header;
    public string Body { get; } = body;

    /// <summary>The input line under the body, already masked for PINs; null when nothing is typed here.</summary>
    public string? Input { get; } = input;

    public CardTerminalKeys LitKeys { get; } = litKeys;
    public bool TapReady { get; } = tapReady;
    public TimeSpan? TappedAt { get; } = tappedAt;
    public TimeSpan? ApprovedAt { get; } = approvedAt;
    public TimeSpan? DeclinedAt { get; } = declinedAt;
    public bool ReceiptWaiting { get; } = receiptWaiting;
    public TimeSpan? ReceiptPrintedAt { get; } = receiptPrintedAt;
    public int SaleId { get; } = saleId;
}

[Serializable, NetSerializable]
public sealed class ColonyCardTerminalKeyMsg(CardTerminalKey key, int saleId) : BoundUserInterfaceMessage
{
    public CardTerminalKey Key { get; } = key;

    /// <summary>The sale the customer's screen showed, so a changed sale can't be confirmed blind.</summary>
    public int SaleId { get; } = saleId;
}

[Serializable, NetSerializable]
public sealed class ColonyCardTerminalTapMsg(int saleId) : BoundUserInterfaceMessage
{
    public int SaleId { get; } = saleId;
}

[Serializable, NetSerializable]
public sealed class ColonyCardTerminalTakeReceiptMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum CardTerminalVisuals : byte
{
    Receipt,
}

[Serializable, NetSerializable]
public enum CardTerminalReceiptVisual : byte
{
    None,
    Printing,
    Waiting,
}
