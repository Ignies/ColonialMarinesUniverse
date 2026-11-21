using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Rounds from a jeep's mounted gun pass its own seated riders by, whatever their factions, and still
/// hit whoever stands beyond. Riders' hand guns get no such pass: a passenger can shoot the driver.
/// </summary>
[TestFixture]
public sealed class CMUJeepCrewFireTest : GameTest
{
    [Test]
    public async Task MountedGunRoundsPassTheCrew()
    {
        var (map, jeep, driver, _) = await SeatedJeep("CMUVehicleJeepGunner", passenger: false);

        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            var gun = SEntMan.EntityQuery<RefillableByBulletBoxComponent>(true)
                .Select(c => c.Owner)
                .Single(uid => transform.GetParentUid(uid) is var parent
                               && (parent == jeep || transform.GetParentUid(parent) == jeep));

            // A round on a line straight through the driver's seat, to a target past the jeep.
            var driverPosition = transform.GetMapCoordinates(driver).Position;
            target = SEntMan.SpawnEntity("CMMobHuman", new MapCoordinates(driverPosition + new Vector2(2.5f, 0f),
                map.MapId));
            var round = SEntMan.SpawnEntity("VehicleBulletSentry10x50mm",
                new MapCoordinates(driverPosition - new Vector2(1.5f, 0f), map.MapId));
            SEntMan.System<SharedGunSystem>().ShootProjectile(round, Vector2.UnitX, Vector2.Zero, gun);
        });
        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(driver).Float(), Is.Zero,
                "The jeep's own mounted gun should not hit its driver.");
            Assert.That(damageable.GetTotalDamage(target).Float(), Is.GreaterThan(0f),
                "The round should fly on past the crew to whoever stands beyond the jeep.");
        });
    }

    [Test]
    public async Task PassengerCanShootTheDriver()
    {
        var (_, _, driver, passenger) = await SeatedJeep("CMUVehicleJeepCargo", passenger: true);

        EntityUid pistol = default;
        await Server.WaitPost(() =>
        {
            pistol = SEntMan.SpawnEntity("RMCWeaponPistolB92FS",
                SEntMan.GetComponent<TransformComponent>(passenger).Coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(passenger, pistol), Is.True);
        });

        for (var i = 0; i < 10; i++)
        {
            await Server.WaitPost(() =>
            {
                var gun = new Entity<GunComponent>(pistol, SEntMan.GetComponent<GunComponent>(pistol));
                SEntMan.System<SharedGunSystem>().AttemptShoot(passenger, gun,
                    SEntMan.GetComponent<TransformComponent>(driver).Coordinates, driver);
            });
            await RunSeconds(0.5f);
        }

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(driver).Float(), Is.GreaterThan(0f),
                "A passenger's own gun should still hit the driver."));
    }

    private async Task<(TestMapData Map, EntityUid Jeep, EntityUid Driver, EntityUid Passenger)> SeatedJeep(
        string prototype,
        bool passenger)
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid driver = default;
        EntityUid rider = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            driver = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
            if (passenger)
                rider = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, -0.45f, 0.75f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            // The cargo jeep seats a driver and, beside them, a passenger; a technical also takes a gunner.
            var seats = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities;
            var seatSystem = SEntMan.System<CMUVehicleSeatSystem>();
            seatSystem.ClickSeat(seats.First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Driver), driver);
            if (passenger)
                seatSystem.ClickSeat(seats.First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s) is { Driver: false, Gunner: false }), rider);
        });
        await RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(driver).Buckled, Is.True, "The driver should be seated.");
            if (passenger)
                Assert.That(SEntMan.GetComponent<BuckleComponent>(rider).Buckled, Is.True,
                    "The passenger should be seated.");
        });

        return (map, jeep, driver, rider);
    }
}
