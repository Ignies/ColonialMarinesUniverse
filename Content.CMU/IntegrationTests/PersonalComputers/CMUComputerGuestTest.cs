using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUComputerSessionSystem))]
public sealed class CMUComputerGuestTest : GameTest
{
    [Test]
    [Description("A colony terminal lets someone browse as a guest, who can buy nothing; a security desk does not; a card at a guest session logs in properly, and logging off ends the guest session.")]
    public async Task GuestsOnPublicTerminalsOnly()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            void Send(EntityUid computer, BoundUserInterfaceMessage message)
            {
                message.Actor = user;
                message.UiKey = CMUPersonalComputerUiKey.Key;
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            var colony = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(colony);
            Send(colony, new CMUComputerGuestLoginMessage());
            Assert.That(pc.Guest, Is.False, "A switched off terminal started a guest session.");

            pc.On = true;
            Send(colony, new CMUComputerGuestLoginMessage());
            Assert.That(pc.Guest, Is.True, "A colony terminal refused a guest.");
            Assert.That(pc.SessionAccount, Is.Zero, "A guest has an account to spend from.");

            var security = SEntMan.SpawnEntity("CMUPersonalDesktopSecurity", map.GridCoords);
            var desk = SEntMan.GetComponent<CMUPersonalComputerComponent>(security);
            desk.On = true;
            Send(security, new CMUComputerGuestLoginMessage());
            Assert.That(desk.Guest, Is.False, "A security desk let a guest in.");

            var weyland = SEntMan.SpawnEntity("CMUPersonalDesktopWeyland", map.GridCoords);
            var company = SEntMan.GetComponent<CMUPersonalComputerComponent>(weyland);
            company.On = true;
            Send(weyland, new CMUComputerGuestLoginMessage());
            Assert.That(company.Guest, Is.False, "A company terminal let a guest in.");

            var card = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(colony, new InteractUsingEvent(user, card, colony, map.GridCoords));
            Assert.That(pc.SessionUser, Is.Not.Null, "A card at a guest session did not log in.");
            Assert.That(pc.Guest, Is.False, "The guest session outlived a proper login.");

            Send(colony, new CMUComputerCardSlotMessage());
            Assert.That(pc.SessionUser, Is.Null);
            Send(colony, new CMUComputerGuestLoginMessage());
            Assert.That(pc.Guest, Is.True);
            Send(colony, new CMUComputerLogoutMessage());
            Assert.That(pc.Guest, Is.False, "Logging off did not end the guest session.");
        });
    }
}
