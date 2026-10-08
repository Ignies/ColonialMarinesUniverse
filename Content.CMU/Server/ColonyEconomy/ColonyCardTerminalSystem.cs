using Content.Server.GameTicking;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed class ColonyCardTerminalSystem : EntitySystem
{
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private ColonyBankPaperworkSystem _paperwork = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int PinLength = ColonyAtmComponent.PinLength;
    private static readonly TimeSpan PrintTime = TimeSpan.FromSeconds(1.25);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyCardTerminalComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ColonyCardTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ColonyCardTerminalComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<ColonyCardTerminalComponent, ColonyCardTerminalKeyMsg>(OnKey);
        SubscribeLocalEvent<ColonyCardTerminalComponent, ColonyCardTerminalTapMsg>(OnTap);
        SubscribeLocalEvent<ColonyCardTerminalComponent, ColonyCardTerminalTakeReceiptMsg>(OnTakeReceipt);
        SubscribeLocalEvent<ColonyCardTerminalComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ColonyCardTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (RequestOpen(comp) && comp.Customer is { } customer &&
                (now > comp.RequestExpires || TerminatingOrDeleted(customer) || !InReach((uid, comp), customer)))
            {
                CancelRequest((uid, comp), now > comp.RequestExpires ? "cmu-terminal-timed-out" : "cmu-terminal-cancelled");
            }

            if (IsSetup(comp.Screen) && now > comp.SetupExpires)
            {
                LeaveSetup(comp);
                Refresh((uid, comp));
            }

            if (comp.ReceiptPrintedAt is { } printed && now > printed + PrintTime &&
                _appearance.TryGetData(uid, CardTerminalVisuals.Receipt, out CardTerminalReceiptVisual visual) &&
                visual == CardTerminalReceiptVisual.Printing)
            {
                SetReceiptVisual(uid);
            }
        }
    }

    // ─── Presenting to a customer ──────────────────────────────────────────

    private void OnAfterInteract(Entity<ColonyCardTerminalComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User ||
            !HasComp<ActorComponent>(target) || _mobState.IsIncapacitated(target))
        {
            return;
        }

        args.Handled = true;
        var comp = ent.Comp;
        if (comp.Screen != CardTerminalScreen.Armed)
        {
            var reason = comp.Screen == CardTerminalScreen.Waiting ? "cmu-terminal-popup-busy" : "cmu-terminal-popup-no-sale";
            _popup.PopupEntity(Loc.GetString(reason), ent, args.User);
            return;
        }

        comp.SaleId++;
        comp.Customer = target;
        comp.Step = comp.TipsEnabled ? CardTerminalStep.Tip : CardTerminalStep.Tap;
        comp.TipPercent = 0;
        comp.TappedCard = null;
        comp.Buffer = string.Empty;
        comp.Status = string.Empty;
        comp.RequestExpires = _timing.CurTime + comp.CustomerTimeout;
        comp.Screen = CardTerminalScreen.Waiting;

        _ui.OpenUi(ent.Owner, ColonyCardTerminalUi.Customer, target);
        _popup.PopupEntity(Loc.GetString("cmu-terminal-popup-present", ("merchant", args.User), ("amount", Money(comp.Amount))), target, target);
        _popup.PopupEntity(Loc.GetString("cmu-terminal-popup-present-self", ("customer", target)), args.User, args.User);
        Refresh(ent);
    }

    private void OnUiOpened(Entity<ColonyCardTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        // The customer window is the server's to open, and only for the customer.
        if (Equals(args.UiKey, ColonyCardTerminalUi.Customer) && args.Actor != ent.Comp.Customer)
        {
            _ui.CloseUi(ent.Owner, ColonyCardTerminalUi.Customer, args.Actor);
            return;
        }

        Refresh(ent);
    }

    private void OnUiClosed(Entity<ColonyCardTerminalComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!Equals(args.UiKey, ColonyCardTerminalUi.Customer) || args.Actor != ent.Comp.Customer)
            return;

        if (RequestOpen(ent.Comp))
            CancelRequest(ent, "cmu-terminal-cancelled");
        else
            ent.Comp.Customer = null;
    }

    // ─── Keys ──────────────────────────────────────────────────────────────

    private void OnKey(Entity<ColonyCardTerminalComponent> ent, ref ColonyCardTerminalKeyMsg msg)
    {
        if (Equals(msg.UiKey, ColonyCardTerminalUi.Customer))
            CustomerKey(ent, msg.Actor, msg.Key, msg.SaleId);
        else
            MerchantKey(ent, msg.Actor, msg.Key);

        Refresh(ent);
    }

    private void MerchantKey(Entity<ColonyCardTerminalComponent> ent, EntityUid actor, CardTerminalKey key)
    {
        var comp = ent.Comp;
        if (comp.Screen is CardTerminalScreen.SetupPin or CardTerminalScreen.Setup or CardTerminalScreen.SetupPayout or CardTerminalScreen.SetupTip)
        {
            if (actor != comp.SetupUser)
                return;

            comp.SetupExpires = _timing.CurTime + comp.SetupTimeout;
        }

        switch (comp.Screen)
        {
            case CardTerminalScreen.Register or CardTerminalScreen.SetupAuth when key == CardTerminalKey.Cancel:
                LeaveSetup(comp);
                break;

            case CardTerminalScreen.RegisterPin or CardTerminalScreen.SetupPin:
                if (key == CardTerminalKey.Enter)
                    ConfirmMerchantPin(comp);
                else if (key == CardTerminalKey.Cancel)
                    LeaveSetup(comp);
                else
                    Type(comp, key, PinLength);
                break;

            case CardTerminalScreen.Ready:
                if (key == CardTerminalKey.Menu)
                {
                    comp.Screen = CardTerminalScreen.SetupAuth;
                    comp.Status = string.Empty;
                    comp.SetupExpires = _timing.CurTime + comp.SetupTimeout;
                }
                else if (key == CardTerminalKey.Enter && int.TryParse(comp.Buffer, out var amount) && amount > 0)
                {
                    comp.Amount = amount;
                    comp.Buffer = string.Empty;
                    comp.Status = string.Empty;
                    comp.Screen = CardTerminalScreen.Armed;
                }
                else
                {
                    Type(comp, key, ColonyCardTerminalComponent.MaxAmountDigits);
                }
                break;

            case CardTerminalScreen.Armed when key == CardTerminalKey.Cancel:
                comp.Screen = CardTerminalScreen.Ready;
                break;

            case CardTerminalScreen.Waiting when key == CardTerminalKey.Cancel:
                CancelRequest(ent, "cmu-terminal-cancelled");
                break;

            case CardTerminalScreen.Approved when key == CardTerminalKey.D1 && comp.LastReceipt is { } receipt:
                PrintReceipt(ent, receipt, merchantCopy: true);
                break;

            case CardTerminalScreen.Approved or CardTerminalScreen.Declined
                when key is CardTerminalKey.Enter or CardTerminalKey.Cancel:
                comp.Screen = CardTerminalScreen.Ready;
                comp.Status = string.Empty;
                break;

            case CardTerminalScreen.Setup:
                SetupMenu(comp, key);
                break;

            case CardTerminalScreen.SetupPayout:
                if (key == CardTerminalKey.Enter)
                    SetPayout(comp);
                else if (key == CardTerminalKey.Cancel)
                    comp.Screen = CardTerminalScreen.Setup;
                else
                    Type(comp, key, 5);
                break;

            case CardTerminalScreen.SetupTip:
                if (key == CardTerminalKey.Enter)
                    SetTipPreset(comp);
                else if (key == CardTerminalKey.Cancel)
                    comp.Screen = CardTerminalScreen.Setup;
                else
                    Type(comp, key, 3);
                break;
        }
    }

    private void CustomerKey(Entity<ColonyCardTerminalComponent> ent, EntityUid actor, CardTerminalKey key, int saleId)
    {
        var comp = ent.Comp;
        if (!IsCustomer(ent, actor) || saleId != comp.SaleId)
            return;

        switch (comp.Step)
        {
            case CardTerminalStep.Tip when key == CardTerminalKey.Cancel:
            case CardTerminalStep.Tap when key == CardTerminalKey.Cancel:
            case CardTerminalStep.Pin when key == CardTerminalKey.Cancel:
                CancelRequest(ent, "cmu-terminal-cancelled");
                break;

            case CardTerminalStep.Tip when key is >= CardTerminalKey.D0 and <= CardTerminalKey.D3:
                var choice = (int) key;
                comp.TipPercent = choice == 0 ? 0 : comp.TipPercents[choice - 1];
                comp.Step = CardTerminalStep.Tap;
                break;

            case CardTerminalStep.Pin when key == CardTerminalKey.Enter:
                Charge(ent);
                break;

            case CardTerminalStep.Pin:
                Type(comp, key, PinLength);
                break;

            case CardTerminalStep.Approved or CardTerminalStep.Declined when key is CardTerminalKey.Enter or CardTerminalKey.Cancel:
                _ui.CloseUi(ent.Owner, ColonyCardTerminalUi.Customer, actor);
                break;
        }
    }

    private static void Type(ColonyCardTerminalComponent comp, CardTerminalKey key, int maxLength)
    {
        if (key == CardTerminalKey.Clear)
        {
            if (comp.Buffer.Length > 0)
                comp.Buffer = comp.Buffer[..^1];
            return;
        }

        var digits = key switch
        {
            <= CardTerminalKey.D9 => ((int) key).ToString(),
            CardTerminalKey.DoubleZero => "00",
            _ => string.Empty,
        };

        if (digits.Length > 0 && comp.Buffer.Length + digits.Length <= maxLength)
            comp.Buffer += digits;
    }

    // ─── Tapping a card ────────────────────────────────────────────────────

    private void OnTap(Entity<ColonyCardTerminalComponent> ent, ref ColonyCardTerminalTapMsg msg)
    {
        var comp = ent.Comp;
        var actor = msg.Actor;
        var customer = Equals(msg.UiKey, ColonyCardTerminalUi.Customer);

        if (customer ? !IsCustomer(ent, actor) || msg.SaleId != comp.SaleId || comp.Step != CardTerminalStep.Tap
                     : comp.Screen is not (CardTerminalScreen.Register or CardTerminalScreen.SetupAuth))
        {
            return;
        }

        if (!TryFindCard(actor, out var card))
        {
            comp.Status = Loc.GetString("cmu-terminal-no-card");
            Refresh(ent);
            return;
        }

        comp.TappedAt = _timing.CurTime;
        comp.Buffer = string.Empty;
        comp.Status = string.Empty;
        _audio.PlayPvs(comp.TapSound, ent);

        if (customer)
        {
            comp.TappedCard = card;
            comp.Step = CardTerminalStep.Pin;
        }
        else if (comp.Screen == CardTerminalScreen.SetupAuth && card.Comp.AccountNumber != comp.OwnerAccount)
        {
            comp.Status = Loc.GetString("cmu-terminal-not-owner");
        }
        else
        {
            comp.PendingCard = card;
            comp.SetupUser = actor;
            comp.Screen = comp.Screen == CardTerminalScreen.Register ? CardTerminalScreen.RegisterPin : CardTerminalScreen.SetupPin;
            comp.SetupExpires = _timing.CurTime + comp.SetupTimeout;
        }

        Refresh(ent);
    }

    private bool TryFindCard(EntityUid user, out Entity<IdCardComponent> card)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (_idCard.TryGetIdCard(held, out card))
                return true;
        }

        return _idCard.TryFindIdCard(user, out card);
    }

    // ─── Merchant setup ────────────────────────────────────────────────────

    private void ConfirmMerchantPin(ColonyCardTerminalComponent comp)
    {
        if (comp.PendingCard is not { } cardUid || !TryComp<IdCardComponent>(cardUid, out var card))
        {
            LeaveSetup(comp);
            return;
        }

        if (!CheckPin(comp, cardUid, card))
            return;

        if (comp.Screen == CardTerminalScreen.RegisterPin)
        {
            comp.OwnerAccount = comp.PayoutAccount = card.AccountNumber;
            comp.Screen = CardTerminalScreen.Ready;
            comp.Status = Loc.GetString("cmu-terminal-registered", ("name", card.FullName ?? "Unknown"));
            comp.SetupUser = null;
        }
        else
        {
            comp.Screen = CardTerminalScreen.Setup;
            comp.Status = string.Empty;
        }

        comp.PendingCard = null;
    }

    private void SetupMenu(ColonyCardTerminalComponent comp, CardTerminalKey key)
    {
        comp.Status = string.Empty;
        switch (key)
        {
            case CardTerminalKey.D1:
                comp.Buffer = string.Empty;
                comp.Screen = CardTerminalScreen.SetupPayout;
                break;
            case CardTerminalKey.D2:
                comp.TipsEnabled = !comp.TipsEnabled;
                break;
            case CardTerminalKey.D3:
                comp.Buffer = string.Empty;
                comp.TipPresetEditing = 0;
                comp.Screen = CardTerminalScreen.SetupTip;
                break;
            case CardTerminalKey.D4:
                comp.OwnerAccount = comp.PayoutAccount = 0;
                LeaveSetup(comp);
                break;
            case CardTerminalKey.Cancel or CardTerminalKey.Enter:
                LeaveSetup(comp);
                break;
        }
    }

    private void SetPayout(ColonyCardTerminalComponent comp)
    {
        if (!int.TryParse(comp.Buffer, out var account) || _bank.FindAccount(account) == null)
        {
            comp.Status = Loc.GetString("cmu-terminal-no-account");
            comp.Buffer = string.Empty;
            return;
        }

        comp.PayoutAccount = account;
        comp.Buffer = string.Empty;
        comp.Screen = CardTerminalScreen.Setup;
    }

    private void SetTipPreset(ColonyCardTerminalComponent comp)
    {
        if (!int.TryParse(comp.Buffer, out var percent) || percent > 100)
        {
            comp.Buffer = string.Empty;
            return;
        }

        comp.TipPercents[comp.TipPresetEditing] = percent;
        comp.Buffer = string.Empty;
        if (++comp.TipPresetEditing >= comp.TipPercents.Length)
            comp.Screen = CardTerminalScreen.Setup;
    }

    private static void LeaveSetup(ColonyCardTerminalComponent comp)
    {
        comp.Screen = comp.OwnerAccount == 0 ? CardTerminalScreen.Register : CardTerminalScreen.Ready;
        comp.SetupUser = null;
        comp.PendingCard = null;
        comp.Buffer = string.Empty;
    }

    private static bool IsSetup(CardTerminalScreen screen)
        => screen is CardTerminalScreen.RegisterPin or CardTerminalScreen.SetupAuth or CardTerminalScreen.SetupPin
            or CardTerminalScreen.Setup or CardTerminalScreen.SetupPayout or CardTerminalScreen.SetupTip;

    // ─── Charging ──────────────────────────────────────────────────────────

    private bool CheckPin(ColonyCardTerminalComponent comp, EntityUid cardUid, IdCardComponent card)
    {
        var entered = comp.Buffer;
        comp.Buffer = string.Empty;
        if (_bank.IsLocked(card, out _))
        {
            comp.Status = Loc.GetString("cmu-terminal-declined-locked");
            return false;
        }

        if (entered.Length == PinLength && int.TryParse(entered, out var pin) &&
            _bank.TryAuthenticatePin(cardUid, card, pin, out _))
        {
            return true;
        }

        comp.Status = Loc.GetString(_bank.IsLocked(card, out _) ? "cmu-terminal-declined-locked" : "cmu-terminal-wrong-pin");
        return false;
    }

    private void Charge(Entity<ColonyCardTerminalComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.TappedCard is not { } cardUid || !TryComp<IdCardComponent>(cardUid, out var card))
        {
            Decline(ent, "cmu-terminal-declined-card");
            return;
        }

        if (!CheckPin(comp, cardUid, card))
        {
            if (_bank.IsLocked(card, out _))
                Decline(ent, "cmu-terminal-declined-locked");
            else
                Buzz(ent);
            return;
        }

        if (_bank.FindAccount(comp.PayoutAccount) is not { } payout)
        {
            Decline(ent, "cmu-terminal-declined-payout");
            return;
        }

        if (payout.uid == cardUid)
        {
            Decline(ent, "cmu-terminal-declined-self");
            return;
        }

        var tip = (int) Math.Min(int.MaxValue, (long) comp.Amount * comp.TipPercent / 100);
        var total = comp.Amount + tip;
        if (card.AccountBalance < total)
        {
            Decline(ent, "cmu-terminal-declined-funds");
            return;
        }

        card.AccountBalance -= total;
        Dirty(cardUid, card);
        payout.card.AccountBalance += total;
        Dirty(payout.uid, payout.card);

        var reference = _bank.NewReference("PAY");
        _bank.RecordTransaction(cardUid, AtmHistoryKind.CardPayment, total, payout.card.AccountNumber, reference);
        _bank.RecordTransaction(payout.uid, AtmHistoryKind.CardSale, total, card.AccountNumber, reference);

        comp.LastReceipt = new CardReceipt(reference, _ticker.RoundDuration(), payout.card.FullName ?? "Unknown",
            payout.card.AccountNumber, card.AccountNumber, comp.Amount, tip, comp.TipPercent);
        PrintReceipt(ent, comp.LastReceipt, merchantCopy: false);

        comp.Step = CardTerminalStep.Approved;
        comp.Screen = CardTerminalScreen.Approved;
        comp.Status = Loc.GetString("cmu-terminal-approved-from", ("amount", Money(total)), ("name", card.FullName ?? "Unknown"));
        comp.ApprovedAt = _timing.CurTime;
        _audio.PlayPvs(comp.ApproveSound, ent);
    }

    private void Decline(Entity<ColonyCardTerminalComponent> ent, string reason)
    {
        var comp = ent.Comp;
        comp.Step = CardTerminalStep.Declined;
        comp.Screen = CardTerminalScreen.Declined;
        comp.Status = Loc.GetString(reason);
        Buzz(ent);
    }

    private void Buzz(Entity<ColonyCardTerminalComponent> ent)
    {
        ent.Comp.DeclinedAt = _timing.CurTime;
        _audio.PlayPvs(ent.Comp.DeclineSound, ent);
    }

    private void CancelRequest(Entity<ColonyCardTerminalComponent> ent, string reason)
    {
        var comp = ent.Comp;
        var customer = comp.Customer;
        comp.Customer = null;
        comp.Step = CardTerminalStep.None;
        comp.Screen = CardTerminalScreen.Declined;
        comp.Status = Loc.GetString(reason);
        Buzz(ent);

        if (customer != null)
            _ui.CloseUi(ent.Owner, ColonyCardTerminalUi.Customer, customer);

        Refresh(ent);
    }

    private static bool RequestOpen(ColonyCardTerminalComponent comp)
        => comp.Customer != null && comp.Step is CardTerminalStep.Tip or CardTerminalStep.Tap or CardTerminalStep.Pin;

    private bool IsCustomer(Entity<ColonyCardTerminalComponent> ent, EntityUid actor)
        => actor == ent.Comp.Customer && _blocker.CanInteract(actor, ent) && InReach(ent, actor);

    private bool InReach(Entity<ColonyCardTerminalComponent> ent, EntityUid customer)
    {
        var a = _transform.GetMapCoordinates(customer);
        var b = _transform.GetMapCoordinates(ent);
        return a.MapId == b.MapId && (a.Position - b.Position).Length() <= ent.Comp.CustomerRange;
    }

    // ─── Receipts ──────────────────────────────────────────────────────────

    private void PrintReceipt(Entity<ColonyCardTerminalComponent> ent, CardReceipt receipt, bool merchantCopy)
    {
        // A receipt nobody took is pushed out by the next.
        _paperwork.Eject(ent, ColonyCardTerminalComponent.ReceiptSlotId);
        var total = Money(receipt.Amount + receipt.Tip);
        var name = Loc.GetString(merchantCopy ? "cmu-bank-receipt-copy-name" : "cmu-bank-receipt-name", ("total", total));
        if (_paperwork.TryPrint(ent, ColonyCardTerminalComponent.ReceiptSlotId, name, _paperwork.Receipt(receipt, merchantCopy)))
        {
            ent.Comp.ReceiptPrintedAt = _timing.CurTime;
            _appearance.SetData(ent, CardTerminalVisuals.Receipt, CardTerminalReceiptVisual.Printing);
        }
    }

    private void SetReceiptVisual(EntityUid uid)
    {
        var waiting = _paperwork.GetWaitingPaper(uid, ColonyCardTerminalComponent.ReceiptSlotId) != null;
        _appearance.SetData(uid, CardTerminalVisuals.Receipt,
            waiting ? CardTerminalReceiptVisual.Waiting : CardTerminalReceiptVisual.None);
    }

    private void OnTakeReceipt(Entity<ColonyCardTerminalComponent> ent, ref ColonyCardTerminalTakeReceiptMsg msg)
    {
        var customer = Equals(msg.UiKey, ColonyCardTerminalUi.Customer);
        if (customer && msg.Actor != ent.Comp.Customer)
            return;

        if (!_paperwork.TryTake(ent, ColonyCardTerminalComponent.ReceiptSlotId, msg.Actor))
            return;

        SetReceiptVisual(ent);

        if (customer && !RequestOpen(ent.Comp))
            _ui.CloseUi(ent.Owner, ColonyCardTerminalUi.Customer, msg.Actor);

        Refresh(ent);
    }

    private void OnGetVerbs(Entity<ColonyCardTerminalComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null ||
            _paperwork.GetWaitingPaper(ent, ColonyCardTerminalComponent.ReceiptSlotId) == null)
        {
            return;
        }

        var user = args.User;
        var terminal = ent;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bank-take-receipt-verb"),
            Act = () =>
            {
                _paperwork.TryTake(terminal, ColonyCardTerminalComponent.ReceiptSlotId, user);
                SetReceiptVisual(terminal);
                Refresh(terminal);
            },
        });
    }

    // ─── Screens ───────────────────────────────────────────────────────────

    private void Refresh(Entity<ColonyCardTerminalComponent> ent)
    {
        var comp = ent.Comp;
        var receiptWaiting = _paperwork.GetWaitingPaper(ent, ColonyCardTerminalComponent.ReceiptSlotId) != null;
        var merchant = _bank.FindAccount(comp.PayoutAccount)?.card.FullName;
        var header = merchant ?? Loc.GetString("cmu-terminal-header");

        var (body, input, keys, tap) = MerchantScreen(comp);
        _ui.SetUiState(ent.Owner, ColonyCardTerminalUi.Merchant, new ColonyCardTerminalBuiState(
            header, body, input, keys, tap, comp.TappedAt, comp.ApprovedAt, comp.DeclinedAt,
            receiptWaiting, comp.ReceiptPrintedAt, comp.SaleId));

        if (comp.Customer == null)
            return;

        (body, input, keys, tap) = CustomerScreen(comp, merchant ?? header);
        _ui.SetUiState(ent.Owner, ColonyCardTerminalUi.Customer, new ColonyCardTerminalBuiState(
            header, body, input, keys, tap, comp.TappedAt, comp.ApprovedAt, comp.DeclinedAt,
            receiptWaiting, comp.ReceiptPrintedAt, comp.SaleId));
    }

    private (string Body, string? Input, CardTerminalKeys Keys, bool Tap) MerchantScreen(ColonyCardTerminalComponent comp)
    {
        const CardTerminalKeys typing = CardTerminalKeys.Digits | CardTerminalKeys.Clear | CardTerminalKeys.Cancel | CardTerminalKeys.Enter;
        var pin = new string('*', comp.Buffer.Length);
        var lines = new List<string>();
        string? input = null;
        var keys = CardTerminalKeys.None;
        var tap = false;

        switch (comp.Screen)
        {
            case CardTerminalScreen.Register:
                lines.Add(L("cmu-terminal-not-set-up"));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-tap-to-register"));
                tap = true;
                break;
            case CardTerminalScreen.RegisterPin or CardTerminalScreen.SetupPin:
                lines.Add(L("cmu-terminal-enter-pin"));
                input = pin;
                keys = typing;
                break;
            case CardTerminalScreen.Ready:
                lines.Add(L("cmu-terminal-ready"));
                lines.Add(L("cmu-terminal-payout", ("account", comp.PayoutAccount.ToString())));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-amount"));
                input = comp.Buffer.Length > 0 ? $"${comp.Buffer}" : "$";
                keys = typing | CardTerminalKeys.Menu;
                break;
            case CardTerminalScreen.Armed:
                lines.Add(L("cmu-terminal-charge", ("amount", Money(comp.Amount))));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-use-on-customer"));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-hint-cancel"));
                keys = CardTerminalKeys.Cancel;
                break;
            case CardTerminalScreen.Waiting:
                lines.Add(L("cmu-terminal-waiting"));
                lines.Add(comp.Customer is { } customer ? Name(customer) : string.Empty);
                lines.Add(Money(comp.Amount));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-hint-cancel"));
                keys = CardTerminalKeys.Cancel;
                break;
            case CardTerminalScreen.Approved:
                lines.Add(L("cmu-terminal-approved"));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-hint-new-sale"));
                lines.Add(L("cmu-terminal-hint-copy"));
                keys = CardTerminalKeys.Enter | CardTerminalKeys.EnterReady | CardTerminalKeys.Cancel | CardTerminalKeys.Digits;
                break;
            case CardTerminalScreen.Declined:
                lines.Add(L("cmu-terminal-declined"));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-hint-new-sale"));
                keys = CardTerminalKeys.Enter | CardTerminalKeys.EnterReady | CardTerminalKeys.Cancel;
                break;
            case CardTerminalScreen.SetupAuth:
                lines.Add(L("cmu-terminal-setup"));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-tap-owner"));
                keys = CardTerminalKeys.Cancel;
                tap = true;
                break;
            case CardTerminalScreen.Setup:
                lines.Add(L("cmu-terminal-setup"));
                lines.Add(L("cmu-terminal-setup-payout", ("account", comp.PayoutAccount.ToString())));
                lines.Add(L("cmu-terminal-setup-tips", ("state", L(comp.TipsEnabled ? "cmu-terminal-on" : "cmu-terminal-off"))));
                lines.Add(L("cmu-terminal-setup-presets", ("presets", string.Join('/', comp.TipPercents))));
                lines.Add(L("cmu-terminal-setup-unregister"));
                lines.Add(L("cmu-terminal-setup-done"));
                keys = CardTerminalKeys.Digits | CardTerminalKeys.Cancel;
                break;
            case CardTerminalScreen.SetupPayout:
                lines.Add(L("cmu-terminal-payout-title"));
                lines.Add(L("cmu-terminal-account"));
                input = comp.Buffer;
                keys = typing;
                break;
            case CardTerminalScreen.SetupTip:
                lines.Add(L("cmu-terminal-tip-title", ("index", comp.TipPresetEditing + 1)));
                lines.Add(L("cmu-terminal-percent"));
                input = comp.Buffer;
                keys = typing;
                break;
        }

        if (comp.Status.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add(comp.Status);
        }

        return (string.Join('\n', lines), input, keys, tap);
    }

    private (string Body, string? Input, CardTerminalKeys Keys, bool Tap) CustomerScreen(ColonyCardTerminalComponent comp, string merchant)
    {
        var lines = new List<string> { L("cmu-terminal-pay-to", ("name", merchant)) };
        string? input = null;
        var keys = CardTerminalKeys.Cancel;
        var tap = false;
        var tip = (int) Math.Min(int.MaxValue, (long) comp.Amount * comp.TipPercent / 100);

        switch (comp.Step)
        {
            case CardTerminalStep.Tip:
                lines.Add(L("cmu-terminal-amount-line", ("amount", Money(comp.Amount))));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-add-tip"));
                for (var i = 0; i < comp.TipPercents.Length; i++)
                {
                    var percent = comp.TipPercents[i];
                    var amount = (int) Math.Min(int.MaxValue, (long) comp.Amount * percent / 100);
                    lines.Add(L("cmu-terminal-tip-option", ("key", i + 1), ("percent", percent), ("amount", Money(amount))));
                }
                lines.Add(L("cmu-terminal-no-tip"));
                keys |= CardTerminalKeys.Digits;
                break;
            case CardTerminalStep.Tap:
                lines.Add(L("cmu-terminal-total", ("amount", Money(comp.Amount + tip))));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-tap-card"));
                tap = true;
                break;
            case CardTerminalStep.Pin:
                lines.Add(L("cmu-terminal-total", ("amount", Money(comp.Amount + tip))));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-enter-pin"));
                input = new string('*', comp.Buffer.Length);
                keys |= CardTerminalKeys.Digits | CardTerminalKeys.Clear | CardTerminalKeys.Enter;
                break;
            case CardTerminalStep.Approved:
                lines.Add(L("cmu-terminal-approved"));
                lines.Add(L("cmu-terminal-thanks"));
                lines.Add(string.Empty);
                lines.Add(L("cmu-terminal-take-receipt"));
                keys = CardTerminalKeys.Enter;
                break;
            case CardTerminalStep.Declined:
                lines.Add(L("cmu-terminal-declined"));
                keys = CardTerminalKeys.Enter;
                break;
        }

        if (comp.Status.Length > 0 && comp.Step != CardTerminalStep.Approved)
        {
            lines.Add(string.Empty);
            lines.Add(comp.Status);
        }

        return (string.Join('\n', lines), input, keys, tap);
    }

    private string L(string id, params (string, object)[] args) => Loc.GetString(id, args);

    private string Name(EntityUid uid) => MetaData(uid).EntityName;

    private static string Money(int amount) => $"${amount}";
}
