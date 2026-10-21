using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.Interaction;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUComputerRelaySystem))]
public sealed class CMUComputerRelayTest : GameTest
{
    private static readonly ProtoId<CMUNetSitePrototype> Dump = "CMUNetSiteDeepDump";

    /// <summary>The test map has no power network, so terminals here run without power.</summary>
    [SetUp]
    public async Task NoPowerNeeded()
    {
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitPost(() => cfg.SetCVar(CMUPersonalComputerCVars.NeedsPower, false));
    }

    [TearDown]
    public async Task PowerNeededAgain()
    {
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitPost(() => cfg.SetCVar(CMUPersonalComputerCVars.NeedsPower, true));
    }

    private void Send(EntityUid computer, EntityUid actor, BoundUserInterfaceMessage message, Enum key)
    {
        message.Actor = actor;
        message.UiKey = key;
        SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
    }

    /// <summary>A switched-on public terminal, optionally with someone logged in for a trace to frame.</summary>
    private EntityUid Terminal(EntityCoordinates at, string? user = null)
    {
        var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", at);
        var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);
        pc.On = true;
        pc.SessionUser = user;
        return computer;
    }

    private EntityUid Insurgent(EntityCoordinates at)
    {
        var user = SEntMan.SpawnEntity("CMMobHuman", at);
        SEntMan.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 3);
        SEntMan.EnsureComponent<CLFMemberComponent>(user);
        return user;
    }

    private EntityUid Engineer(EntityCoordinates at)
    {
        var user = SEntMan.SpawnEntity("CMMobHuman", at);
        SEntMan.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 3);
        return user;
    }

    /// <summary>Uses a tuner on the terminal, picks "bug terminal", locks the carrier and routes it to solve the board.</summary>
    private void PlantBug(EntityUid computer, EntityUid user, EntityCoordinates at)
    {
        var tuner = SEntMan.SpawnEntity("CMMultitool", at);
        SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, tuner, computer, at));
        Send(computer, user, new CMUComputerToolMessage(CMUComputerToolAction.Bug), CMUComputerToolUiKey.Key);

        var hackable = SEntMan.GetComponent<CMUDeepWebHackableComponent>(computer);
        var board = hackable.Board;
        Assert.That(board?.Bug, Is.True, "Picking bug did not open a bug board.");

        if (board!.Stage == CMUDeepHackStage.Tune)
            Send(computer, user, new CMUDeepHackLockMessage(board.Frequency, board.Amplitude, board.Phase), CMUDeepHackUiKey.Key);

        // Turn every tile to its solution; the board may solve itself early through another route.
        for (var i = 0; i < board.Cells.Count && hackable.Board != null; i++)
        {
            for (var turns = 0; turns < 4 && hackable.Board != null && board.Cells[i].Rotation != board.Solution[i]; turns++)
                Send(computer, user, new CMUDeepHackRotateMessage(i), CMUDeepHackUiKey.Key);
        }
    }

    private void Format(EntityUid computer, EntityUid user, EntityCoordinates at)
    {
        var tuner = SEntMan.SpawnEntity("CMMultitool", at);
        SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, tuner, computer, at));
        Send(computer, user, new CMUComputerToolMessage(CMUComputerToolAction.Format), CMUComputerToolUiKey.Key);
    }

    [Test]
    [Description("An insurgent bugs a public terminal, routes their hacked terminal through it, and their deep lookups frame the relay's number, location and user, not their own terminal.")]
    public async Task RoutingFramesTheRelay()
    {
        var map = await Pair.CreateTestMap();
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitAssertion(() =>
        {
            var security = SEntMan.System<CMUSecurityNetSystem>();
            var cmb = SEntMan.System<CMUCmbSystem>();

            var victim = Terminal(map.GridCoords, "Dana Voss");
            var victimPc = SEntMan.GetComponent<CMUPersonalComputerComponent>(victim);
            var clf = Terminal(map.GridCoords);
            SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(clf);
            var clfPc = SEntMan.GetComponent<CMUPersonalComputerComponent>(clf);
            var insurgent = Insurgent(map.GridCoords);

            // A non-insurgent engineer cannot bug a terminal.
            var engineer = Engineer(map.GridCoords);
            var tuner = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(victim, new InteractUsingEvent(engineer, tuner, victim, map.GridCoords));
            Send(victim, engineer, new CMUComputerToolMessage(CMUComputerToolAction.Bug), CMUComputerToolUiKey.Key);
            Assert.That(SEntMan.GetComponent<CMUDeepWebHackableComponent>(victim).Board, Is.Null, "A non-insurgent started a bug.");

            // The insurgent bugs the public terminal into a relay, not a deep terminal.
            PlantBug(victim, insurgent, map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUComputerRelayComponent>(victim), "Solving the bug board did not install a relay.");
                Assert.That(SEntMan.HasComponent<CMUDeepWebAccessComponent>(victim), Is.False, "A bug jacked the terminal into the deep instead of relaying it.");
            });

            // The insurgent routes their own hacked terminal through the relay.
            Send(clf, insurgent, new CMURelayRouteMessage(SEntMan.GetNetEntity(victim)), CMUPersonalComputerUiKey.Key);
            Assert.That(SEntMan.GetComponent<CMUDeepWebAccessComponent>(clf).Relay, Is.EqualTo(SEntMan.GetNetEntity(victim)), "Routing through the relay did not take.");

            // A live deep lookup from the insurgent's terminal lands on the relay.
            var alias = SEntMan.System<CMUDeepAliasSystem>().GetAlias(SProtoMan.Index(Dump));
            var alertsBefore = cmb.Alerts.Count;
            Send(clf, insurgent, new CMUNetVisitMessage(alias, true), CMUPersonalComputerUiKey.Key);

            Assert.Multiple(() =>
            {
                Assert.That(cmb.Alerts, Has.Count.GreaterThan(alertsBefore), "The routed lookup raised no Marshal alert.");
                Assert.That(cmb.Alerts[0].TerminalId, Is.EqualTo(victimPc.TerminalId), "The alert blamed the wrong terminal.");
                Assert.That(cmb.Alerts.Any(a => a.TerminalId == clfPc.TerminalId && a.Address == alias), Is.False,
                    "The insurgent's own terminal was alerted.");

                var entry = security.Entries.FirstOrDefault(e => e.Kind == CMUSecurityEvent.DeepLookup);
                Assert.That(entry?.TerminalId, Is.EqualTo(victimPc.TerminalId), "Security blamed the wrong terminal.");
                Assert.That(entry?.Location, Is.EqualTo(security.Location(victim)), "Security was sent to the wrong place.");
                Assert.That(victimPc.Log.Any(l => l.Text.Contains(alias)), "The relay's own log did not take the routed lookup.");

                // A Marshal trace runs on the relay, where a colonist is logged in to be framed.
                Assert.That(cmb.StartTrace((victim, victimPc)), "The relay could not be traced.");
            });

            // With the relay feature switched off, routing carries nothing.
            cfg.SetCVar(CMUPersonalComputerCVars.Relay, false);
            try
            {
                var before = cmb.Alerts.Count;
                Send(clf, insurgent, new CMUNetVisitMessage(alias, true), CMUPersonalComputerUiKey.Key);
                Assert.That(cmb.Alerts, Has.Count.EqualTo(before), "A routed lookup alerted with the relay feature off.");
            }
            finally
            {
                cfg.SetCVar(CMUPersonalComputerCVars.Relay, true);
            }
        });
    }

    [Test]
    [Description("Only a trained engineer formats a bugged terminal, which clears the relay and the screen corruption; a clean terminal is left alone.")]
    public async Task FormatClearsTheBug()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var victim = Terminal(map.GridCoords);
            var insurgent = Insurgent(map.GridCoords);
            PlantBug(victim, insurgent, map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUComputerRelayComponent>(victim), "The bug did not install a relay to format.");

            // A novice with the tuner cannot format it.
            var novice = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Format(victim, novice, map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUComputerRelayComponent>(victim), "A novice formatted the terminal.");

            // An engineer formats it, clearing the bug.
            var engineer = Engineer(map.GridCoords);
            Format(victim, engineer, map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUComputerRelayComponent>(victim), Is.False, "Formatting did not clear the relay bug.");
        });
    }

    [Test]
    [Description("Planting through a relay drops an incriminating file into the bugged terminal's files, framed on whoever is logged in there, with a lead for the Marshals; a format wipes it.")]
    public async Task PlantingFramesTheColonist()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cmb = SEntMan.System<CMUCmbSystem>();

            var victim = Terminal(map.GridCoords, "Dana Voss");
            var victimPc = SEntMan.GetComponent<CMUPersonalComputerComponent>(victim);
            var clf = Terminal(map.GridCoords);
            SEntMan.EnsureComponent<CMUDeepWebAccessComponent>(clf);
            var insurgent = Insurgent(map.GridCoords);

            PlantBug(victim, insurgent, map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUComputerRelayComponent>(victim), "The bug did not install a relay.");

            var alertsBefore = cmb.Alerts.Count;
            Send(clf, insurgent, new CMURelayPlantMessage(SEntMan.GetNetEntity(victim)), CMUPersonalComputerUiKey.Key);

            Assert.Multiple(() =>
            {
                Assert.That(victimPc.Notes, Has.Count.EqualTo(1), "No file was planted in the terminal's files.");
                Assert.That(victimPc.Notes[0].Author, Is.EqualTo("Dana Voss"), "The planted file was not framed on the logged-in colonist.");
                Assert.That(victimPc.Log.Any(l => l.Text.Contains(victimPc.Notes[0].Name)), "The planted file left no trail in the log.");
                Assert.That(cmb.Alerts, Has.Count.GreaterThan(alertsBefore), "Planting raised no lead for the Marshals.");
                Assert.That(cmb.Alerts[0].TerminalId, Is.EqualTo(victimPc.TerminalId), "The lead pointed at the wrong terminal.");
            });

            // Formatting the terminal wipes the planted evidence along with the bug.
            var engineer = Engineer(map.GridCoords);
            Format(victim, engineer, map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUComputerRelayComponent>(victim), Is.False, "Format did not clear the bug.");
                Assert.That(victimPc.Notes, Is.Empty, "Format did not wipe the planted evidence.");
            });
        });
    }

    [Test]
    public void CorruptionKeepsTagsAndIsStable()
    {
        const string text = "[color=red]Weyland-Yutani[/color] bulletin board";
        Assert.That(CMUTextCorruption.Corrupt(text, 0f, 7), Is.EqualTo(text), "Zero corruption changed the text.");

        var once = CMUTextCorruption.Corrupt(text, 0.8f, 7);
        var twice = CMUTextCorruption.Corrupt(text, 0.8f, 7);
        Assert.Multiple(() =>
        {
            Assert.That(once, Has.Length.EqualTo(text.Length), "Corruption changed the text length.");
            Assert.That(once, Is.EqualTo(twice), "Corruption was not deterministic.");
            Assert.That(once, Does.Contain("color=red"), "Corruption mangled a markup tag.");
            Assert.That(once, Does.Contain("[/color]"), "Corruption mangled a closing tag.");
            Assert.That(once, Is.Not.EqualTo(text), "High corruption changed nothing.");
        });
    }
}
