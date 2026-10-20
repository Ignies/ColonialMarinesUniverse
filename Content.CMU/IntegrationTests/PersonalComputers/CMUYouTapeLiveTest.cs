using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUYouTapeLiveSystem))]
public sealed class CMUYouTapeLiveTest : GameTest
{
    [Test]
    [Description("A broadcasting camera is on YouTape's live list, with whoever carries it, and drops off when it is gone.")]
    public async Task BroadcastingCamerasAreLive()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var live = SEntMan.System<CMUYouTapeLiveSystem>();
            var before = live.Feeds().Count;
            var camera = SEntMan.SpawnEntity("RMCCameraBroadcasting", map.GridCoords);
            var net = SEntMan.GetNetEntity(camera);
            Assert.That(live.Feeds().Select(f => f.Camera), Does.Contain(net), "A broadcasting camera was not on the live list.");
            Assert.That(live.Feeds().Single(f => f.Camera == net).Holder, Is.Empty, "A camera on the floor had a holder.");

            var mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SharedTransformSystem>().SetParent(camera, mob);
            Assert.That(live.Feeds().Single(f => f.Camera == net).Holder,
                Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(mob).EntityName), "The live list did not name who carries the camera.");

            SEntMan.DeleteEntity(camera);
            Assert.That(live.Feeds(), Has.Count.EqualTo(before), "A deleted camera stayed on the live list.");
        });
    }
}
