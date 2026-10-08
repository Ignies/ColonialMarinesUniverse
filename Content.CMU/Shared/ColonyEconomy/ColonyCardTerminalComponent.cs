using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>Handheld card terminal: key in an amount, use it on the customer, they tap and enter their PIN.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ColonyCardTerminalComponent : Component
{
    public const string ReceiptSlotId = "card_terminal_receipt";
    public const int MaxAmountDigits = 6;

    /// <summary>The account that registered the terminal; only its card and PIN open the setup.</summary>
    public int OwnerAccount;

    public int PayoutAccount;

    [DataField]
    public bool TipsEnabled;

    [DataField]
    public int[] TipPercents = { 10, 15, 20 };

    public CardTerminalScreen Screen = CardTerminalScreen.Register;
    public string Buffer = string.Empty;
    public string Status = string.Empty;
    public EntityUid? PendingCard;

    public int Amount;
    public int SaleId;
    public EntityUid? Customer;
    public CardTerminalStep Step;
    public int TipPercent;
    public EntityUid? TappedCard;
    public TimeSpan RequestExpires;

    public EntityUid? SetupUser;
    public TimeSpan SetupExpires;
    public int TipPresetEditing;

    public TimeSpan? TappedAt;
    public TimeSpan? ApprovedAt;
    public TimeSpan? DeclinedAt;
    public TimeSpan? ReceiptPrintedAt;
    public CardReceipt? LastReceipt;

    [DataField]
    public TimeSpan CustomerTimeout = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan SetupTimeout = TimeSpan.FromSeconds(30);

    [DataField]
    public float CustomerRange = 2f;

    [DataField]
    public SoundSpecifier TapSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");

    [DataField]
    public SoundSpecifier ApproveSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    [DataField]
    public SoundSpecifier DeclineSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
}

public enum CardTerminalScreen : byte
{
    Register,
    RegisterPin,
    Ready,
    Armed,
    Waiting,
    Approved,
    Declined,
    SetupAuth,
    SetupPin,
    Setup,
    SetupPayout,
    SetupTip,
}

public enum CardTerminalStep : byte
{
    None,
    Tip,
    Tap,
    Pin,
    Approved,
    Declined,
}

/// <summary>What a card receipt says, kept so the merchant can print a copy.</summary>
public sealed record CardReceipt(
    string Reference,
    TimeSpan Time,
    string MerchantName,
    int MerchantAccount,
    int CustomerAccount,
    int Amount,
    int Tip,
    int TipPercent);
