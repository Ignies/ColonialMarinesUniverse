using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Buckle.Components;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Jeep armor and mobility upgrades: an engineer bolts a kit on with a wrench, and nobody else can;
/// armored parts take the place of the stock ones; their weight slows the jeep past its free load, and
/// it takes none past its most.
/// </summary>
[TestFixture]
public sealed class CMUJeepUpgradeTest : GameTest
{
    private static readonly EntProtoId<SkillDefinitionComponent> Engineer = "RMCSkillEngineer";

    private async Task<(TestMapData Map, EntityUid Jeep)> Jeep(string prototype = "CMUVehicleJeepCargo")
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -5; x <= 5; x++)
            {
                for (var y = -5; y <= 5; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return (map, jeep);
    }

    private EntityUid Holding(TestMapData map, string item, bool engineer)
    {
        var user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.6f, 0.5f));
        if (engineer)
            SEntMan.System<SkillsSystem>().SetSkill(user, Engineer, 2);

        var held = SEntMan.SpawnEntity(item, SEntMan.GetComponent<TransformComponent>(user).Coordinates);
        Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, held), Is.True);
        return user;
    }

    private void Click(EntityUid user, EntityUid target)
    {
        SEntMan.System<SharedInteractionSystem>()
            .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);
    }

    private IReadOnlyList<EntityUid> Kits(EntityUid jeep)
    {
        return SEntMan.System<SharedContainerSystem>().GetContainer(jeep, "vehicle-upgrades").ContainedEntities;
    }

    private bool Fit(ItemSlotsSystem slots, EntityUid jeep, string slot, string part)
    {
        return slots.TryInsert(jeep, slot, SEntMan.SpawnEntity(part, MapCoordinates.Nullspace), null);
    }

    [Test]
    public async Task AnEngineerBoltsAKitOnAndNobodyElseCan()
    {
        var (map, jeep) = await Jeep();
        EntityUid rookie = default;
        EntityUid engineer = default;
        await Server.WaitPost(() =>
        {
            rookie = Holding(map, "CMUJeepBullBar", engineer: false);
            Click(rookie, jeep);
        });
        await RunSeconds(8);

        await Server.WaitPost(() =>
        {
            Assert.That(Kits(jeep), Is.Empty, "Only an engineer should be able to fit a kit.");
            engineer = Holding(map, "CMUJeepBullBar", engineer: true);
            Click(engineer, jeep);
        });
        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Kits(jeep), Has.Count.EqualTo(1), "An engineer should bolt the bull bar on.");
            var upgrades = SEntMan.System<CMUVehicleUpgradeSystem>();
            Assert.That(upgrades.CrashDamage(jeep), Is.LessThan(1f), "A bull bar should take some of a crash.");
        });
    }

    [Test]
    public async Task WeightSlowsTheJeepAndCapsWhatItTakes()
    {
        var (_, jeep) = await Jeep();
        await Server.WaitAssertion(() =>
        {
            var upgrades = SEntMan.System<CMUVehicleUpgradeSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var kits = containers.GetContainer(jeep, "vehicle-upgrades");
            Assert.That(upgrades.SpeedMultiplier(jeep), Is.EqualTo(1f), "A stock jeep shouldn't be slowed.");

            // Light kits ride free.
            containers.Insert(SEntMan.SpawnEntity("CMUJeepSnorkel", MapCoordinates.Nullspace), kits);
            containers.Insert(SEntMan.SpawnEntity("CMUJeepBullBar", MapCoordinates.Nullspace), kits);
            Assert.That(upgrades.SpeedMultiplier(jeep), Is.EqualTo(1f),
                "Within its free load the jeep shouldn't slow.");
            Assert.That(upgrades.WaterDrag(jeep), Is.LessThan(1f), "A snorkel should ease the water's drag.");

            // Past it, the jeep slows.
            containers.Insert(SEntMan.SpawnEntity("CMUJeepRollCage", MapCoordinates.Nullspace), kits);
            containers.Insert(SEntMan.SpawnEntity("CMUJeepSkidPlate", MapCoordinates.Nullspace), kits);
            var slowed = upgrades.SpeedMultiplier(jeep);
            Assert.That(slowed, Is.LessThan(1f), "Past its free load, the jeep should slow.");
            Assert.That(upgrades.IsCrashProof(jeep), Is.True,
                "A roll cage should keep the riders from being knocked out.");

            // Armored doors on both sides still fit; an armored tailgate on top would put it past its most.
            var slots = SEntMan.System<ItemSlotsSystem>();
            foreach (var slot in new[] { "jeep-door-driver", "jeep-door-passenger", "jeep-tailgate" })
            {
                foreach (var stock in containers.GetContainer(jeep, slot).ContainedEntities.ToArray())
                {
                    SEntMan.DeleteEntity(stock);
                }
            }

            Assert.That(Fit(slots, jeep, "jeep-door-driver", "CMUJeepDoorArmored"), Is.True,
                "An armored door should fit within the jeep's most.");
            Assert.That(upgrades.SpeedMultiplier(jeep), Is.LessThan(slowed),
                "Each armored part should slow the jeep further.");
            Assert.That(Fit(slots, jeep, "jeep-door-passenger", "CMUJeepDoorArmored"), Is.True,
                "A second armored door should still fit.");
            Assert.That(Fit(slots, jeep, "jeep-tailgate", "CMUJeepTailgateArmored"), Is.False,
                "A part past the jeep's most weight shouldn't go on.");
        });
    }

    [Test]
    public async Task ArmoredPartsAndRunFlatsFitTheStockSlots()
    {
        var (_, jeep) = await Jeep("CMUVehicleJeepGunner");
        await Server.WaitAssertion(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            var upgrades = SEntMan.System<CMUVehicleUpgradeSystem>();
            foreach (var (slot, part) in new[]
                     {
                         ("jeep-hood", "CMUJeepHoodArmored"),
                         ("jeep-windshield", "CMUJeepWindshieldBallistic"),
                         ("wheel-1", "CMUJeepWheelRunFlat"),
                     })
            {
                foreach (var stock in containers.GetContainer(jeep, slot).ContainedEntities.ToArray())
                {
                    SEntMan.DeleteEntity(stock);
                }

                Assert.That(Fit(slots, jeep, slot, part), Is.True,
                    $"The {part} should fit the jeep's {slot}.");
            }

            Assert.That(upgrades.EngineShare(jeep), Is.LessThan(1f),
                "Engine bay armor should keep hits off the engine.");

            // Raised, the hood shields nothing.
            SEntMan.System<CMUOpenVehicleSystem>()
                .SetPanel((jeep, SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep)), CMUOpenVehicleSystem.Hood, true);
            Assert.That(upgrades.EngineShare(jeep), Is.EqualTo(1f),
                "A raised armored hood shouldn't shield the engine.");
        });
    }

    [Test]
    public async Task OnlyAnEngineerTakesAKitOff()
    {
        var (map, jeep) = await Jeep();
        EntityUid rookie = default;
        EntityUid kit = default;
        await Server.WaitPost(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            kit = SEntMan.SpawnEntity("CMUJeepBullBar", MapCoordinates.Nullspace);
            containers.Insert(kit, containers.GetContainer(jeep, "vehicle-upgrades"));

            // The do-after a remove verb starts, run by someone who isn't an engineer.
            rookie = Holding(map, "CMWrench", engineer: false);
            var ev = new CMUVehicleKitDoAfterEvent(false, SEntMan.GetNetEntity(kit));
            var args = new DoAfterArgs(SEntMan, rookie, TimeSpan.FromSeconds(1), ev, jeep, jeep);
            Assert.That(SEntMan.System<SharedDoAfterSystem>().TryStartDoAfter(args), Is.True);
        });
        await RunSeconds(3f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Kits(jeep), Does.Contain(kit), "Only an engineer should get a kit off.");
        });
    }

    [Test]
    public async Task ArmoredDoorsAndTailgateShieldTheirRiders()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid driver = default;
        EntityUid rear = default;
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

            // The gun jeep: a driver behind the east door, the gunner in over the tailgate behind.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            driver = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
            rear = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.05f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var seats = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities;
            var seating = SEntMan.System<CMUVehicleSeatSystem>();
            seating.ClickSeat(seats.First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Driver), driver);
            seating.ClickSeat(seats.First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Gunner), rear);
        });
        await RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(driver).Buckled, Is.True);
            Assert.That(SEntMan.GetComponent<BuckleComponent>(rear).Buckled, Is.True);

            // Climbing in can leave a panel open; cover needs it shut.
            var open = SEntMan.System<CMUOpenVehicleSystem>();
            var parts = (jeep, SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep));
            open.SetPanel(parts, "door_driver", false);
            open.SetPanel(parts, CMUOpenVehicleSystem.Tailgate, false);

            var side = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 6.5f, 0.5f));
            var behind = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 7.5f));
            Assert.That(Stopped(driver, side, BodyPartType.Torso), Is.False,
                "A stock door shouldn't stop a shot at the driver's chest.");
            Assert.That(Stopped(rear, behind, BodyPartType.Leg), Is.False,
                "A stock tailgate shouldn't stop a shot at the gunner from behind.");

            Swap(jeep, "jeep-door-driver", "CMUJeepDoorArmored");
            Swap(jeep, "jeep-tailgate", "CMUJeepTailgateArmored");
            var door = SEntMan.GetComponent<CMUVehicleUpgradeComponent>(
                SEntMan.System<ItemSlotsSystem>().GetItemOrNull(jeep, "jeep-door-driver")!.Value);
            door.ChestChance = 1f;

            Assert.That(Stopped(driver, side, BodyPartType.Torso), Is.True,
                "An armored door should stop a shot at the driver's chest.");
            Assert.That(Stopped(rear, behind, BodyPartType.Leg), Is.True,
                "An armored tailgate should stop a shot at the gunner's legs from behind.");
        });
    }

    private EntityUid Swap(EntityUid jeep, string slot, string part)
    {
        var containers = SEntMan.System<SharedContainerSystem>();
        foreach (var stock in containers.GetContainer(jeep, slot).ContainedEntities.ToArray())
        {
            SEntMan.DeleteEntity(stock);
        }

        var fitted = SEntMan.SpawnEntity(part, MapCoordinates.Nullspace);
        Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(jeep, slot, fitted, null), Is.True);
        return fitted;
    }

    /// <summary>
    /// Whether the jeep's cover stops a bullet from <paramref name="shooter"/> at the rider.
    /// </summary>
    private bool Stopped(EntityUid rider, EntityUid shooter, BodyPartType part)
    {
        // Hit location would otherwise roll where the bullet lands; the test names the part.
        SEntMan.RemoveComponent<HitLocationComponent>(rider);
        var damage = new DamageSpecifier();
        damage.DamageDict["Piercing"] = 20;
        var impact = new DamageImpact(DamageImpactDelivery.Projectile, default, default, default);
        var ev = new BeforeDamageChangedEvent(damage, shooter, null, impact, TargetPart: part);
        SEntMan.EventBus.RaiseLocalEvent(rider, ref ev);
        return ev.Cancelled;
    }
}
