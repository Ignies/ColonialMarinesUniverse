using System.Linq;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Client.CMU14.PersonalComputers;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUDeepMazeSystem))]
[TestOf(typeof(CMUDeepLeakSystem))]
public sealed class CMUDeepMazeTest : GameTest
{
    private static readonly ProtoId<CMUNetSitePrototype> Dump = "CMUNetSiteDeepDump";
    private static readonly ProtoId<CMUNetSitePrototype> Herald = "CMUNetSiteColonyHerald";

    [Test]
    [Description("Every hidden site has a chain of ad pages; following it to the end unlocks the site, and the pages parse in the browser.")]
    public async Task AdChainsUnlockHiddenSites()
    {
        var map = await Pair.CreateTestMap();
        var pages = new List<string>();

        await Server.WaitAssertion(() =>
        {
            var maze = SEntMan.System<CMUDeepMazeSystem>();
            var aliases = SEntMan.System<CMUDeepAliasSystem>();
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var access = SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(computer);

            foreach (var site in SProtoMan.EnumeratePrototypes<CMUNetSitePrototype>().Where(s => s.Hidden))
            {
                var chain = maze.ChainTo(site.ID);
                Assert.That(chain, Has.Count.GreaterThanOrEqualTo(2), $"{site.ID} has no ad chain.");
                Assert.That(maze.Entries, Does.Contain(chain[0]), "A chain's first page is not something a scan can find.");

                for (var i = 0; i < chain.Count; i++)
                {
                    Assert.That(CMUDeepAddress.IsAd(chain[i]), $"{chain[i]} is not a valid ad host.");
                    var next = i + 1 < chain.Count ? chain[i + 1] : aliases.GetAlias(site);
                    var page = maze.Generate(chain[i]);
                    Assert.That(page, Does.Contain($"link=\"{next}\""), $"{chain[i]} does not link on to {next}.");
                    Assert.That(maze.Generate(chain[i]), Is.EqualTo(page), "The same ad host gave two different pages.");
                    pages.Add(page);
                }

                Assert.That(access.Unlocked, Does.Not.Contain(site.ID), $"{site.ID} was open before reaching its door.");
                foreach (var host in chain)
                {
                    var request = new CMUDeepPageRequestMessage(host) { Actor = user, UiKey = CMUPersonalComputerUiKey.Key };
                    SEntMan.EventBus.RaiseLocalEvent(computer, (object) request);
                }

                Assert.That(access.Unlocked, Does.Contain(site.ID), $"Following the ads did not unlock {site.ID}.");
            }

            pages.Add(maze.Generate(maze.JunkHost()));
        });

        var parser = Pair.Client.ResolveDependency<DocumentParsingManager>();
        await Pair.Client.WaitAssertion(() =>
        {
            foreach (var page in pages)
            {
                var document = new Document();
                Assert.That(parser.TryAddMarkup(document, page), $"An ad page failed to parse:\n{page}");
                Assert.That(CMUNetPageView.Descendants(document).OfType<GuidebookError>(), Is.Empty, $"An ad page has invalid markup:\n{page}");
                Assert.That(CMUNetPageView.Descendants(document).OfType<CMUNetPopup>(), Is.Not.Empty, "An ad page has no pop up.");
            }

            // Every product ad names a real item and draws.
            var protoMan = Pair.Client.ResolveDependency<IPrototypeManager>();
            foreach (var ad in protoMan.EnumeratePrototypes<CMUDeepAdPrototype>())
            {
                var document = new Document();
                parser.TryAddMarkup(document, $"<Document><CMUNetAd Id=\"{ad.ID}\"/></Document>");
                Assert.That(CMUNetPageView.Descendants(document).OfType<CMUNetAd>(), Is.Not.Empty,
                    $"Ad {ad.ID} did not draw; is {ad.Entity} a real entity?");
            }
        });
    }

    [Test]
    [Description("A dump shows two PIN digits, and the next dump of the same account shows the other two.")]
    public async Task DumpsSpreadThePinAcrossLeaks()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var leaks = SEntMan.System<CMUDeepLeakSystem>();
            var card = SEntMan.GetComponent<IdCardComponent>(SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords));
            int code = card.AtmPin;
            var pin = code.ToString("D4");

            var first = leaks.MaskPin(card).Replace(" ", string.Empty);
            var second = leaks.MaskPin(card).Replace(" ", string.Empty);
            Assert.Multiple(() =>
            {
                Assert.That(first.Count(c => c != '_'), Is.EqualTo(2), "A dump did not show exactly two digits.");
                Assert.That(second.Count(c => c != '_'), Is.EqualTo(2), "A dump did not show exactly two digits.");
                for (var i = 0; i < 4; i++)
                {
                    var shown = first[i] != '_' ? first[i] : second[i];
                    Assert.That(shown, Is.EqualTo(pin[i]), "Two dumps together did not give the whole PIN.");
                }
            });
        });
    }

    [Test]
    [Description("Only insurgents seize the Herald; the poster sees the propaganda over the front page; one hijack per cooldown.")]
    public async Task InsurgentsHijackTheHerald()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var boards = SEntMan.System<CMUNetBoardSystem>();
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(computer);
            var colonist = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var insurgent = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<CLFMemberComponent>(insurgent);

            void Hijack(EntityUid actor, string title)
            {
                var message = new CMUDeepHijackMessage(title, "The colony belongs to those who built it.")
                {
                    Actor = actor,
                    UiKey = CMUPersonalComputerUiKey.Key,
                };
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Hijack(colonist, "NOT AN INSURGENT");
            Assert.That(boards.GetHijack(Herald), Is.Null, "Someone who is not an insurgent seized the Herald.");

            Hijack(insurgent, "WAKE UP");
            Assert.That(boards.GetHijack(Herald)?.Title, Is.EqualTo("WAKE UP"), "An insurgent could not seize the Herald.");

            Hijack(insurgent, "AGAIN");
            Assert.That(boards.GetHijack(Herald)?.Title, Is.EqualTo("WAKE UP"), "The hijack cooldown did not hold.");
        });
    }

    [Test]
    [Description("Pages this computer can open are saved to the desktop; hidden or unknown ones are not.")]
    public async Task SaveLinksToTheDesktop()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);

            void Save(string address)
            {
                var message = new CMUNetSaveShortcutMessage(address, "Saved") { Actor = user, UiKey = CMUPersonalComputerUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Save("herald.colony.news");
            Save("no.such.site");
            Save("dump.deep");
            Save("deeps://home");
            Assert.That(pc.Shortcuts.Select(s => s.Address), Is.EqualTo(new[] { "herald.colony.news" }),
                "Only the reachable page should have been saved on a computer that is not hacked.");

            var access = SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(computer);
            access.Unlocked.Add(Dump);
            Save("dump.deep");
            Assert.That(pc.Shortcuts, Has.Count.EqualTo(1), "A deep site was saved by its fixed name instead of this round's address.");

            var alias = access.Aliases[Dump];
            Assert.That(alias, Does.EndWith(".deep").And.Not.EqualTo("dump.deep"), "The dump has no random address this round.");
            Save(alias);
            Save("deeps://home");
            Assert.That(pc.Shortcuts, Has.Count.EqualTo(3), "A hacked computer could not save its deep pages.");
        });
    }
}
