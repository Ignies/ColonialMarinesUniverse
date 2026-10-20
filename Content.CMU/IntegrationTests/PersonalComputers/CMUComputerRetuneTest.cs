using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Interaction;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUComputerRetuneSystem))]
[TestOf(typeof(CMUSecurityNetSystem))]
public sealed class CMUComputerRetuneTest : GameTest
{
    private static readonly ProtoId<CMUNetSitePrototype> Dump = "CMUNetSiteDeepDump";
    private static readonly ProtoId<CMUNetworkPrototype> Public = "CMUNetPublic";

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

    /// <summary>Uses a multitool on the computer, which opens its menu, and picks "retune network".</summary>
    private void PickRetune(EntityUid computer, EntityUid user)
    {
        var coordinates = SEntMan.GetComponent<TransformComponent>(computer).Coordinates;
        var tuner = SEntMan.SpawnEntity("CMMultitool", coordinates);
        SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, tuner, computer, coordinates));
        Send(computer, user, new CMUComputerToolMessage(CMUComputerToolAction.Retune), CMUComputerToolUiKey.Key);
    }

    /// <summary>Picks "retune network" on the multitool menu, then locks the tuner onto the carrier it was given.</summary>
    private void Retune(EntityUid computer, EntityUid engineer)
    {
        PickRetune(computer, engineer);
        var board = SEntMan.GetComponent<CMUDeepWebHackableComponent>(computer).Board;
        Assert.That(board?.Retune, Is.True, "Picking retune did not open the tuner on a retune.");
        Send(computer, engineer, new CMUDeepHackLockMessage(board!.Frequency, board.Amplitude, board.Phase), CMUDeepHackUiKey.Key);
    }

    private EntityUid Engineer(EntityCoordinates at)
    {
        var engineer = SEntMan.SpawnEntity("CMMobHuman", at);
        SEntMan.System<SkillsSystem>().SetSkill(engineer, "RMCSkillEngineer", 3);
        return engineer;
    }

    /// <summary>A terminal that only lets in cards with an access a plain card does not have.</summary>
    private EntityUid Locked(EntityCoordinates at)
    {
        var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", at);
        var reader = SEntMan.EnsureComponent<AccessReaderComponent>(computer);
        SEntMan.System<AccessReaderSystem>().TryAddAccess((computer, reader), "AU14AccessColonyMarshall");
        SEntMan.GetComponent<CMUPersonalComputerComponent>(computer).On = true;
        return computer;
    }

    private void Swipe(EntityUid computer, EntityCoordinates at, string name)
    {
        var user = SEntMan.SpawnEntity("CMMobHuman", at);
        var card = SEntMan.SpawnEntity("CMIDCardGold", at);
        SEntMan.GetComponent<IdCardComponent>(card).FullName = name;
        SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, card, computer, at));
    }

    [Test]
    [Description("Retuning a terminal dark takes it off its networks and stops it reporting; security keeps it on the map, offline, where it went dark.")]
    public async Task DarkTerminalsStopReporting()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var net = SEntMan.System<CMUExtraNetSystem>();
            var security = SEntMan.System<CMUSecurityNetSystem>();
            var cmb = SEntMan.System<CMUCmbSystem>();
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);
            var engineer = Engineer(map.GridCoords);
            var novice = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            PickRetune(computer, novice);
            Assert.That(SEntMan.GetComponent<CMUDeepWebHackableComponent>(computer).Board, Is.Null, "Someone without engineering started a retune.");

            // A wrong lock leaves the terminal where it was.
            PickRetune(computer, engineer);
            var board = SEntMan.GetComponent<CMUDeepWebHackableComponent>(computer).Board!;
            Assert.That(board, Is.Not.Null, "A trained engineer could not start a retune.");
            var wrong = board.Frequency % CMUDeepHackPuzzle.MaxFrequency + 1;
            Send(computer, engineer, new CMUDeepHackLockMessage(wrong, board.Amplitude, board.Phase), CMUDeepHackUiKey.Key);
            Assert.That(pc.Dark, Is.False, "A wrong lock retuned the terminal.");

            var before = security.Entries.Count;
            Send(computer, engineer, new CMUDeepHackLockMessage(board.Frequency, board.Amplitude, board.Phase), CMUDeepHackUiKey.Key);
            Assert.That(pc.Dark, "Locking the carrier did not take the terminal dark.");
            Assert.That(net.GetNetworks((computer, pc)), Does.Not.Contain(Public), "A dark terminal still reaches the colony's networks.");
            Assert.That(security.Entries.Take(security.Entries.Count - before).Any(e => e.TerminalId == pc.TerminalId && e.Kind == CMUSecurityEvent.WentDark),
                "Security was not told the terminal went dark.");

            // From now on nothing it does is reported, though its own log still keeps it.
            var reported = security.Entries.Count;
            var alerts = cmb.Alerts.Count;
            var alias = SEntMan.System<CMUDeepAliasSystem>().GetAlias(SProtoMan.Index(Dump));
            Send(computer, engineer, new CMUNetVisitMessage(alias, false), CMUPersonalComputerUiKey.Key);
            Assert.Multiple(() =>
            {
                Assert.That(cmb.Alerts, Has.Count.EqualTo(alerts), "A dark terminal's deep lookup alerted the Marshals.");
                Assert.That(security.Entries, Has.Count.EqualTo(reported), "A dark terminal's deep lookup reached security.");
                Assert.That(pc.Log.Last().Text, Does.Contain(alias), "The dark terminal did not keep the visit in its own log.");
                Assert.That(cmb.StartTrace((computer, pc)), Is.False, "A dark terminal could be traced.");
            });

            // Moved after going dark, it still shows where it dropped off.
            var seenAt = SEntMan.GetComponent<TransformComponent>(computer).Coordinates.Position;
            SEntMan.System<SharedTransformSystem>().SetCoordinates(computer, map.GridCoords.Offset(new System.Numerics.Vector2(3, 0)));
            var pin = security.Terminals(map.Grid).Single(t => t.TerminalId == pc.TerminalId);
            Assert.That(pin.Status, Is.EqualTo(CMUSecurityStatus.Dark), "The security map does not show the terminal offline.");
            Assert.That(pin.Position, Is.EqualTo(seenAt), "The security map followed the dark terminal instead of keeping where it was last seen.");

            // Retuning back, within the engineer's reach, brings it onto the network and the map.
            SEntMan.System<SharedTransformSystem>().SetCoordinates(computer, map.GridCoords);
            Retune(computer, engineer);
            Assert.That(pc.Dark, Is.False, "Retuning again did not bring the terminal back.");
            Assert.That(net.GetNetworks((computer, pc)), Does.Contain(Public));
            Assert.That(security.Terminals(map.Grid).Single(t => t.TerminalId == pc.TerminalId).Status, Is.Not.EqualTo(CMUSecurityStatus.Dark));
        });
    }

    [Test]
    [Description("A card refused at a terminal's reader lands on the unauthorized use log with its name; at a dark terminal it does not.")]
    public async Task RefusedLoginsAreLogged()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var security = SEntMan.System<CMUSecurityNetSystem>();
            var computer = Locked(map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);

            Swipe(computer, map.GridCoords, "Ann Kowalski");
            Assert.That(pc.SessionUser, Is.Null, "A card without access logged in.");
            var entry = security.Entries.FirstOrDefault();
            Assert.That(entry?.Kind, Is.EqualTo(CMUSecurityEvent.LoginRefused), "The refused card was not logged for security.");
            Assert.That(entry?.TerminalId, Is.EqualTo(pc.TerminalId));
            Assert.That(entry?.Name, Is.EqualTo("Ann Kowalski"), "The log does not name the refused card.");

            SEntMan.System<CMUComputerRetuneSystem>().SetDark(computer, true);
            var count = security.Entries.Count;
            Swipe(computer, map.GridCoords, "Simon Hartley");
            Assert.That(security.Entries, Has.Count.EqualTo(count), "A dark terminal reported a refused card.");
        });
    }

    [Test]
    [Description("Switching retuning off removes the option and brings dark terminals back; switching the security network off stops its log.")]
    public async Task SwitchesTurnPartsOff()
    {
        var map = await Pair.CreateTestMap();
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitAssertion(() =>
        {
            var net = SEntMan.System<CMUExtraNetSystem>();
            var security = SEntMan.System<CMUSecurityNetSystem>();
            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var pc = SEntMan.GetComponent<CMUPersonalComputerComponent>(computer);
            SEntMan.System<CMUComputerRetuneSystem>().SetDark(computer, true);

            cfg.SetCVar(CMUPersonalComputerCVars.Retune, false);
            try
            {
                PickRetune(computer, Engineer(map.GridCoords));
                Assert.That(SEntMan.GetComponent<CMUDeepWebHackableComponent>(computer).Board, Is.Null, "A retune started with retuning switched off.");
                Assert.That(net.GetNetworks((computer, pc)), Does.Contain(Public), "A dark terminal stayed off the network with retuning switched off.");
            }
            finally
            {
                cfg.SetCVar(CMUPersonalComputerCVars.Retune, true);
            }

            var locked = Locked(map.GridCoords);
            cfg.SetCVar(CMUPersonalComputerCVars.SecurityNet, false);
            try
            {
                var count = security.Entries.Count;
                Swipe(locked, map.GridCoords, "Ann Kowalski");
                Assert.That(security.Entries, Has.Count.EqualTo(count), "The security network logged a refused card while switched off.");
            }
            finally
            {
                cfg.SetCVar(CMUPersonalComputerCVars.SecurityNet, true);
            }
        });
    }
}
