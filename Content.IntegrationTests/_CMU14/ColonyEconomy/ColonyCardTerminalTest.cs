using System.Linq;
using Content.Client.CMU14.ColonyEconomy;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>The player is the customer; a merchant beside them holds a registered terminal.</summary>
public sealed class ColonyCardTerminalTest : ColonyAtmTestBase
{
    private const string Terminal = "AUColonyCardTerminal";

    private EntityUid _merchant;
    private EntityUid _terminal;
    private IdCardComponent _merchantCard = default!;

    private ColonyCardTerminalComponent TerminalComp => SEntMan.GetComponent<ColonyCardTerminalComponent>(_terminal);

    private async Task<(IdCardComponent Card, int Pin)> SetUpSale(int customerBalance, int amount, bool tips = false)
    {
        var card = await PlaceInHands(IdCard);
        var customer = Comp<IdCardComponent>(card);
        await Server.WaitPost(() => customer.AccountBalance = customerBalance);

        _merchant = await SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(TargetCoords));
        var merchantCard = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        _terminal = await SpawnEntity(Terminal, SEntMan.GetCoordinates(TargetCoords));
        _merchantCard = SEntMan.GetComponent<IdCardComponent>(merchantCard);

        await Server.WaitPost(() =>
        {
            HandSys.TryPickupAnyHand(_merchant, _terminal);
            var comp = TerminalComp;
            comp.OwnerAccount = comp.PayoutAccount = _merchantCard.AccountNumber;
            comp.TipsEnabled = tips;
            comp.Screen = CardTerminalScreen.Ready;
        });

        foreach (var digit in amount.ToString())
            await MerchantKey((CardTerminalKey) (digit - '0'));
        await MerchantKey(CardTerminalKey.Enter);
        Assert.That(TerminalComp.Screen, Is.EqualTo(CardTerminalScreen.Armed), "The sale was not armed");

        return (customer, customer.AtmPin);
    }

    private async Task MerchantKey(CardTerminalKey key)
    {
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(_terminal,
            new ColonyCardTerminalKeyMsg(key, 0) { Actor = _merchant, UiKey = ColonyCardTerminalUi.Merchant }));
        await RunTicks(2);
    }

    private async Task PresentToPlayer()
    {
        await Server.WaitPost(() => InteractSys.InteractUsing(_merchant, _terminal, SPlayer,
            SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates));
        await RunTicks(10);
    }

    private async Task Press(string control)
    {
        await ClickControl<ColonyCardTerminalWindow>(control);
        await RunTicks(5);
    }

    private async Task TapAndPin(int pin)
    {
        await Press(nameof(ColonyCardTerminalWindow.BtnTap));
        foreach (var digit in pin.ToString())
            await Press($"Btn{digit}");
        await Press(nameof(ColonyCardTerminalWindow.BtnEnter));
    }

    private CardTerminalReceiptVisual ReceiptVisual()
    {
        SEntMan.System<SharedAppearanceSystem>().TryGetData(_terminal, CardTerminalVisuals.Receipt, out CardTerminalReceiptVisual visual);
        return visual;
    }

    private EntityUid? Receipt()
        => SEntMan.System<ColonyBankPaperworkSystem>().GetWaitingPaper(_terminal, ColonyCardTerminalComponent.ReceiptSlotId);

    [Test]
    public async Task CustomerPaysWithCardAndPin()
    {
        var (customer, pin) = await SetUpSale(200, 40);
        await PresentToPlayer();
        Assert.That(IsUiOpen(ColonyCardTerminalUi.Customer), "No payment window opened for the customer");

        await TapAndPin(pin);

        var paper = Receipt();
        Assert.Multiple(() =>
        {
            Assert.That(customer.AccountBalance, Is.EqualTo(160));
            Assert.That(_merchantCard.AccountBalance, Is.EqualTo(40));
            Assert.That(TerminalComp.Screen, Is.EqualTo(CardTerminalScreen.Approved));
            Assert.That(paper, Is.Not.Null, "No receipt was printed");
        });

        var reference = TerminalComp.LastReceipt!.Reference;
        var bank = SEntMan.System<ColonyBankSystem>();
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<PaperComponent>(paper!.Value).Content, Does.Contain(reference).And.Contain("$40"));
            Assert.That(bank.GetHistory(_merchantCard.Owner).Last().Reference, Is.EqualTo(reference));
        });

        Assert.That(ReceiptVisual(), Is.EqualTo(CardTerminalReceiptVisual.Printing), "The receipt isn't printing out of the terminal");
        await RunSeconds(2);
        Assert.That(ReceiptVisual(), Is.EqualTo(CardTerminalReceiptVisual.Waiting));

        await Drop();                                // the test mob has one hand; free it for the receipt
        await Press(nameof(ColonyCardTerminalWindow.BtnReceipt));
        Assert.Multiple(() =>
        {
            Assert.That(HeldItem(), Is.EqualTo(paper), "The receipt didn't go into the customer's hand");
            Assert.That(IsUiOpen(ColonyCardTerminalUi.Customer), Is.False, "The payment window stayed open");
            Assert.That(ReceiptVisual(), Is.EqualTo(CardTerminalReceiptVisual.None), "The taken receipt still shows on the terminal");
        });
    }

    [Test]
    public async Task CustomerAddsATip()
    {
        var (customer, pin) = await SetUpSale(200, 40, tips: true);
        await PresentToPlayer();

        await Press(nameof(ColonyCardTerminalWindow.Btn2));      // 15%
        await TapAndPin(pin);

        Assert.Multiple(() =>
        {
            Assert.That(customer.AccountBalance, Is.EqualTo(200 - 46));
            Assert.That(_merchantCard.AccountBalance, Is.EqualTo(46));
        });
    }

    [Test]
    public async Task WrongPinIsRefusedAndCountsTowardTheLock()
    {
        var (customer, pin) = await SetUpSale(200, 40);
        await PresentToPlayer();

        await TapAndPin(int.Parse(WrongPin(pin)));
        Assert.Multiple(() =>
        {
            Assert.That(customer.AccountBalance, Is.EqualTo(200), "A wrong PIN paid");
            Assert.That(TerminalComp.Step, Is.EqualTo(CardTerminalStep.Pin), "A wrong PIN ended the payment");
            Assert.That(customer.PinAttempts, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NotEnoughMoneyIsDeclined()
    {
        var (customer, pin) = await SetUpSale(10, 40);
        await PresentToPlayer();
        await TapAndPin(pin);

        Assert.Multiple(() =>
        {
            Assert.That(customer.AccountBalance, Is.EqualTo(10));
            Assert.That(TerminalComp.Screen, Is.EqualTo(CardTerminalScreen.Declined));
            Assert.That(Receipt(), Is.Null);
        });
    }

    [Test]
    public async Task WalkingAwayCancelsThePayment()
    {
        await SetUpSale(200, 40);
        await PresentToPlayer();
        Assert.That(IsUiOpen(ColonyCardTerminalUi.Customer));

        await Server.WaitPost(() => SEntMan.System<SharedTransformSystem>().SetCoordinates(SPlayer,
            SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates.Offset(new System.Numerics.Vector2(5, 0))));
        await RunTicks(10);

        Assert.Multiple(() =>
        {
            Assert.That(TerminalComp.Customer, Is.Null);
            Assert.That(TerminalComp.Screen, Is.EqualTo(CardTerminalScreen.Declined));
            Assert.That(IsUiOpen(ColonyCardTerminalUi.Customer), Is.False);
        });
    }

    [Test]
    public async Task OnlyTheCustomerGetsThePaymentWindow()
    {
        await SetUpSale(200, 40);
        var stranger = await SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(TargetCoords));
        await PresentToPlayer();

        await Server.WaitPost(() => SUiSys.OpenUi(_terminal, ColonyCardTerminalUi.Customer, stranger));
        await RunTicks(5);
        Assert.That(SUiSys.IsUiOpen(_terminal, ColonyCardTerminalUi.Customer, stranger), Is.False,
            "Someone else opened the customer's payment window");
    }

    [Test]
    public async Task PriceCannotChangeOnceTheCustomerHasIt()
    {
        await SetUpSale(200, 40);
        await PresentToPlayer();

        await MerchantKey(CardTerminalKey.D9);
        await MerchantKey(CardTerminalKey.Enter);
        Assert.That(TerminalComp.Amount, Is.EqualTo(40));
    }

    [Test]
    public async Task OnlyTheOwnersCardOpensTheSetup()
    {
        await SetUpSale(200, 40);
        await MerchantKey(CardTerminalKey.Cancel);           // disarm
        await MerchantKey(CardTerminalKey.Menu);
        Assert.That(TerminalComp.Screen, Is.EqualTo(CardTerminalScreen.SetupAuth));

        // The merchant mob holds the terminal; give it a stranger's card in the other hand and tap it.
        var strangerCard = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        await Server.WaitPost(() =>
        {
            HandSys.TryPickupAnyHand(_merchant, strangerCard);
            SEntMan.EventBus.RaiseLocalEvent(_terminal,
                new ColonyCardTerminalTapMsg(0) { Actor = _merchant, UiKey = ColonyCardTerminalUi.Merchant });
        });
        await RunTicks(2);

        Assert.That(TerminalComp.Screen, Is.EqualTo(CardTerminalScreen.SetupAuth), "A stranger's card opened the setup");
    }
}
