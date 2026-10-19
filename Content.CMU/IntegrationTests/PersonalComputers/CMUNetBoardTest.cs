using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUNetBoardSystem))]
public sealed class CMUNetBoardTest : GameTest
{
    private const string Herald = "CMUNetSiteColonyHerald";

    [Test]
    [Description("Only the board's poster jobs publish; anyone else comments, named by the server; ghosts are refused; text is capped.")]
    public async Task PostersPublishAndVisitorsComment()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var boards = SEntMan.System<CMUNetBoardSystem>();
            var minds = SEntMan.System<SharedMindSystem>();
            var roles = SEntMan.System<SharedRoleSystem>();

            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var journalist = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var visitor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);

            var mind = minds.CreateMind(null, "Test Journalist");
            minds.TransferTo(mind, journalist, mind: mind.Comp);
            roles.MindAddJobRole(mind, jobPrototype: "AU14JobCivilianJournalist");

            void Raise(EntityUid actor, BoundUserInterfaceMessage message)
            {
                message.Actor = actor;
                message.UiKey = CMUPersonalComputerUiKey.Key;
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Raise(visitor, new CMUNetBoardPostMessage(Herald, "Fake story", "A visitor may not publish."));
            Assert.That(boards.GetPosts(Herald), Is.Empty, "A visitor published on the Herald.");

            var longTitle = new string('x', CMUNetBoardLimits.TitleLength + 40);
            Raise(journalist, new CMUNetBoardPostMessage(Herald, longTitle, "Atmosphere processor fixed."));
            Assert.That(boards.GetPosts(Herald), Has.Count.EqualTo(1), "The journalist could not publish.");

            var post = boards.GetPosts(Herald)[0];
            Assert.Multiple(() =>
            {
                Assert.That(post.Title, Has.Length.EqualTo(CMUNetBoardLimits.TitleLength));
                Assert.That(post.Author, Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(journalist).EntityName));
                Assert.That(post.Role, Is.Not.Empty);
            });

            Raise(visitor, new CMUNetBoardCommentMessage(Herald, post.Id, "[color=red]Great news[/color]"));
            Raise(ghost, new CMUNetBoardCommentMessage(Herald, post.Id, "Ghosts cannot comment."));
            Raise(visitor, new CMUNetBoardCommentMessage(Herald, post.Id, "Second comment inside the cooldown."));

            Assert.That(post.Comments, Has.Count.EqualTo(1), "Ghost comments or comments inside the cooldown went through.");
            Assert.Multiple(() =>
            {
                Assert.That(post.Comments[0].Author, Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(visitor).EntityName));
                Assert.That(post.Comments[0].Body, Is.EqualTo("[color=red]Great news[/color]"),
                    "Comments are stored as typed; the client draws them as plain text.");
            });
        });
    }
}
