using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// The jeeps' utility kits: an auxiliary tank that grows the fuel tank, smoke dischargers that fire
/// and reload, a winch that hauls a crate up behind, a hitch that tows one, and a camo net.
/// </summary>
[TestFixture]
public sealed class CMUJeepUtilityTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Jeep)> Jeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
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

            // Unrotated, the jeep faces south: behind it is +y.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return (map, jeep);
    }

    private EntityUid Bolt(EntityUid jeep, string kit)
    {
        var containers = SEntMan.System<SharedContainerSystem>();
        var spawned = SEntMan.SpawnEntity(kit, MapCoordinates.Nullspace);
        Assert.That(containers.Insert(spawned, containers.GetContainer(jeep, "vehicle-upgrades")), Is.True);
        return spawned;
    }

    [Test]
    public async Task TheAuxTankHoldsMoreFuelAndSpillsItWhenTakenOff()
    {
        var (_, jeep) = await Jeep();
        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(jeep, "fuel", out var tank, out var fuel), Is.True);
            var stock = fuel!.MaxVolume;

            var aux = Bolt(jeep, "CMUJeepAuxTank");
            Assert.That(fuel.MaxVolume, Is.GreaterThan(stock), "The auxiliary tank should hold more fuel.");

            solutions.TryAddReagent(tank!.Value, "WeldingFuel", fuel.AvailableVolume, out _);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Remove(aux, containers.GetContainer(jeep, "vehicle-upgrades"));
            Assert.That(fuel.MaxVolume, Is.EqualTo(stock));
            Assert.That(fuel.Volume, Is.LessThanOrEqualTo(stock), "Fuel past the jeep's own tank should spill.");
        });
    }

    [Test]
    public async Task TheSmokeDischargersFireAndReload()
    {
        var (map, jeep) = await Jeep();
        await Server.WaitAssertion(() =>
        {
            var utility = SEntMan.System<CMUVehicleUtilitySystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.6f, 0.5f));
            var kit = Bolt(jeep, "CMUJeepSmokeDischargers");
            var smoke = SEntMan.GetComponent<CMUVehicleSmokeDischargerComponent>(kit);
            var full = smoke.Charges;

            Assert.That(utility.TryFireSmoke(jeep, user), Is.True);
            Assert.That(smoke.Charges, Is.EqualTo(full - 1));
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>()
                    .Count(m => m.EntityPrototype?.ID == "RMCSmoke"),
                Is.GreaterThan(0), "The dischargers should throw smoke around the jeep.");

            Assert.That(utility.TryFireSmoke(jeep, user), Is.False, "They shouldn't fire again straight away.");
        });
    }

    [Test]
    public async Task TheWinchHaulsACrateAndTheHitchTowsIt()
    {
        var (map, jeep) = await Jeep();
        EntityUid crate = default;
        await Server.WaitAssertion(() =>
        {
            var utility = SEntMan.System<CMUVehicleUtilitySystem>();
            var transforms = SEntMan.System<SharedTransformSystem>();
            Bolt(jeep, "CMUJeepWinch");
            Bolt(jeep, "CMUJeepTowHitch");
            crate = SEntMan.SpawnEntity("RMCCrateGreen", new EntityCoordinates(map.Grid, 3.5f, 4.5f));

            Assert.That(utility.TryWinch(jeep, crate), Is.True, "The winch should haul the crate in.");
            var behind = transforms.GetWorldPosition(crate) - transforms.GetWorldPosition(jeep);
            Assert.That(behind.Y, Is.GreaterThan(1.5f), "The crate should end up behind the jeep.");
            Assert.That(MathF.Abs(behind.X), Is.LessThan(0.5f));

            Assert.That(utility.TryHitch(jeep, crate), Is.True, "The hitch should take the crate in tow.");
            Assert.That(utility.TowSpeed(jeep), Is.LessThan(1f), "Towing should slow the jeep.");

            transforms.SetWorldPosition(jeep, transforms.GetWorldPosition(jeep) + new System.Numerics.Vector2(-3f, 0f));
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var transforms = SEntMan.System<SharedTransformSystem>();
            var behind = transforms.GetWorldPosition(crate) - transforms.GetWorldPosition(jeep);
            Assert.That(MathF.Abs(behind.X), Is.LessThan(0.5f), "The towed crate should follow the jeep.");
            Assert.That(behind.Y, Is.GreaterThan(1.5f));
        });
    }

    [Test]
    public async Task TheWinchTakesAWreckButNoAPC()
    {
        var (map, jeep) = await Jeep();
        await Server.WaitAssertion(() =>
        {
            var utility = SEntMan.System<CMUVehicleUtilitySystem>();
            Bolt(jeep, "CMUJeepWinch");
            Bolt(jeep, "CMUJeepTowHitch");

            var apc = SEntMan.SpawnEntity("CMUVehicleAPCWolfpack", new EntityCoordinates(map.Grid, 0.5f, 5f));
            Assert.That(utility.TryWinch(jeep, apc), Is.False, "An APC is far too heavy to winch.");
            Assert.That(utility.TryHitch(jeep, apc), Is.False, "An APC is far too heavy to tow.");
            SEntMan.DeleteEntity(apc);

            var wreck = SEntMan.SpawnEntity("CMUJeepWreckCargo", new EntityCoordinates(map.Grid, 3.5f, 4.5f));
            Assert.That(utility.TryWinch(jeep, wreck), Is.True, "A jeep's wreck should winch in.");
        });
    }

    [Test]
    public async Task TheCamoNetComesDownWhenTheJeepFires()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
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

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Bolt(jeep, "CMUJeepCamoNet");
            Assert.That(SEntMan.System<CMUVehicleUtilitySystem>().ToggleCamoNet(jeep), Is.True);

            // The machine gun on the pedestal fires.
            var containers = SEntMan.System<SharedContainerSystem>();
            var mount = containers.GetContainer(jeep, "turret").ContainedEntities.Single();
            var gun = containers.GetContainer(mount, "turret-gun").ContainedEntities.Single();
            var gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            var gunComp = SEntMan.GetComponent<GunComponent>(gun);
            Assert.That(SEntMan.System<SharedGunSystem>()
                .AttemptShoot(gunner, (gun, gunComp), new EntityCoordinates(map.Grid, 0.5f, -6.5f)), Is.True);
        });
        await RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUVehicleCamouflagedComponent>(jeep), Is.False,
                "Firing should give the jeep away.");
            Assert.That(SEntMan.HasComponent<EntityActiveInvisibleComponent>(jeep), Is.False);
        });
    }

    [Test]
    public async Task JeepsCantTowEachOtherInACircle()
    {
        var (map, jeep) = await Jeep();
        await Server.WaitAssertion(() =>
        {
            var utility = SEntMan.System<CMUVehicleUtilitySystem>();
            Bolt(jeep, "CMUJeepTowHitch");
            var other = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 3f));
            Bolt(other, "CMUJeepTowHitch");

            Assert.That(utility.TryHitch(jeep, other), Is.True, "A jeep should tow another nobody's driving.");
            Assert.That(utility.TryHitch(other, jeep), Is.False, "The towed jeep shouldn't tow its tower back.");

            var third = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 6f));
            Assert.That(utility.TryHitch(other, third), Is.False, "A jeep in tow shouldn't tow another.");

            // The tower deleted outright, its load is free to be towed again.
            SEntMan.DeleteEntity(jeep);
            Assert.That(SEntMan.HasComponent<CMUVehicleTowedComponent>(other), Is.False,
                "A deleted tower should let its load go.");
        });
    }

    [Test]
    public async Task TheRoofRackOnlyOpensBoltedOn()
    {
        var (map, jeep) = await Jeep();
        await Server.WaitAssertion(() =>
        {
            var storage = SEntMan.System<SharedStorageSystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.6f, 0.5f));
            var rack = SEntMan.SpawnEntity("CMUJeepRoofRack", new EntityCoordinates(map.Grid, 1.6f, 1.5f));
            var rackStorage = SEntMan.GetComponent<StorageComponent>(rack);
            Assert.That(storage.CanInteract(user, (rack, rackStorage)), Is.False,
                "A loose rack shouldn't open like a crate.");

            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(rack, containers.GetContainer(jeep, "vehicle-upgrades")), Is.True);
            Assert.That(storage.CanInteract(user, (rack, rackStorage)), Is.True,
                "Bolted on, the rack should open from the jeep.");
        });
    }

    [Test]
    public async Task TheCamoNetHidesTheParkedJeep()
    {
        var (_, jeep) = await Jeep();
        await Server.WaitAssertion(() =>
        {
            var utility = SEntMan.System<CMUVehicleUtilitySystem>();
            Bolt(jeep, "CMUJeepCamoNet");
            Assert.That(utility.ToggleCamoNet(jeep), Is.True);
            Assert.That(SEntMan.HasComponent<EntityActiveInvisibleComponent>(jeep), Is.True,
                "Spread, the net should hide the jeep.");
            Assert.That(utility.ToggleCamoNet(jeep), Is.True);
            Assert.That(SEntMan.HasComponent<EntityActiveInvisibleComponent>(jeep), Is.False,
                "Taken down, the jeep should show again.");
        });
    }
}
