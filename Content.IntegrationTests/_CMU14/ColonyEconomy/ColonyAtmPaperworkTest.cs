using System.Linq;
using Content.Client.CMU14.ColonyEconomy;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

public sealed class ColonyAtmPaperworkTest : ColonyAtmTestBase
{
    private EntityUid? PaperInSlot()
        => SEntMan.System<ColonyBankPaperworkSystem>().GetWaitingPaper(STarget!.Value, ColonyAtmComponent.ReceiptSlotId);

    private string Content(EntityUid paper) => SEntMan.GetComponent<PaperComponent>(paper).Content;

    private async Task TransferTo(int account, int amount)
    {
        await Type("3", enter: false);              // 3) TRANSFER
        await Type(account.ToString());
        await Type(amount.ToString());
        await Enter();
    }

    [Test]
    public async Task TransferCertificateWaitsInTheSlotUntilTaken()
    {
        await SpawnTarget(Atm);
        var (_, pin, from) = await InsertNewCard(500);
        var (otherUid, _, to) = await SpawnOtherCard();
        await Type(pin.ToString());
        await TransferTo(to, 120);

        Assert.That(ClientAtmState().CertificateReady, "The result screen offers no certificate");
        await Type("1", enter: false);

        var paper = PaperInSlot();
        Assert.That(paper, Is.Not.Null, "Nothing was printed");
        var reference = SEntMan.System<ColonyBankSystem>().GetHistory(otherUid).Last().Reference;
        var paperComp = SEntMan.GetComponent<PaperComponent>(paper!.Value);
        Assert.Multiple(() =>
        {
            Assert.That(reference, Does.StartWith("TRF-"), "The transfer has no reference");
            Assert.That(Content(paper.Value), Does.Contain(reference!).And.Contain($"#{from}").And.Contain($"#{to}").And.Contain("$120"));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(paper.Value).EntityPrototype?.ID, Is.EqualTo("CMUPaperWEYLAND"));
            Assert.That(paperComp.StampedBy, Is.Not.Empty, "The certificate is not stamped");
            Assert.That(paperComp.EditingDisabled, "The certificate can be written on");
            Assert.That(ClientAtmState().ReceiptWaiting, "The screen doesn't show the paper in the slot");
            Assert.That(ClientAtmState().CertificateReady, Is.False, "The certificate can be printed twice");
        });

        await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnReceipt));
        await RunTicks(5);
        Assert.Multiple(() =>
        {
            Assert.That(HeldItem(), Is.EqualTo(paper), "The certificate didn't go into the hand");
            Assert.That(PaperInSlot(), Is.Null);
        });
    }

    [Test]
    public async Task StatementListsTheHistoryWithReferences()
    {
        await SpawnTarget(Atm);
        var (_, pin, account) = await InsertNewCard(500);
        var (_, _, to) = await SpawnOtherCard();
        await Type(pin.ToString());
        await TransferTo(to, 75);
        await Enter();                               // back to the menu

        await Type("5", enter: false);              // 5) HISTORY
        await Type("1", enter: false);              // print

        var paper = PaperInSlot();
        Assert.That(paper, Is.Not.Null, "No statement was printed");
        var text = Content(paper!.Value);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain($"#{account}"));
            Assert.That(text, Does.Contain($"#{to}").And.Contain("-$75").And.Contain("TRF-"));
        });
    }

    [Test]
    public async Task OnePaperAtATime()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());
        await Type("5", enter: false);
        await Type("1", enter: false);
        var first = PaperInSlot();

        await Type("1", enter: false);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(PaperInSlot(), Is.EqualTo(first), "A second paper pushed the first out");
        });
    }

    [Test]
    public async Task NoCertificateAfterLeavingTheResult()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(500);
        var (_, _, to) = await SpawnOtherCard();
        await Type(pin.ToString());
        await TransferTo(to, 50);
        Assert.That(ClientAtmState().CertificateReady);

        await Enter();                               // back to the menu
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.PendingCertificate, Is.Null, "The certificate outlived the result screen");
            Assert.That(ClientAtmState().CertificateReady, Is.False);
            Assert.That(PaperInSlot(), Is.Null);
        });
    }
}
