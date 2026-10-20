using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Interaction;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUComputerSessionSystem))]
public sealed class CMUComputerScreenTest : GameTest
{
    [Test]
    [Description("A running session keeps what is on screen for the next viewer, within limits; ending the session or switching off clears it, and switching on records when.")]
    public async Task ScreenStaysForTheNextViewer()
    {
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        // The test map has no power, and an unpowered terminal will not switch on.
        await Server.WaitPost(() => cfg.SetCVar(CMUPersonalComputerCVars.NeedsPower, false));
        try
        {
            var map = await Pair.CreateTestMap();
            await Server.WaitAssertion(() =>
            {
                var timing = Server.ResolveDependency<IGameTiming>();
                var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
                var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);
                var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                var card = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);

                void Send(BoundUserInterfaceMessage message)
                {
                    message.Actor = user;
                    message.UiKey = CMUPersonalComputerUiKey.Key;
                    SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
                }

                CMUComputerScreenWindow Browser(string address, float x = 40)
                {
                    return new CMUComputerScreenWindow { App = "Browser", Address = address, Position = new Vector2(x, 20), Size = new Vector2(640, 440) };
                }

                Send(new CMUComputerScreenMessage(new() { Browser("intranet.wy") }));
                Assert.That(pc.Screen, Is.Empty, "A switched off terminal kept a screen.");

                SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, card, computer, map.GridCoords));
                Send(new CMUComputerPowerButtonMessage());
                Assert.That(pc.On, Is.True, "The power button did not switch the terminal on.");
                Assert.That(pc.PoweredAt, Is.EqualTo(timing.CurTime), "Switching on did not record when.");
                Assert.That(pc.SessionUser, Is.Not.Null, "Switching on with a card in did not log in.");

                Send(new CMUComputerScreenMessage(new()
                {
                    Browser("intranet.wy/memo", float.NaN),
                    Browser(new string('a', CMUComputerScreenLimits.AddressLength + 1)),
                    new CMUComputerScreenWindow { App = string.Empty },
                    new CMUComputerScreenWindow { App = "Files", Position = new Vector2(-50, 99999), Minimized = true },
                }));
                Assert.That(pc.Screen.Select(w => w.App), Is.EqualTo(new[] { "Browser", "Files" }),
                    "The screen did not keep exactly the valid windows, in order.");
                Assert.That(pc.Screen[0].Address, Is.EqualTo("intranet.wy/memo"));
                Assert.That(pc.Screen[0].Position, Is.EqualTo(Vector2.Zero), "A position that is not a number was kept.");
                Assert.That(pc.Screen[1].Position, Is.EqualTo(new Vector2(0, CMUComputerScreenLimits.Extent)), "A position off any screen was not clamped.");
                Assert.That(pc.Screen[1].Minimized, Is.True);

                Send(new CMUComputerScreenMessage(Enumerable.Range(0, 20).Select(i => Browser($"site{i}.net")).ToList()));
                Assert.That(pc.Screen, Has.Count.EqualTo(CMUComputerScreenLimits.Windows), "More windows were kept than the limit.");

                // Taking the card out ends the session: the next user starts on a clean screen.
                Send(new CMUComputerCardSlotMessage());
                Assert.That(pc.SessionUser, Is.Null);
                Assert.That(pc.Screen, Is.Empty, "Ending the session left its screen behind.");
                Send(new CMUComputerScreenMessage(new() { Browser("intranet.wy") }));
                Assert.That(pc.Screen, Is.Empty, "A terminal at its login screen kept a screen.");

                SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, card, computer, map.GridCoords));
                Send(new CMUComputerScreenMessage(new() { Browser("intranet.wy") }));
                Assert.That(pc.Screen, Has.Count.EqualTo(1), "A new session could not keep a screen.");
                Send(new CMUComputerPowerButtonMessage());
                Assert.That(pc.On, Is.False);
                Assert.That(pc.Screen, Is.Empty, "Switching off left the screen behind.");
            });
        }
        finally
        {
            await Server.WaitPost(() => cfg.SetCVar(CMUPersonalComputerCVars.NeedsPower, true));
        }
    }
}
