using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

/// <summary>Traces, log tampering, mail, notes, wallpapers, SpaceGPT, the arcade and the deep market.</summary>
[TestFixture]
public sealed class CMUComputerFeaturesTest : GameTest
{
    private EntityUid Computer(EntityCoordinates at)
    {
        return SEntMan.SpawnEntity("RMCPersonalDesktop", at);
    }

    /// <summary>A colonist with an ID card worth the given money, logged in to the computer.</summary>
    private (EntityUid User, IdCardComponent Card) LogIn(EntityUid computer, EntityCoordinates at, int money = 0)
    {
        var user = SEntMan.SpawnEntity("CMMobHuman", at);
        var cardUid = SEntMan.SpawnEntity("CMIDCardGold", at);
        var card = SEntMan.GetComponent<IdCardComponent>(cardUid);
        card.FullName = $"Colonist {cardUid.Id}";
        card.AccountBalance = money;
        SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, cardUid, computer, at));
        return (user, card);
    }

    private void Send(EntityUid computer, EntityUid actor, BoundUserInterfaceMessage message, Enum? key = null)
    {
        message.Actor = actor;
        message.UiKey = key ?? CMUPersonalComputerUiKey.Key;
        SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
    }

    [Test]
    [Description("A trace that finishes while someone is logged in names them on the Marshals' alerts.")]
    public async Task TracesNameWhoIsLoggedIn()
    {
        var map = await Pair.CreateTestMap();
        var timing = Server.ResolveDependency<IGameTiming>();
        CMUPersonalComputerComponent terminal = default!;
        await Server.WaitAssertion(() =>
        {
            var target = Computer(map.GridCoords);
            LogIn(target, map.GridCoords);
            terminal = SEntMan.GetComponent<CMUPersonalComputerComponent>(target);
            Assert.That(SEntMan.System<CMUCmbSystem>().StartTrace((target, terminal)), "The trace did not start.");
            Assert.That(terminal.TraceEnds, Is.GreaterThan(timing.CurTime), "The traced terminal does not show the trace.");
            terminal.TraceEnds = timing.CurTime;
        });

        await Server.WaitRunTicks(3);
        await Server.WaitAssertion(() =>
        {
            var alert = SEntMan.System<CMUCmbSystem>().Alerts.FirstOrDefault(a => a.Trace != null);
            Assert.That(alert?.Trace, Does.Contain(terminal.SessionUser!), "The finished trace did not name the user.");
        });
    }

    [Test]
    [Description("Skilled engineers can wipe a log, which leaves its own line, and forge lines; others cannot.")]
    public async Task EngineersTamperWithLogs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var computer = Computer(map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);
            LogIn(computer, map.GridCoords);
            var novice = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var expert = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SkillsSystem>().SetSkill(expert, "RMCSkillEngineer", 3);

            var before = pc.Log.Count;
            Send(computer, novice, new CMUComputerLogWipeMessage(), CMUComputerLogUiKey.Key);
            Assert.That(pc.Log, Has.Count.EqualTo(before), "Someone without the skill wiped the log.");

            Send(computer, expert, new CMUComputerLogWipeMessage(), CMUComputerLogUiKey.Key);
            Assert.That(pc.Log, Has.Count.EqualTo(1), "The wipe should leave exactly its own line.");

            Send(computer, expert, new CMUComputerLogForgeMessage("Opened herald.colony.news"), CMUComputerLogUiKey.Key);
            Assert.That(pc.Log.Last().Text, Is.EqualTo("Opened herald.colony.news"), "The forged line is missing.");
        });
    }

    [Test]
    [Description("Mail goes to a name's mailbox; notes are saved and deleted; found wallpapers go on the desktop.")]
    public async Task MailNotesAndWallpapers()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var first = Computer(map.GridCoords);
            var second = Computer(map.GridCoords);
            var (sender, _) = LogIn(first, map.GridCoords);
            var (_, receiverCard) = LogIn(second, map.GridCoords);

            Send(first, sender, new CMUMailSendMessage(receiverCard.FullName!, "Lunch", "Canteen at noon?"));
            Assert.That(SEntMan.System<CMUMailSystem>().Inbox(receiverCard.FullName!).Select(m => m.Subject), Does.Contain("Lunch"),
                "The mail did not arrive.");

            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(first);
            Send(first, sender, new CMUNoteSaveMessage("passwords", "pump room: 4471"));
            Assert.That(pc.Notes.Select(n => n.Name), Does.Contain("passwords"), "The note was not saved.");
            Send(first, sender, new CMUNoteDeleteMessage(0));
            Assert.That(pc.Notes, Is.Empty, "The note was not deleted.");

            Send(first, sender, new CMUNetSetWallpaperMessage("CMUWallpaperSynthwave"));
            Assert.That(pc.Wallpaper?.Id, Is.EqualTo("CMUWallpaperSynthwave"), "The wallpaper was not set.");
            Send(first, sender, new CMUNetSetWallpaperMessage("NoSuchWallpaper"));
            Assert.That(pc.Wallpaper?.Id, Is.EqualTo("CMUWallpaperSynthwave"), "An unknown wallpaper was accepted.");
        });
    }

    [Test]
    [Description("A SpaceGPT question opens a poll for the ghosts, and is answered once voting ends.")]
    public async Task SpaceGptAnswersAfterTheVote()
    {
        var map = await Pair.CreateTestMap();
        var spaceGpt = Server.System<CMUSpaceGptSystem>();
        await Server.WaitAssertion(() =>
        {
            var computer = Computer(map.GridCoords);
            var (user, _) = LogIn(computer, map.GridCoords);
            Send(computer, user, new CMUSpaceGptAskMessage("Will I survive the shift?"));
            Assert.That(spaceGpt.OpenPolls, Is.EqualTo(1), "The question did not reach the ghosts.");
        });

        await Server.WaitRunTicks((int) (CMUSpaceGptLimits.Voting.TotalSeconds * 35));
        await Server.WaitAssertion(() => Assert.That(spaceGpt.OpenPolls, Is.Zero, "The poll was never answered."));
    }

    [Test]
    [Description("Arcade scores go on the round board, best first.")]
    public async Task ArcadeKeepsHighScores()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var computer = Computer(map.GridCoords);
            var (user, _) = LogIn(computer, map.GridCoords);
            Send(computer, user, new CMUArcadeSubmitMessage(CMUArcadeGame.HuggerRun, 120));
            Send(computer, user, new CMUArcadeSubmitMessage(CMUArcadeGame.HuggerRun, 340));
            Send(computer, user, new CMUArcadeSubmitMessage(CMUArcadeGame.HuggerRun, 999999));
            var scores = SEntMan.System<CMUComputerFunSystem>().Scores(CMUArcadeGame.HuggerRun).Select(s => s.Score);
            Assert.That(scores, Is.EqualTo(new[] { 340, 120 }), "Scores are wrong, out of order, or an impossible one got in.");
        });
    }

    [Test]
    [Description("Insurgents list on the deep market; a buyer pays the seller and the listing goes.")]
    public async Task DeepMarketMovesMoney()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var market = SEntMan.System<CMUDeepEconomySystem>();
            var sellerPc = Computer(map.GridCoords);
            var buyerPc = Computer(map.GridCoords);
            SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(sellerPc);
            SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(buyerPc);
            var (seller, sellerCard) = LogIn(sellerPc, map.GridCoords, 0);
            var (buyer, buyerCard) = LogIn(buyerPc, map.GridCoords, 1000);

            Send(sellerPc, seller, new CMUMarketListMessage("Blank ID", "Never swiped", 400, "Behind the canteen"));
            Assert.That(market.Listings, Is.Empty, "Someone who is not an insurgent listed goods.");

            SEntMan.EnsureComponent<CLFMemberComponent>(seller);
            Send(sellerPc, seller, new CMUMarketListMessage("Blank ID", "Never swiped", 400, "Behind the canteen"));
            var listing = market.Listings.Single();

            Send(buyerPc, buyer, new CMUMarketBuyMessage(listing.Id));
            Assert.Multiple(() =>
            {
                Assert.That(buyerCard.AccountBalance, Is.EqualTo(600), "The buyer was not charged.");
                Assert.That(sellerCard.AccountBalance, Is.EqualTo(400), "The seller was not paid.");
                Assert.That(market.Listings, Is.Empty, "The sold listing is still up.");
            });
        });
    }
}
