using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// The gun jeep's grenade launcher and rocket pod swap onto its pedestal in the machine gun's place.
/// The launcher won't fire at anything so close its burst would reach the jeep; the pod's backblast
/// knocks down and scorches whoever stands behind it.
/// </summary>
[TestFixture]
public sealed class CMUJeepGunsTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Jeep, EntityUid Gun)> GunJeep(string gun)
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -10; x <= 10; x++)
            {
                for (var y = -10; y <= 10; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            // Unrotated, the jeep faces south: its front is -y.
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);

        EntityUid fitted = default;
        await Server.WaitPost(() =>
        {
            // Swap the machine gun on the pedestal mount for the other gun.
            var containers = SEntMan.System<SharedContainerSystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(containers.TryGetContainer(jeep, "turret", out var turret), Is.True);
            var mount = turret!.ContainedEntities.Single();
            Assert.That(containers.TryGetContainer(mount, "turret-gun", out var gunSlot), Is.True);
            foreach (var old in gunSlot!.ContainedEntities.ToArray())
            {
                SEntMan.DeleteEntity(old);
            }

            fitted = SEntMan.SpawnEntity(gun, MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(mount, "turret-gun", fitted, null), Is.True,
                $"The {gun} should fit the gun jeep's pedestal mount.");
        });
        await RunTicksSync(5);
        return (map, jeep, fitted);
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

    [Test]
    public async Task TheGrenadeLauncherWontFireAtItsOwnFeet()
    {
        var (map, _, launcher) = await GunJeep("CMUJeepGrenadeLauncher");
        await Server.WaitAssertion(() =>
        {
            var gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            var full = Ammo(launcher);
            Assert.That(full, Is.GreaterThan(0), "The grenade launcher should come loaded.");

            Assert.That(TryShoot(launcher, gunner, new EntityCoordinates(map.Grid, 0.5f, -1f)), Is.False,
                "It shouldn't lob a round right in front of the jeep.");
            Assert.That(Ammo(launcher), Is.EqualTo(full));

            Assert.That(TryShoot(launcher, gunner, new EntityCoordinates(map.Grid, 0.5f, -6.5f)), Is.True,
                "It should lob a round well clear of the jeep.");
            Assert.That(Ammo(launcher), Is.LessThan(full));
        });
    }

    [Test]
    public async Task TheRocketPodsBackblastThrowsDownWhoeverIsBehindIt()
    {
        var (map, _, pod) = await GunJeep("CMUJeepRocketPod");
        EntityUid behind = default;
        EntityUid aside = default;
        await Server.WaitAssertion(() =>
        {
            var gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            var full = Ammo(pod);
            Assert.That(full, Is.GreaterThan(0), "The rocket pod should come loaded.");

            // Aiming forward, the blast throws out behind the pedestal, over the back of the jeep.
            behind = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
            aside = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 3.5f, 0.5f));
            Assert.That(TryShoot(pod, gunner, new EntityCoordinates(map.Grid, 0.5f, -7.5f)), Is.True,
                "Someone behind it shouldn't stop it firing.");
            Assert.That(Ammo(pod), Is.LessThan(full));
        });
        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(behind), Is.True,
                "The backblast should knock down whoever stands behind the pod.");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(behind).Float(), Is.GreaterThan(0f),
                "The backblast should scorch whoever stands behind the pod.");
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(aside), Is.False,
                "Someone off to the side is clear of the backblast.");
        });
    }

    [Test]
    public async Task TheBackblastSparesTheJeepsRiders()
    {
        var (map, jeep, pod) = await GunJeep("CMUJeepRocketPod");
        EntityUid rider = default;
        await Server.WaitPost(() =>
        {
            // By the passenger's door; climbing in over it takes a moment.
            rider = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, -0.45f, 0.75f));
            var seats = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities;
            var passenger = seats.First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s) is { Driver: false, Gunner: false });
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(passenger, rider);
        });
        await RunSeconds(4f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(rider).Buckled, Is.True, "The rider should be seated.");

            // A blast thrown straight back at the rider's seat from a pace in front of it.
            var guns = SEntMan.System<CMUJeepGunSystem>();
            var xforms = SEntMan.System<SharedTransformSystem>();
            var seat = xforms.GetMapCoordinates(rider);
            var muzzle = seat.Offset(new System.Numerics.Vector2(0f, -1f));
            var aim = muzzle.Position - seat.Position;
            var blast = (pod, SEntMan.GetComponent<CMUGunBackblastComponent>(pod));
            var caught = new List<EntityUid>();

            guns.InBackblast(blast, muzzle, aim, EntityUid.Invalid, null, caught);
            Assert.That(caught, Does.Contain(rider), "Out of the jeep, the rider would stand in the blast.");

            guns.InBackblast(blast, muzzle, aim, EntityUid.Invalid, jeep, caught);
            Assert.That(caught, Does.Not.Contain(rider), "The jeep's bodywork should shield its rider.");
        });
    }

    [Test]
    public async Task TheRocketPodsRocketIsNotAnAntiArmorRound()
    {
        await Server.WaitAssertion(() =>
        {
            var rocket = SProtoMan.Index<EntityPrototype>("CMUJeepProjectileRocket");
            Assert.That(rocket.Components.ContainsKey("TankCookOffProjectile"), Is.False,
                "The jeep's light rocket shouldn't carry the TOW's cook-off and hull damage.");
        });
    }

    [Test]
    public async Task EachGunIsDrawnInItsOwnArt()
    {
        var (_, jeep, launcher) = await GunJeep("CMUJeepGrenadeLauncher");
        await Server.WaitAssertion(() =>
        {
            // What the turret overlay and the maintenance preview draw for the pedestal.
            var guns = SEntMan.System<CMUJeepGunSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var mount = containers.GetContainer(jeep, "turret").ContainedEntities.Single();
            Assert.That(guns.Art(mount)?.Comp.State, Is.EqualTo("glturret_0"),
                "The pedestal should be drawn with the grenade launcher on it.");

            containers.Remove(launcher, containers.GetContainer(mount, "turret-gun"));
            Assert.That(guns.Art(mount)?.Comp.State, Is.EqualTo("pedestal_0"),
                "With its gun off, the pedestal should be drawn bare.");
        });

        var (_, _, pod) = await GunJeep("CMUJeepRocketPod");
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUVehicleTurretArtComponent>(pod).State, Is.EqualTo("rlturret_0")));
    }
}
