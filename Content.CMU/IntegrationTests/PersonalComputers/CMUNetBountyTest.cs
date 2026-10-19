using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Interaction;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUNetBoardSystem))]
public sealed class CMUNetBountyTest : GameTest
{
    private static readonly ProtoId<CMUNetSitePrototype> Bounties = "CMUNetSiteColonyBounties";

    [Test]
    [Description("A bounty leaves the poster's account when posted, goes to the commenter the poster picks, and a withdrawn one comes back.")]
    public async Task BountiesMoveMoney()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var boards = SEntMan.System<CMUNetBoardSystem>();
            var posterPc = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var hunterPc = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var poster = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hunter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var posterCard = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
            var hunterCard = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
            var posterAccount = SEntMan.GetComponent<IdCardComponent>(posterCard);
            var hunterAccount = SEntMan.GetComponent<IdCardComponent>(hunterCard);
            posterAccount.AccountBalance = 1000;
            hunterAccount.AccountBalance = 0;

            SEntMan.EventBus.RaiseLocalEvent(posterPc, new InteractUsingEvent(poster, posterCard, posterPc, map.GridCoords));
            SEntMan.EventBus.RaiseLocalEvent(hunterPc, new InteractUsingEvent(hunter, hunterCard, hunterPc, map.GridCoords));

            void Send(EntityUid computer, EntityUid actor, BoundUserInterfaceMessage message)
            {
                message.Actor = actor;
                message.UiKey = CMUPersonalComputerUiKey.Key;
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Send(posterPc, poster, new CMUNetBoardPostMessage(Bounties, "Lost cat", "Orange, answers to Jones.", 300));
            var post = boards.GetPosts(Bounties).First();
            Assert.Multiple(() =>
            {
                Assert.That(posterAccount.AccountBalance, Is.EqualTo(700), "Posting the bounty did not take the money.");
                Assert.That(post.Reward, Is.EqualTo(300));
                Assert.That(post.Terminal, Is.EqualTo(SEntMan.GetComponent<CMUPersonalComputerComponent>(posterPc).TerminalId),
                    "The posting does not say which terminal it came from.");
            });

            Send(hunterPc, hunter, new CMUNetBoardCommentMessage(Bounties, post.Id, "Found him in the vents."));
            Send(hunterPc, hunter, new CMUNetBoardPayoutMessage(Bounties, post.Id, 0));
            Assert.That(hunterAccount.AccountBalance, Is.EqualTo(0), "Someone other than the poster paid out the bounty.");

            Send(posterPc, poster, new CMUNetBoardPayoutMessage(Bounties, post.Id, 0));
            Assert.Multiple(() =>
            {
                Assert.That(hunterAccount.AccountBalance, Is.EqualTo(300), "The bounty did not reach the commenter.");
                Assert.That(post.Reward, Is.Zero);
                Assert.That(post.PaidTo, Is.Not.Null);
            });

            // A second posting, withdrawn, gives the money back. The post cooldown is skipped by a new poster.
            var other = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Send(posterPc, other, new CMUNetBoardPostMessage(Bounties, "Pump repair", "North pump is down.", 200));
            var second = boards.GetPosts(Bounties).First();
            Assert.That(posterAccount.AccountBalance, Is.EqualTo(500));
            Send(posterPc, other, new CMUNetBoardWithdrawMessage(Bounties, second.Id));
            Assert.That(posterAccount.AccountBalance, Is.EqualTo(700), "A withdrawn bounty was not refunded.");
        });
    }

    [Test]
    [Description("Switching the deep web off stops the hack from starting.")]
    public async Task SwitchesTurnFeaturesOff()
    {
        var map = await Pair.CreateTestMap();
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitAssertion(() =>
        {
            cfg.SetCVar(CMUPersonalComputerCVars.DeepWeb, false);
            try
            {
                var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
                var engineer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                SEntMan.System<SkillsSystem>().SetSkill(engineer, "RMCSkillEngineer", 4);
                var pick = new CMUComputerToolMessage(CMUComputerToolAction.Hack) { Actor = engineer, UiKey = CMUComputerToolUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) pick);
                Assert.That(SEntMan.GetComponent<CMUDeepWebHackableComponent>(computer).Board, Is.Null,
                    "The hack started with the deep web switched off.");
            }
            finally
            {
                cfg.SetCVar(CMUPersonalComputerCVars.DeepWeb, true);
            }
        });
    }
}
