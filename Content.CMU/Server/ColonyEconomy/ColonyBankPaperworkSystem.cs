using System.Globalization;
using System.Linq;
using System.Text;
using Content.Server.GameTicking;
using Content.Shared.Clock;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>Bank statements, transfer certificates and card receipts, printed into a machine's receipt slot.</summary>
public sealed class ColonyBankPaperworkSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private GameTicker _ticker = default!;

    private static readonly EntProtoId PaperPrototype = "CMUPaperWEYLAND";
    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg");
    private const string StampState = "paper_stamp-we_ya";
    private static readonly Color StampColour = Color.FromHex("#3681bb");

    private const string DateFormat = "dd MMMM, yyyy - HH:mm";
    private const string LineDateFormat = "dd MMM HH:mm";

    public EntityUid? GetWaitingPaper(EntityUid machine, string slotId)
    {
        return _container.TryGetContainer(machine, slotId, out var slot) && slot is ContainerSlot receiptSlot
            ? receiptSlot.ContainedEntity
            : null;
    }

    /// <summary>Prints into the machine's receipt slot; refuses while a paper is still waiting there.</summary>
    public bool TryPrint(EntityUid machine, string slotId, string name, string markup)
    {
        if (GetWaitingPaper(machine, slotId) != null)
            return false;

        // Map coordinates: a handheld terminal's own are its holder's.
        var paper = Spawn(PaperPrototype, _transform.GetMapCoordinates(machine));
        if (!TryComp<PaperComponent>(paper, out var paperComp))
        {
            Del(paper);
            return false;
        }

        _paper.SetContent((paper, paperComp), markup);
        _paper.TryStamp((paper, paperComp),
            new StampDisplayInfo { StampedName = "cmu-bank-stamp-name", StampedColor = StampColour },
            StampState);
        paperComp.EditingDisabled = true;
        Dirty(paper, paperComp);
        _metaData.SetEntityName(paper, name);

        var slot = _container.EnsureContainer<ContainerSlot>(machine, slotId);
        if (!_container.Insert(paper, slot))
        {
            Del(paper);
            return false;
        }

        _audio.PlayPvs(PrintSound, machine);
        return true;
    }

    public bool TryTake(EntityUid machine, string slotId, EntityUid user)
    {
        if (GetWaitingPaper(machine, slotId) is not { } paper ||
            !_container.TryGetContainer(machine, slotId, out var slot) ||
            !_container.Remove(paper, slot))
        {
            return false;
        }

        _hands.PickupOrDrop(user, paper);
        return true;
    }

    /// <summary>Pushes the waiting paper out to fall where the machine is.</summary>
    public void Eject(EntityUid machine, string slotId)
    {
        if (GetWaitingPaper(machine, slotId) is { } paper && _container.TryGetContainer(machine, slotId, out var slot))
            _container.Remove(paper, slot);
    }

    // ─── Documents ─────────────────────────────────────────────────────────

    public string Statement(string holder, int account, int balance, IReadOnlyList<ColonyAccountHistoryEntry> history)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[head=2]{Loc.GetString("cmu-bank-statement-title")}[/head]");
        Field(sb, "cmu-bank-field-holder", Escape(holder));
        Field(sb, "cmu-bank-field-account", $"#{account}");
        Field(sb, "cmu-bank-field-issued", Now());
        Field(sb, "cmu-bank-field-balance", Money(balance));
        sb.AppendLine();

        if (history.Count == 0)
        {
            sb.AppendLine(Loc.GetString("cmu-bank-statement-empty"));
        }
        else
        {
            sb.AppendLine("[mono]");
            sb.AppendLine(Loc.GetString("cmu-bank-statement-columns"));
            foreach (var entry in history.Reverse())
            {
                var amount = $"{(IsCredit(entry.Kind) ? '+' : '-')}{Money(entry.Amount)}";
                var line = $"{At(entry.Time).ToString(LineDateFormat, CultureInfo.InvariantCulture)}  {amount,9}  {Describe(entry)}";
                if (entry.Reference is { } reference)
                    line += $"  {reference}";
                sb.AppendLine(Escape(line));
            }
            sb.AppendLine("[/mono]");
        }

        sb.AppendLine();
        sb.Append($"[italic]{Loc.GetString("cmu-bank-statement-footer")}[/italic]");
        return sb.ToString();
    }

    public string Certificate(string reference, int amount, string fromName, int fromAccount, string toName, int toAccount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[head=2]{Loc.GetString("cmu-bank-certificate-title")}[/head]");
        sb.AppendLine(Loc.GetString("cmu-bank-certificate-body", ("amount", Money(amount))));
        sb.AppendLine();
        Field(sb, "cmu-bank-field-from", $"{Escape(fromName)} (#{fromAccount})");
        Field(sb, "cmu-bank-field-to", $"{Escape(toName)} (#{toAccount})");
        Field(sb, "cmu-bank-field-amount", Money(amount));
        Field(sb, "cmu-bank-field-date", Now());
        Field(sb, "cmu-bank-field-reference", reference);
        sb.AppendLine();
        sb.Append($"[italic]{Loc.GetString("cmu-bank-certificate-footer")}[/italic]");
        return sb.ToString();
    }

    public string Receipt(CardReceipt receipt, bool merchantCopy)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[head=2]{Loc.GetString("cmu-bank-receipt-title")}[/head]");
        if (merchantCopy)
            sb.AppendLine($"[bold]{Loc.GetString("cmu-bank-receipt-merchant-copy")}[/bold]");

        Field(sb, "cmu-bank-field-merchant", $"{Escape(receipt.MerchantName)} (#{receipt.MerchantAccount})");
        Field(sb, "cmu-bank-field-date", Now(receipt.Time));
        sb.AppendLine();

        sb.AppendLine("[mono]");
        sb.AppendLine(Row(Loc.GetString("cmu-bank-receipt-sale"), Money(receipt.Amount)));
        if (receipt.Tip > 0)
            sb.AppendLine(Row(Loc.GetString("cmu-bank-receipt-tip", ("percent", receipt.TipPercent)), Money(receipt.Tip)));
        sb.AppendLine(Row(Loc.GetString("cmu-bank-receipt-total"), Money(receipt.Amount + receipt.Tip)));
        sb.AppendLine("[/mono]");
        sb.AppendLine();

        Field(sb, "cmu-bank-field-card", Loc.GetString("cmu-bank-receipt-card", ("digits", MaskAccount(receipt.CustomerAccount))));
        Field(sb, "cmu-bank-field-reference", receipt.Reference);
        sb.Append($"[bold]{Loc.GetString("cmu-bank-receipt-approved")}[/bold]");
        return sb.ToString();
    }

    // ─── Formatting ────────────────────────────────────────────────────────

    public static bool IsCredit(AtmHistoryKind kind)
        => kind is AtmHistoryKind.Deposit or AtmHistoryKind.CashDeposit or AtmHistoryKind.TransferIn
            or AtmHistoryKind.Retracted or AtmHistoryKind.CardSale;

    private string Describe(ColonyAccountHistoryEntry entry)
    {
        var other = entry.OtherAccount.ToString(CultureInfo.InvariantCulture);
        return entry.Kind switch
        {
            AtmHistoryKind.Withdrawal => Loc.GetString("cmu-bank-line-withdrawal"),
            AtmHistoryKind.Deposit => Loc.GetString("cmu-bank-line-deposit"),
            AtmHistoryKind.CashDeposit => Loc.GetString("cmu-bank-line-cash-deposit"),
            AtmHistoryKind.TransferOut => Loc.GetString("cmu-bank-line-transfer-out", ("account", other)),
            AtmHistoryKind.TransferIn => Loc.GetString("cmu-bank-line-transfer-in", ("account", other)),
            AtmHistoryKind.Retracted => Loc.GetString("cmu-bank-line-retracted"),
            AtmHistoryKind.Purchase => Loc.GetString("cmu-bank-line-purchase"),
            AtmHistoryKind.CardPayment => Loc.GetString("cmu-bank-line-card-payment", ("account", other)),
            AtmHistoryKind.CardSale => Loc.GetString("cmu-bank-line-card-sale", ("account", other)),
            _ => string.Empty,
        };
    }

    private void Field(StringBuilder sb, string label, string value)
        => sb.AppendLine($"[bold]{Loc.GetString(label)}[/bold] {value}");

    private static string Row(string label, string money)
        => Escape($"{label,-16}{money,10}");

    private static string Money(int amount) => $"${amount.ToString("N0", CultureInfo.InvariantCulture)}";

    private static string MaskAccount(int account)
    {
        var digits = account.ToString(CultureInfo.InvariantCulture);
        var shown = Math.Max(0, digits.Length - 2);
        return new string('*', shown) + digits[shown..];
    }

    private static string Escape(string text) => FormattedMessage.EscapeText(text);

    // Colony clock time at a given round time, as wall clocks show it.
    private DateTime At(TimeSpan roundTime)
    {
        var clock = EntityQuery<GlobalTimeManagerComponent>().FirstOrDefault();
        var date = clock?.DateOffset ?? DateTime.Today.AddYears(100);
        return date + (clock?.TimeOffset ?? TimeSpan.Zero) + roundTime;
    }

    private string Now(TimeSpan? roundTime = null)
        => At(roundTime ?? _ticker.RoundDuration()).ToString(DateFormat, CultureInfo.InvariantCulture);
}
