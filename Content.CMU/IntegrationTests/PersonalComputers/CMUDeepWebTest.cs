using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.PersonalComputers.DeepWeb;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUDeepWebSystem))]
public sealed class CMUDeepWebTest : GameTest
{
    [Test]
    [Description("Only a trained engineer gets the hack; a wrong carrier lock speeds the trace; locking the carrier and routing it to the core jacks the computer.")]
    public async Task EngineersHackComputersIntoTheDeep()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var skills = SEntMan.System<SkillsSystem>();
            var net = SEntMan.System<CMUExtraNetSystem>();

            var computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords);
            var tuner = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            var novice = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var engineer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(engineer, "RMCSkillEngineer", 3);
            var hackable = SEntMan.GetComponent<CMUDeepWebHackableComponent>(computer);

            // The tuner opens the multitool menu; picking "hack terminal" starts the hack.
            void Use(EntityUid user)
            {
                SEntMan.EventBus.RaiseLocalEvent(computer, new InteractUsingEvent(user, tuner, computer, map.GridCoords));
                var pick = new CMUComputerToolMessage(CMUComputerToolAction.Hack) { Actor = user, UiKey = CMUComputerToolUiKey.Key };
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) pick);
            }

            void Raise(EntityUid user, BoundUserInterfaceMessage message)
            {
                message.Actor = user;
                message.UiKey = CMUDeepHackUiKey.Key;
                SEntMan.EventBus.RaiseLocalEvent(computer, (object) message);
            }

            Use(novice);
            Assert.That(hackable.Board, Is.Null, "Someone without engineering skill started the hack.");

            Use(engineer);
            Assert.That(hackable.Board, Is.Not.Null, "A trained engineer could not start the hack.");
            var board = hackable.Board!;
            Assert.That(board.Stage, Is.EqualTo(CMUDeepHackStage.Tune), "A level 3 engineer should start by tuning.");
            Assert.That(CMUDeepHackPuzzle.Solved(board.Cells, board.Width, board.Height, board.SourceRow, board.CoreRow), Is.False,
                "The board came already solved.");

            var traceEnds = board.TraceEnds;
            var wrong = board.Frequency % CMUDeepHackPuzzle.MaxFrequency + 1;
            Raise(engineer, new CMUDeepHackLockMessage(wrong, board.Amplitude, board.Phase));
            Assert.Multiple(() =>
            {
                Assert.That(board.Stage, Is.EqualTo(CMUDeepHackStage.Tune), "A wrong carrier was accepted.");
                Assert.That(board.TraceEnds, Is.LessThan(traceEnds), "A wrong lock did not speed up the trace.");
            });

            Raise(engineer, new CMUDeepHackLockMessage(board.Frequency, board.Amplitude, board.Phase));
            Assert.That(board.Stage, Is.EqualTo(CMUDeepHackStage.Route), "The right carrier did not lock.");

            // Turning a tile as someone else does nothing.
            var turnable = board.Cells.FindIndex(c => !c.Locked && c.Tile != CMUDeepHackTile.Empty);
            var before = board.Cells[turnable].Rotation;
            Raise(novice, new CMUDeepHackRotateMessage(turnable));
            Assert.That(board.Cells[turnable].Rotation, Is.EqualTo(before), "Someone other than the hacker turned a tile.");

            // Turn every tile to the solution; the board may solve itself early through another route.
            for (var i = 0; i < board.Cells.Count && hackable.Board != null; i++)
            {
                for (var turns = 0; turns < 4 && hackable.Board != null && board.Cells[i].Rotation != board.Solution[i]; turns++)
                    Raise(engineer, new CMUDeepHackRotateMessage(i));
            }

            Assert.That(SEntMan.HasComponent<CMUDeepWebAccessComponent>(computer), "Routing the carrier to the core did not jack the computer.");

            var networks = net.GetNetworks((computer, SEntMan.GetComponent<CMUPersonalComputerComponent>(computer)));
            Assert.That(networks, Does.Contain(new Robust.Shared.Prototypes.ProtoId<CMUNetworkPrototype>("CMUNetDeep")));
        });
    }
}
