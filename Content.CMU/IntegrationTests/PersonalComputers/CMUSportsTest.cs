using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Interaction;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

/// <summary>The sportsbook: matches go the way the server drew them, kits exist, and bets pay out to the card.</summary>
[TestFixture]
public sealed class CMUSportsTest : GameTest
{
    [Test]
    [Description("Every sport ends with the drawn winner on top, and the same seed always plays the same match.")]
    public void MatchesEndWithTheWinner()
    {
        foreach (var sport in Enum.GetValues<CMUSport>())
        {
            var entrants = CMUSports.Entrants(sport);
            for (var seed = 0; seed < 200; seed++)
            {
                var winner = seed % entrants;
                var play = CMUSports.Play(sport, seed, entrants, winner);
                Assert.That(CMUSports.Play(sport, seed, entrants, winner).Result, Is.EqualTo(play.Result), $"{sport} seed {seed} is not repeatable.");
                Assert.That(play.Events.All(e => e.Time >= 0 && e.Time <= CMUSports.Length(sport)), $"{sport} seed {seed} plays outside its time.");
                switch (sport)
                {
                    case CMUSport.Sprint:
                        Assert.That(play.Score[winner], Is.EqualTo(play.Score.Min()), $"Sprint seed {seed}: the winner was not fastest.");
                        Assert.That(play.Score.Count(s => s == play.Score[winner]), Is.EqualTo(1), $"Sprint seed {seed}: a dead heat.");
                        break;
                    case CMUSport.Boxing:
                        var knockout = play.Events.FirstOrDefault(e => e.Action == CMUSportsAction.Knockout);
                        if (knockout.Action == CMUSportsAction.Knockout)
                            Assert.That(knockout.Side, Is.Not.EqualTo(winner), $"Boxing seed {seed}: the winner was knocked out.");
                        else
                            Assert.That(play.Result, Does.StartWith("UD"), $"Boxing seed {seed}: no knockout and no decision.");
                        break;
                    default:
                        Assert.That(play.Score[winner], Is.GreaterThan(play.Score[1 - winner]), $"{sport} seed {seed}: the winner did not outscore.");
                        break;
                }
            }
        }
    }

    [Test]
    [Description("Every kit the arena dresses its dolls in is a real starting gear.")]
    public void KitsExist()
    {
        var prototypes = Server.ResolveDependency<IPrototypeManager>();
        var kits = CMUSports.Colours.SelectMany((_, colour) => new[] { "Runner", "Player", "Spacer" }.Select(role => CMUSports.Kit(role, colour)))
            .Concat(new[] { CMUSports.Kit("Boxer", 0), CMUSports.Kit("Boxer", 1) });
        foreach (var kit in kits)
            Assert.That(prototypes.HasIndex<StartingGearPrototype>(kit), $"Missing kit {kit}.");
    }

    [Test]
    [Description("A bet on the winner pays stake times odds back to the card; bets on anyone else are lost.")]
    public async Task BetsPayTheWinner()
    {
        var map = await Pair.CreateTestMap();
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var fun = SEntMan.System<CMUComputerFunSystem>();
            var match = fun.Matches.First(m => m.Result == null);
            var cards = new List<IdCardComponent>();
            for (var slot = 0; slot < match.Entrants.Count; slot++)
            {
                var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
                var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                var cardUid = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
                var card = SEntMan.GetComponent<IdCardComponent>(cardUid);
                card.FullName = $"Punter {slot}";
                card.AccountBalance = 1000;
                SEntMan.GetComponent<CMUPersonalComputerComponent>(computer).On = true;
                SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, cardUid, computer, map.GridCoords));
                var bet = new CMUBetPlaceMessage(match.Id, slot, 100) { Actor = user, UiKey = CMUPersonalComputerUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) bet);
                Assert.That(card.AccountBalance, Is.EqualTo(900), $"The stake on slot {slot} was not taken.");
                cards.Add(card);
            }

            fun.PlayNow(match.Id);
            var played = fun.Matches.First(m => m.Id == match.Id);
            Assert.That(played.Result, Is.Not.Null, "The match was not played.");
            for (var slot = 0; slot < cards.Count; slot++)
            {
                var expected = slot == match.Winner ? 900 + (int) (100 * match.Odds[slot]) : 900;
                Assert.That(cards[slot].AccountBalance, Is.EqualTo(expected), $"Slot {slot} was settled wrong.");
            }
        });
    }
}
