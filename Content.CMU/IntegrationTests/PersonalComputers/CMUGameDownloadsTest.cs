using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUGameDownloadSystem))]
public sealed class CMUGameDownloadsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    [Description("Arcade games download to the desktop once each and delete again; unknown games and downloads while the arcade is off do not land.")]
    public async Task GamesDownloadToTheDesktop()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);

            void Raise(BoundUserInterfaceMessage message)
            {
                message.Actor = user;
                message.UiKey = CMUPersonalComputerUiKey.Key;
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Raise(new CMUGameDownloadMessage(CMUArcadeGame.HuggerRun));
            Raise(new CMUGameDownloadMessage(CMUArcadeGame.HuggerRun));
            Raise(new CMUGameDownloadMessage((CMUArcadeGame) 200));
            Assert.That(pc.Downloads, Is.EqualTo(new[] { CMUArcadeGame.HuggerRun }),
                "A game should download once, and only games that exist should download.");
            Assert.That(pc.Shortcuts, Is.Empty, "A download took a saved link's place on the desktop.");

            Raise(new CMUGameDeleteMessage(CMUArcadeGame.HuggerRun));
            Assert.That(pc.Downloads, Is.Empty, "A downloaded game could not be deleted from the desktop.");

            cfg.SetCVar(CMUPersonalComputerCVars.Arcade, false);
            try
            {
                Raise(new CMUGameDownloadMessage(CMUArcadeGame.TrackerReflex));
                Assert.That(pc.Downloads, Is.Empty, "A game downloaded while the arcade was off.");
            }
            finally
            {
                cfg.SetCVar(CMUPersonalComputerCVars.Arcade, true);
            }
        });
    }
}
