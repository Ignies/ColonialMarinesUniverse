using System.IO;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Server.CMU14.Photography;
using Content.Shared.CMU14.Photography;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUPhotoSystem))]
public sealed class CMUPhotoTest : GameTest
{
    private static byte[] Png(int size)
    {
        using var image = new Image<Rgba32>(size, size, new Rgba32(40, 200, 90, 255));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    [Test]
    [Description("Only a PNG of the frame's exact size, for a request the server made, becomes a print; held prints attach to postings.")]
    public async Task CapturesBecomePrintsAndAttachToPostings()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;

        await Server.WaitAssertion(() =>
        {
            var photos = SEntMan.System<CMUPhotoSystem>();
            var boards = SEntMan.System<CMUNetBoardSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var minds = SEntMan.System<SharedMindSystem>();
            var roles = SEntMan.System<SharedRoleSystem>();

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, user);
            var mind = minds.CreateMind(session.UserId, "Test Journalist");
            minds.TransferTo(mind, user, mind: mind.Comp);
            roles.MindAddJobRole(mind, jobPrototype: "AU14JobCivilianJournalist");

            var cameraUid = SEntMan.SpawnEntity("RMCCameraBroadcasting", map.GridCoords);
            var camera = new Entity<CMUPhotoCameraComponent>(cameraUid, SEntMan.GetComponent<CMUPhotoCameraComponent>(cameraUid));
            var frame = camera.Comp.Size * CMUPhotoLimits.TilePixels;

            var wrong = photos.RequestCapture(session, user, camera, map.GridCoords);
            Assert.That(photos.TryAcceptCapture(session, wrong, Png(frame + 1), out _), Is.False,
                "A capture of the wrong size was printed.");

            var right = photos.RequestCapture(session, user, camera, map.GridCoords);
            Assert.That(photos.TryAcceptCapture(session, right, Png(frame), out var print), Is.True,
                "A capture of the frame's size was refused.");
            Assert.That(photos.TryAcceptCapture(session, right, Png(frame), out _), Is.False,
                "The same request printed twice.");

            var photo = SEntMan.GetComponent<CMUPhotoComponent>(print);
            Assert.Multiple(() =>
            {
                Assert.That(photos.HasPhoto(photo.Id));
                Assert.That(photo.Size, Is.EqualTo(camera.Comp.Size));
                Assert.That(photo.Photographer, Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(user).EntityName));
            });

            Assert.That(hands.TryPickupAnyHand(user, print), "The journalist could not pick up the print.");
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var post = new CMUNetBoardPostMessage("CMUNetSiteColonyHerald", "Photo story", "See the photo.", attachPhoto: true)
            {
                Actor = user,
                UiKey = CMUPersonalComputerUiKey.Key,
            };
            SEntMan.EventBus.RaiseLocalEvent(computer, (object) post);

            var posts = boards.GetPosts("CMUNetSiteColonyHerald");
            Assert.That(posts, Has.Count.EqualTo(1));
            Assert.That(posts[0].Photo, Is.EqualTo(photo.Id), "The held print was not attached to the posting.");
        });
    }
}
