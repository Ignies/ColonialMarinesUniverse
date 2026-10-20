using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Shared._RMC14.Rules;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

/// <summary>
/// The console bridge: colony consoles run the PC OS with their own screen as a program, keep their access, and go back
/// to their own screen when admins switch the bridge off.
/// </summary>
[TestFixture]
public sealed class CMUComputerConsoleTest : GameTest
{
    private const string Terminal = "CMUResearchDataTerminal";
    private const string Secured = "AUDepartmentConsoleSecurity";

    private EntityUid Place(string prototype, EntityCoordinates at)
    {
        var console = SEntMan.SpawnEntity(prototype, at);
        SEntMan.System<SharedTransformSystem>().AnchorEntity(console);
        return console;
    }

    [Test]
    [Description("A colony console runs the PC with its own screen as a program; switching the bridge off gives it its screen back.")]
    public async Task BridgeComesAndGoes()
    {
        var map = await Pair.CreateTestMap();
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            var bridge = SEntMan.System<CMUComputerBridgeServerSystem>();
            var ui = SEntMan.System<UserInterfaceSystem>();
            var console = Place(Terminal, map.GridCoords);
            var original = SEntMan.GetComponent<ActivatableUIComponent>(console).Key;

            bridge.Refresh();
            Assert.That(SEntMan.HasComponent<CMUPersonalComputerComponent>(console), "The console was not bridged.");
            Assert.That(SEntMan.GetComponent<ActivatableUIComponent>(console).Key, Is.EqualTo(CMUPersonalComputerUiKey.Key), "Using the console does not open the PC.");
            Assert.That(ui.HasUi(console, CMUPersonalComputerUiKey.Key), "The console has no PC interface.");
            var apps = SEntMan.GetComponent<CMUComputerConsoleComponent>(console).Apps;
            Assert.That(apps.Count == 1 && Equals(apps[0].Key, original), "The console's own screen is not its program.");

            cfg.SetCVar(CMUPersonalComputerCVars.Bridge, false);
            try
            {
                bridge.Refresh();
                Assert.That(SEntMan.HasComponent<CMUPersonalComputerComponent>(console), Is.False, "The console stayed a PC with the bridge off.");
                Assert.That(SEntMan.GetComponent<ActivatableUIComponent>(console).Key, Is.EqualTo(original), "The console did not get its own screen back.");
            }
            finally
            {
                cfg.SetCVar(CMUPersonalComputerCVars.Bridge, true);
            }
        });
    }

    [Test]
    [Description("A console program needs the machine on and a card in, then opens the console's own window.")]
    public async Task ProgramsOpenTheConsole()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            var ui = SEntMan.System<UserInterfaceSystem>();
            var console = Place(Terminal, map.GridCoords);
            SEntMan.System<CMUComputerBridgeServerSystem>().Refresh();
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(console);
            var key = SEntMan.GetComponent<CMUComputerConsoleComponent>(console).Apps[0].Key;
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            void Open()
            {
                var message = new CMUComputerOpenConsoleMessage(0) { Actor = user, UiKey = CMUPersonalComputerUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(console, (object) message);
            }

            Open();
            Assert.That(ui.IsUiOpen(console, key, user), Is.False, "A switched off console opened its program.");

            pc.On = true;
            Open();
            Assert.That(ui.IsUiOpen(console, key, user), Is.False, "A console opened its program with nobody logged in.");

            var card = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
            SEntMan.GetComponent<IdCardComponent>(card).FullName = "Console User";
            SEntMan.EventBus.RaiseLocalEvent(console, new InteractUsingEvent(user, card, console, map.GridCoords));
            Open();
            Assert.That(ui.IsUiOpen(console, key, user), "The console program did not open the console's window.");
        });
    }

    [Test]
    [Description("A card without the console's access cannot log in to it, hacked or not.")]
    public async Task ConsolesKeepTheirAccess()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            var console = Place(Secured, map.GridCoords);
            SEntMan.System<CMUComputerBridgeServerSystem>().Refresh();
            Assert.That(SEntMan.HasComponent<AccessReaderComponent>(console), "The test console has no access reader.");
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(console);
            pc.On = true;
            SEntMan.EnsureComponent<Content.Shared.CMU14.PersonalComputers.DeepWeb.CMUDeepWebAccessComponent>(console);

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var card = SEntMan.SpawnEntity("CMIDCardGold", map.GridCoords);
            SEntMan.GetComponent<IdCardComponent>(card).FullName = "No Clearance";
            SEntMan.GetComponent<AccessComponent>(card).Tags.Clear();
            SEntMan.EventBus.RaiseLocalEvent(console, new InteractUsingEvent(user, card, console, map.GridCoords));
            Assert.That(pc.SessionUser, Is.Null, "A card with no access logged in to a secured console.");
            Assert.That(pc.CardPrototype, Is.Null, "The refused card stayed in the reader.");
        });
    }

    [Test]
    [Description("A plain desk PC opens when a player uses it, as any console does, powered or not.")]
    public async Task DesksOpen()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            var ui = SEntMan.System<UserInterfaceSystem>();
            foreach (var desk in new[] { "RMCPersonalDesktop", "CMUPersonalDesktopWeyland", "CMUPersonalDesktopSecurity" })
            {
                var computer = Place(desk, map.GridCoords);
                SEntMan.System<CMUComputerBridgeServerSystem>().Refresh();
                Assert.That(SEntMan.HasComponent<CMUComputerConsoleComponent>(computer), Is.False, $"The bridge took over the desk {desk}.");
                var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                // Through the engine's own open message, as a player's click goes, so the PC's message gate is crossed.
                var open = new OpenBoundInterfaceMessage { Actor = user, UiKey = CMUPersonalComputerUiKey.Key, Entity = SEntMan.GetNetEntity(computer) };
                var attempt = new BoundUserInterfaceMessageAttempt(user, computer, CMUPersonalComputerUiKey.Key, open);
                SEntMan.EventBus.RaiseLocalEvent(computer, attempt);
                Assert.That(attempt.Cancelled, Is.False, $"The switched off {desk} refused to open.");
                SEntMan.System<ActivatableUISystem>().InteractUI(user, computer);
                Assert.That(ui.IsUiOpen(computer, CMUPersonalComputerUiKey.Key, user), $"Using {desk} did not open it.");
                SEntMan.DeleteEntity(user);
                SEntMan.DeleteEntity(computer);
            }
        });
    }
}
