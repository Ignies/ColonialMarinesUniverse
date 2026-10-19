using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Server.Fax;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Fax.Components;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUComputerSessionSystem))]
[TestOf(typeof(CMUCmbSystem))]
public sealed class CMUComputerSessionTest : GameTest
{
    private static readonly ProtoId<CMUNetSitePrototype> Dump = "CMUNetSiteDeepDump";

    [Test]
    [Description("Terminals are numbered; an ID card logs its holder in and out; the log records it and only engineers can read it.")]
    public async Task CardsLogInAndEngineersReadLogs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var first = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var second = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(first);
            Assert.That(pc.TerminalId, Is.Positive.And.Not.EqualTo(SEntMan.GetComponent<CMUPersonalComputerComponent>(second).TerminalId),
                "Terminals did not get their own numbers.");

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var card = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
            void Swipe() => SEntMan.EventBus.RaiseLocalEvent(first, new InteractUsingEvent(user, card, first, map.GridCoords));

            Assert.That(pc.SessionUser, Is.Null, "A fresh terminal was already logged in.");
            Swipe();
            Assert.That(pc.SessionUser, Is.Not.Null, "Swiping an ID card did not log in.");
            Swipe();
            Assert.That(pc.SessionUser, Is.Null, "Swiping the same card again did not log out.");
            Assert.That(pc.Log, Has.Count.EqualTo(2), "The login and logout were not logged.");

            var ui = SEntMan.System<Robust.Server.GameObjects.UserInterfaceSystem>();
            void Pick(EntityUid actor)
            {
                var message = new CMUComputerToolMessage(CMUComputerToolAction.Logs) { Actor = actor, UiKey = CMUComputerToolUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(first, (object) message);
            }

            Pick(user);
            Assert.That(ui.IsUiOpen(first, CMUComputerLogUiKey.Key, user), Is.False, "Someone with no engineering read the logs.");

            var engineer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SkillsSystem>().SetSkill(engineer, "RMCSkillEngineer", 1);
            Pick(engineer);
            Assert.That(pc.Log.Last().Text, Does.Contain(SEntMan.GetComponent<MetaDataComponent>(engineer).EntityName),
                "Reading the log was not itself logged.");
        });
    }

    [Test]
    [Description("Looking up a live deep address from an unhacked terminal alerts the Marshals; a hacked one does not.")]
    public async Task DeepLookupsAlertTheMarshals()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cmb = SEntMan.System<CMUCmbSystem>();
            var alias = SEntMan.System<CMUDeepAliasSystem>().GetAlias(SProtoMan.Index(Dump));
            var clean = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var hacked = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(hacked);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            void Visit(EntityUid computer, string address)
            {
                var message = new CMUNetVisitMessage(address, false) { Actor = user, UiKey = CMUPersonalComputerUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            var before = cmb.Alerts.Count;
            Visit(clean, "no.such.place");
            Visit(hacked, alias);
            Assert.That(cmb.Alerts, Has.Count.EqualTo(before), "A harmless or hacked lookup raised an alert.");

            Visit(clean, alias);
            Assert.That(cmb.Alerts, Has.Count.EqualTo(before + 1), "A live deep lookup from a clean terminal raised no alert.");
            Assert.That(cmb.Alerts[0].TerminalId, Is.EqualTo(SEntMan.GetComponent<CMUPersonalComputerComponent>(clean).TerminalId));
        });
    }

    [Test]
    [Description("Every received fax is copied for the Marshals.")]
    public async Task FaxesAreCopiedForTheMarshals()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var fax = SEntMan.SpawnEntity("CMFaxCMB", map.GridCoords);
            SEntMan.System<FaxSystem>().Receive(fax, new FaxPrintout("Meet at the docks.", "Tip-off", senderFaxName: "Colony Admin"));
            var copy = SEntMan.System<CMUCmbSystem>().Faxes.FirstOrDefault();
            Assert.That(copy?.Title, Is.EqualTo("Tip-off"), "A received fax was not copied for the Marshals.");
            Assert.That(copy?.From, Is.EqualTo("Colony Admin"));
        });
    }
}
