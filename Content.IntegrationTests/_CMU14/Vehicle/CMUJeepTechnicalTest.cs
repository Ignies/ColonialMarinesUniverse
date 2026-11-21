using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A technical frame loaded into a cargo jeep's bed and wrenched down brings its gun mount into play
/// and opens the gunner's place in the bed; wrenched loose, the mount goes back and the gunner gets
/// down. A pintle adapter fires the gun clamped in it, once it's screwed down, as true as wielded.
/// </summary>
[TestFixture]
public sealed class CMUJeepTechnicalTest : GameTest
{
    private const string Rifle = "RMCWeaponRifleL24";

    private async Task<TestMapData> OpenGround()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }
        });
        return map;
    }

    private void Click(EntityUid user, EntityUid target)
    {
        SEntMan.System<SharedInteractionSystem>()
            .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);
    }

    private EntityUid GunnerSeat(EntityUid jeep)
    {
        return SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
            .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Gunner);
    }

    private EntityUid? Turret(EntityUid jeep)
    {
        return SEntMan.System<ItemSlotsSystem>().GetItemOrNull(jeep, "turret");
    }

    [Test]
    public async Task AWrenchedDownFrameMakesTheCargoJeepATechnical()
    {
        var map = await OpenGround();
        EntityUid jeep = default;
        EntityUid frame = default;
        EntityUid user = default;
        EntityUid gunner = default;
        await Server.WaitPost(() =>
        {
            // The jeep faces south, so its bed is to the north; the user stands right behind it.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            frame = SEntMan.SpawnEntity("CMUJeepTechnicalFrame", new EntityCoordinates(map.Grid, 1.6f, 3.1f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.4f));
            gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, -0.5f, 2.4f));
            SEntMan.System<CMUOpenVehicleSystem>()
                .SetPanel((jeep, SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep)),
                    CMUOpenVehicleSystem.Tailgate,
                    true);
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<PullingSystem>().TryStartPull(user, frame), Is.True);
        });
        await RunTicksSync(5);
        await Server.WaitPost(() => Click(user, jeep));
        await RunSeconds(3);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Crate, Is.EqualTo(frame),
                "The frame should load into the bed like a crate.");
            Assert.That(Turret(jeep), Is.Null, "A loose frame's mount stays in the frame.");

            // Nothing to man yet.
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(GunnerSeat(jeep), gunner);
        });
        await RunSeconds(3);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.False,
                "The gunner's place should stay shut until the frame is wrenched down.");
            var wrench = SEntMan.SpawnEntity("CMWrench", SEntMan.GetComponent<TransformComponent>(user).Coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, wrench), Is.True);
            Click(user, jeep);
        });
        await RunSeconds(3);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Secured, Is.True);
            var fitted = Turret(jeep) is { } mount
                ? SEntMan.GetComponent<MetaDataComponent>(mount).EntityPrototype?.ID
                : null;
            Assert.That(fitted, Is.EqualTo("CMUJeepTechnicalMount"),
                "Wrenched down, the frame's mount should be the jeep's gun mount.");
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(GunnerSeat(jeep), gunner);
        });
        await RunSeconds(4);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.True,
                "With the mount up, the gunner should get into the bed.");
            Click(user, jeep);
        });
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Secured, Is.False);
            Assert.That(Turret(jeep), Is.Null, "Wrenched loose, the mount should leave the jeep.");
            var stowed = SEntMan.System<SharedContainerSystem>().GetContainer(frame, "technical-mount");
            Assert.That(stowed.ContainedEntities,
                Has.Count.EqualTo(1), "The mount should go back into its frame.");
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.False,
                "With the mount gone, the gunner should get down.");
        });
    }

    [Test]
    public async Task ACradleFiresItsGunOnceItsScrewedDown()
    {
        var map = await OpenGround();
        EntityUid jeep = default;
        EntityUid cradle = default;
        EntityUid rifle = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            // Unrotated, the gun jeep faces south: its front is -y.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            cradle = SEntMan.SpawnEntity("CMUJeepGunCradle", new EntityCoordinates(map.Grid, 3.5f, 0.5f));
            rifle = SEntMan.SpawnEntity(Rifle, MapCoordinates.Nullspace);
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 3.5f, 1.5f));
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(cradle, "cradle-gun", rifle, null), Is.True,
                "A rifle should fit the adapter.");
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var at = SEntMan.GetComponent<TransformComponent>(user).Coordinates;
            var screwdriver = SEntMan.SpawnEntity("CMScrewdriver", at);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, screwdriver), Is.True);
            Click(user, cradle);
        });
        await RunSeconds(4);

        int full = 0;
        await Server.WaitPost(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(cradle, "cradle-gun", out var slot) && slot.Locked, Is.True,
                "A screwdriver should screw the gun down in the adapter.");
            Assert.That(slots.TryEject(cradle, "cradle-gun", user, out _), Is.False,
                "A screwed down gun shouldn't come out.");

            // Loosened again, then fitted to the gun jeep's pedestal in the machine gun's place.
            slots.SetLock(cradle, "cradle-gun", false);
            var mount = SEntMan.System<SharedContainerSystem>().GetContainer(jeep, "turret").ContainedEntities.Single();
            var gunSlot = SEntMan.System<SharedContainerSystem>().GetContainer(mount, "turret-gun");
            foreach (var old in gunSlot.ContainedEntities.ToArray())
            {
                SEntMan.DeleteEntity(old);
            }

            Assert.That(slots.TryInsert(mount, "turret-gun", cradle, null), Is.True,
                "The adapter should fit the gun jeep's pedestal.");

            full = Ammo(rifle);
            Assert.That(full, Is.GreaterThan(0), "The rifle should come loaded.");
            Assert.That(Ammo(cradle), Is.EqualTo(full), "The adapter should count its gun's rounds.");
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            var target = new EntityCoordinates(map.Grid, 0.5f, -6.5f);
            Assert.That(TryShoot(cradle, gunner, target), Is.False, "A loose gun shouldn't fire from the adapter.");

            SEntMan.System<ItemSlotsSystem>().SetLock(cradle, "cradle-gun", true);
            var guns = SEntMan.GetComponent<GunComponent>(cradle);
            var held = SEntMan.GetComponent<GunComponent>(rifle);
            Assert.That(guns.MaxAngleModified.Theta, Is.LessThan(held.MaxAngleModified.Theta),
                "The adapter should hold the gun steadier than it fires in hand.");
            Assert.That(guns.FireRateModified, Is.EqualTo(held.FireRateModified).Within(0.001f),
                "The adapter should fire at its gun's rate.");

            // Other modifiers still apply on top; the point is it isn't marked down as held one-handed.
            var accuracy = SEntMan.GetComponent<RMCWeaponAccuracyComponent>(rifle);
            var shooting = accuracy.ModifiedAccuracyMultiplier;
            var unwielded = accuracy.AccuracyMultiplierUnwielded;
            Assert.That(shooting.Float(), Is.GreaterThan(unwielded.Float()),
                "In the adapter the gun should shoot as true as wielded, not as held one-handed.");

            Assert.That(TryShoot(cradle, gunner, target), Is.True,
                "Screwed down, the gun should fire from the adapter.");
            Assert.That(Ammo(rifle), Is.LessThan(full), "The adapter's shots should come from its gun.");
        });
    }

    [Test]
    public async Task AMagazineUsedOnTheJeepReloadsTheCradledGun()
    {
        var map = await OpenGround();
        EntityUid jeep = default;
        EntityUid rifle = default;
        EntityUid user = default;
        EntityUid magazine = default;
        await Server.WaitPost(() =>
        {
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.6f, 0.5f));
            var cradle = SEntMan.SpawnEntity("CMUJeepGunCradle", MapCoordinates.Nullspace);
            rifle = SEntMan.SpawnEntity(Rifle, MapCoordinates.Nullspace);
            var slots = SEntMan.System<ItemSlotsSystem>();

            // An empty rifle, screwed down in an adapter on the gun jeep's pedestal.
            Assert.That(slots.TryEject(rifle, "gun_magazine", null, out var spent), Is.True);
            SEntMan.DeleteEntity(spent!.Value);
            Assert.That(slots.TryInsert(cradle, "cradle-gun", rifle, null), Is.True);
            slots.SetLock(cradle, "cradle-gun", true);
            var mount = SEntMan.System<SharedContainerSystem>().GetContainer(jeep, "turret").ContainedEntities.Single();
            var gunSlot = SEntMan.System<SharedContainerSystem>().GetContainer(mount, "turret-gun");
            foreach (var old in gunSlot.ContainedEntities.ToArray())
            {
                SEntMan.DeleteEntity(old);
            }

            Assert.That(slots.TryInsert(mount, "turret-gun", cradle, null), Is.True);

            var at = SEntMan.GetComponent<TransformComponent>(user).Coordinates;
            magazine = SEntMan.SpawnEntity("RMCMagazineRifleL24", at);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, magazine), Is.True);
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Assert.That(Ammo(rifle), Is.Zero, "The rifle should start empty.");
            Click(user, jeep);
        });
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(rifle, "gun_magazine"), Is.EqualTo(magazine),
                "A magazine used on the jeep should go into the cradled rifle.");
            Assert.That(Ammo(rifle), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task TheMountComesOffOnlyWithItsFrame()
    {
        var map = await OpenGround();
        EntityUid jeep = default;
        EntityUid frame = default;
        EntityUid droppedGun = default;
        await Server.WaitPost(() =>
        {
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            frame = SEntMan.SpawnEntity("CMUJeepTechnicalFrame", MapCoordinates.Nullspace);
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var cargo = SEntMan.System<CMUVehicleCargoSystem>();
            var bed = (jeep, SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep));
            cargo.Load(bed, frame);
            cargo.SetSecured(bed, true);
            Assert.That(Turret(jeep), Is.Not.Null, "Wrenched down, the frame's mount should be up.");
            // Maintenance takes hardpoints off by ejecting them, which asks the slot's owner and the
            // item first: the mount refuses for itself, not for the gun in its own slot.
            var slots = SEntMan.System<ItemSlotsSystem>();
            var mount = Turret(jeep)!.Value;
            var gun = SEntMan.SpawnEntity("CMUJeepMG", MapCoordinates.Nullspace);
            droppedGun = gun;
            Assert.That(slots.TryInsert(mount, "turret-gun", gun, null), Is.True,
                "A jeep gun should fit the technical's mount.");
            Assert.That(slots.TryGetSlot(jeep, "turret", out var turretSlot), Is.True);
            Assert.That(slots.TryGetSlot(mount, "turret-gun", out var gunSlot), Is.True);

            var takeMount = new ItemSlotEjectAttemptEvent(jeep, mount, null, turretSlot!);
            SEntMan.EventBus.RaiseLocalEvent(mount, ref takeMount);
            Assert.That(takeMount.Cancelled, Is.True, "The mount shouldn't come off but with its frame.");

            // Asked of the mount as the gun slot's owner, and of the gun itself, the mount lets its gun go.
            // Hardpoint maintenance's own hold on every hardpoint is taken off here, to ask only the mount.
            SEntMan.RemoveComponent<HardpointSlotsComponent>(mount);
            var takeGun = new ItemSlotEjectAttemptEvent(mount, gun, null, gunSlot!);
            SEntMan.EventBus.RaiseLocalEvent(mount, ref takeGun);
            SEntMan.EventBus.RaiseLocalEvent(gun, ref takeGun);
            Assert.That(takeGun.Cancelled, Is.False, "A gun on the technical's mount should come off as usual.");

            SEntMan.DeleteEntity(frame);
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Turret(jeep), Is.Null, "A frame destroyed while bolted down takes its mount with it.");
            Assert.That(SEntMan.EntityExists(droppedGun), Is.True, "The gun on the mount should fall clear.");
            Assert.That(SEntMan.System<SharedContainerSystem>().IsEntityInContainer(droppedGun), Is.False);
            Assert.That(SEntMan.GetComponent<CMUVehicleCargoComponent>(jeep).Crate, Is.Null);
        });
    }

    [Test]
    public async Task TheAdapterKeepsItsGunsOwnChecks()
    {
        var map = await OpenGround();
        EntityUid jeep = default;
        EntityUid cradle = default;
        await Server.WaitPost(() =>
        {
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            cradle = SEntMan.SpawnEntity("CMUJeepGunCradle", MapCoordinates.Nullspace);
            var slots = SEntMan.System<ItemSlotsSystem>();
            // Fired only by those its user whitelist names.
            var sniper = SEntMan.SpawnEntity("RMCL112SniperRifle", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(cradle, "cradle-gun", sniper, null), Is.True);
            slots.SetLock(cradle, "cradle-gun", true);

            var mount = SEntMan.System<SharedContainerSystem>().GetContainer(jeep, "turret").ContainedEntities.Single();
            var gunSlot = SEntMan.System<SharedContainerSystem>().GetContainer(mount, "turret-gun");
            foreach (var old in gunSlot.ContainedEntities.ToArray())
            {
                SEntMan.DeleteEntity(old);
            }

            Assert.That(slots.TryInsert(mount, "turret-gun", cradle, null), Is.True);
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            Assert.That(TryShoot(cradle, gunner, new EntityCoordinates(map.Grid, 0.5f, -6.5f)), Is.False,
                "A specialist's rifle in the adapter should still only fire for its specialist.");
        });
    }

    [Test]
    public async Task TheAdapterRefusesLaunchersAndTheVulture()
    {
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            foreach (var gun in new[] { "RMCWeaponLauncherM5ATL", "RMCWeaponSniperM707Vulture" })
            {
                var cradle = SEntMan.SpawnEntity("CMUJeepGunCradle", MapCoordinates.Nullspace);
                var refused = SEntMan.SpawnEntity(gun, MapCoordinates.Nullspace);
                Assert.That(slots.TryInsert(cradle, "cradle-gun", refused, null), Is.False,
                    $"The {gun} shouldn't go in the adapter.");
            }
        });
    }

    private bool TryShoot(EntityUid gun, EntityUid user, EntityCoordinates at)
    {
        var gunComp = SEntMan.GetComponent<GunComponent>(gun);
        return SEntMan.System<SharedGunSystem>().AttemptShoot(user, (gun, gunComp), at);
    }

    private int Ammo(EntityUid gun)
    {
        var ammo = new GetAmmoCountEvent();
        SEntMan.EventBus.RaiseLocalEvent(gun, ref ammo);
        return ammo.Count;
    }
}
