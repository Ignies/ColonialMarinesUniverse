using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Movement;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Ctrl+click the back of a jeep to brace against it, then walk into it: it creeps forward, slower
/// than walking, carrying its pusher along, and stops when they let go. Its sides can't be pushed.
/// </summary>
[TestFixture]
public sealed class CMUJeepPushTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Jeep)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -10; y <= 8; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            // Unrotated, the jeep faces south: its front is -y and its back +y.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return (map, jeep);
    }

    private void CtrlClick(EntityUid user, EntityUid jeep)
    {
        var ev = new CMUTryPullObjectEvent(user);
        SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
        Assert.That(ev.Handled, Is.True, "Ctrl+clicking a jeep should be used to brace against it, not to pull it.");
    }

    private void HoldKeys(EntityUid user, MoveButtons buttons)
    {
        var mover = SEntMan.EnsureComponent<InputMoverComponent>(user);
        var ev = new MoveInputEvent((user, mover), mover.HeldMoveButtons);
        mover.HeldMoveButtons = buttons;
        SEntMan.EventBus.RaiseLocalEvent(user, ref ev);
    }

    [Test]
    public async Task PushingFromBehindCreepsItForward()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid pusher = default;
        var start = Vector2.Zero;
        await Server.WaitPost(() =>
        {
            pusher = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.1f));
            start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(jeep);
            CtrlClick(pusher, jeep);
            var pushing = SEntMan.GetComponent<CMUVehiclePushingComponent>(pusher);
            Assert.That(pushing.Vehicle, Is.EqualTo(jeep));
            Assert.That(pushing.Front, Is.False, "Standing behind the jeep, they should brace against its back.");

            // Walking south, into the jeep's back.
            HoldKeys(pusher, MoveButtons.Down);
        });
        await RunSeconds(3f);

        var pushed = Vector2.Zero;
        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            pushed = transform.GetWorldPosition(jeep) - start;
            Assert.That(pushed.Y, Is.LessThan(-0.3f), "Pushed from behind, the jeep should roll forward.");
            Assert.That(pushed.Y, Is.GreaterThan(-2f), "A jeep pushed by hand should only creep, far slower than walking.");
            Assert.That(MathF.Abs(pushed.X), Is.LessThan(0.2f), "It should roll along its length, not sideways.");

            Assert.That(SEntMan.GetComponent<CMUVehiclePushingComponent>(pusher).Pushing, Is.True);
            var behind = transform.GetWorldPosition(pusher) - transform.GetWorldPosition(jeep);
            Assert.That(behind.Y, Is.InRange(1f, 1.6f), "The pusher should stay braced at the back as it rolls.");

            // Stepping away lets go.
            HoldKeys(pusher, MoveButtons.Up);
            Assert.That(SEntMan.GetComponent<CMUVehiclePushingComponent>(pusher).Vehicle, Is.Null);
        });

        // Let go, it rolls to a stop.
        await RunSeconds(2f);
        var stopped = Vector2.Zero;
        await Server.WaitPost(() => stopped = SEntMan.System<SharedTransformSystem>().GetWorldPosition(jeep));
        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That((SEntMan.System<SharedTransformSystem>().GetWorldPosition(jeep) - stopped).Length(), Is.LessThan(0.05f),
                "Once nobody pushes it, the jeep should stop."));
    }

    [Test]
    public async Task WithoutWheelsItWontBudge()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid pusher = default;
        var start = Vector2.Zero;
        await Server.WaitPost(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            foreach (var slot in SEntMan.GetComponent<VehicleWheelSlotsComponent>(jeep).Slots)
            {
                if (containers.TryGetContainer(jeep, slot, out var container))
                {
                    foreach (var wheel in container.ContainedEntities.ToArray())
                    {
                        SEntMan.DeleteEntity(wheel);
                    }
                }
            }

            pusher = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.1f));
            start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(jeep);
            CtrlClick(pusher, jeep);
            HoldKeys(pusher, MoveButtons.Down);
        });
        await RunSeconds(3f);

        await Server.WaitAssertion(() =>
            Assert.That((SEntMan.System<SharedTransformSystem>().GetWorldPosition(jeep) - start).Length(), Is.LessThan(0.05f),
                "A jeep on its bare hubs shouldn't roll when people push it."));
    }

    [Test]
    public async Task AXenoShovingBackKnocksPushersOff()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid pusher = default;
        await Server.WaitPost(() =>
        {
            pusher = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.1f));
            CtrlClick(pusher, jeep);
            HoldKeys(pusher, MoveButtons.Down);

            // A crusher against the front, shoving north: back against the pusher.
            var crusher = SEntMan.SpawnEntity("RMCXenoCrusher", new EntityCoordinates(map.Grid, 0.5f, -1.2f));
            HoldKeys(crusher, MoveButtons.Up);
        });
        await RunSeconds(2f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUVehiclePushingComponent>(pusher), Is.False,
                "A xeno shoving the jeep back should knock its pushers off it.");
            Assert.That(SEntMan.System<StandingStateSystem>().IsDown(pusher), Is.True,
                "Knocked off, the pusher should be off their feet.");
        });
    }

    [Test]
    public async Task ItsSidesCantBePushed()
    {
        var (map, jeep) = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var pusher = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.6f, 0.5f));
            CtrlClick(pusher, jeep);
            Assert.That(SEntMan.HasComponent<CMUVehiclePushingComponent>(pusher), Is.False,
                "A jeep's side shouldn't be pushable.");
        });
    }
}
