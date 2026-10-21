using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Paper;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUShopSystem))]
public sealed class CMUShopTest : GameTest
{
    private static readonly ProtoId<CMUShopCatalogPrototype> Catalog = "CMUWeyBayCatalog";

    [Test]
    [Description("A WeyBay order is charged to the logged in card, refused without the money, and arrives after its delivery time as a named crate holding the item on the pickup pallet.")]
    public async Task OrdersArriveAtThePickup()
    {
        var map = await Pair.CreateTestMap();
        EntityUid computer = default;
        EntityUid card = default;
        EntityUid pallet = default;
        CMUShopEntry entry = default!;
        await Server.WaitAssertion(() =>
        {
            computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            pallet = SEntMan.SpawnEntity("CMUShopPickupPallet", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            card = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
            var id = SEntMan.GetComponent<IdCardComponent>(card);
            entry = SProtoMan.Index(Catalog).Entries[0];
            id.AccountNumber = 424242;
            id.AccountBalance = entry.Price - 1;

            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);
            pc.On = true;
            pc.SessionUser = "Test Buyer";
            pc.SessionAccount = id.AccountNumber;

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            void Buy()
            {
                var message = new CMUShopBuyMessage(Catalog, 0) { Actor = user, UiKey = CMUPersonalComputerUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Buy();
            Assert.That(id.AccountBalance, Is.EqualTo(entry.Price - 1), "An order went through without the money for it.");

            id.AccountBalance = entry.Price + 5;
            Buy();
            Assert.That(id.AccountBalance, Is.EqualTo(5), "The order was not charged to the card.");
        });

        // Not there before its time, there after it.
        await Server.WaitRunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Parcels(), Is.Empty, "The parcel arrived before its delivery time.");
        });

        var ticks = (int) (TimeSpan.FromMinutes(entry.Minutes).TotalSeconds * Server.ResolveDependency<IGameTiming>().TickRate) + 120;
        await Server.WaitRunTicks(ticks);
        await Server.WaitAssertion(() =>
        {
            var parcels = Parcels();
            Assert.That(parcels, Has.Count.EqualTo(1), "The order did not arrive.");
            var crate = parcels[0];
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(crate).EntityName, Does.Contain("Test Buyer"), "The crate is not addressed to the buyer.");
            var storage = SEntMan.GetComponent<EntityStorageComponent>(crate);
            Assert.That(storage.Contents.ContainedEntities.Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID),
                Does.Contain(entry.Item.Id), "The crate does not hold the item bought.");
            var xform = SEntMan.System<SharedTransformSystem>();
            Assert.That((xform.GetWorldPosition(crate) - xform.GetWorldPosition(pallet)).Length(), Is.LessThan(1f),
                "The crate was not left on the pickup pallet.");
        });
    }

    [Test]
    [Description("A buyer can save an order's receipt to Files and print copies; nobody else's card gets it.")]
    public async Task ReceiptsSaveAndPrint()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var card = SEntMan.GetComponent<IdCardComponent>(SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords));
            var entry = SProtoMan.Index(Catalog).Entries[0];
            card.AccountNumber = 515151;
            card.AccountBalance = entry.Price;

            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);
            pc.On = true;
            pc.SessionUser = "Test Buyer";
            pc.SessionAccount = card.AccountNumber;

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            void Send(BoundUserInterfaceMessage message)
            {
                message.Actor = user;
                message.UiKey = CMUPersonalComputerUiKey.Key;
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Send(new CMUShopBuyMessage(Catalog, 0));
            Send(new CMUShopReceiptMessage(1, false));
            Assert.That(pc.Notes, Has.Count.EqualTo(1), "The receipt was not saved to Files.");
            Assert.That(pc.Notes[0].Text, Does.Contain("Test Buyer").And.Contain("**5151").And.Not.Contain("515151"),
                "The receipt does not name the buyer with the card masked.");

            Send(new CMUShopReceiptMessage(1, true));
            Assert.That(SEntMan.Count<PaperComponent>(), Is.EqualTo(1), "The receipt was not printed.");

            pc.SessionAccount = 999999;
            Send(new CMUShopReceiptMessage(1, false));
            Assert.That(pc.Notes, Has.Count.EqualTo(1), "Another card got the buyer's receipt.");
        });
    }

    private List<EntityUid> Parcels()
    {
        var parcels = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<EntityStorageComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            if (meta.EntityName.StartsWith("WeyBay"))
                parcels.Add(uid);
        }

        return parcels;
    }
}
