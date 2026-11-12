using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// The gun jeep's rounds, and its riders' own, pass the jeep's crew by while still hitting everyone
/// else. A crate of another round swaps the gun's belt, handing back what was left in it, and
/// armor-piercing rounds reach armored hulls that shrug off ball.
/// </summary>
[TestFixture]
public sealed class CMUJeepGunnerTest : GameTest
{
    private const string BallRound = "VehicleCartridgeSentry10x50mm";
    private const string ArmorPiercingRound = "CMUCartridgeCupolaAP";

    private async Task<(TestMapData Map, EntityUid Jeep)> SpawnJeep()
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
        return (map, jeep);
    }

    private EntityUid Gun(EntityUid jeep)
    {
        var transform = SEntMan.System<SharedTransformSystem>();
        return SEntMan.EntityQuery<RefillableByBulletBoxComponent>(true)
            .Select(c => c.Owner)
            .Single(uid => transform.GetParentUid(uid) is var parent &&
                           (parent == jeep || transform.GetParentUid(parent) == jeep));
    }

    [Test]
    public async Task ShotsFromAboardPassTheCrew()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid driver = default;
        EntityUid seat = default;
        await Server.WaitPost(() =>
        {
            driver = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
            seat = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
                .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Driver);
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, driver);
        });
        await RunSeconds(3f);

        EntityUid otherJeep = default;
        await Server.WaitPost(() =>
            otherJeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 5.5f, 0.5f)));
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(driver).BuckledTo, Is.EqualTo(seat), "The driver should be seated.");

            var seated = SEntMan.System<CMUVehicleSeatedSystem>();
            var shot = SEntMan.SpawnEntity("CMUBulletCupolaAP", MapCoordinates.Nullspace);
            var projectile = SEntMan.GetComponent<ProjectileComponent>(shot);

            projectile.Weapon = Gun(jeep);
            Assert.That(seated.IsShotFromOwnVehicle(driver, shot), Is.True,
                "A round from the jeep's own gun should pass its driver by.");

            projectile.Weapon = null;
            projectile.Shooter = driver;
            Assert.That(seated.IsShotFromOwnVehicle(driver, shot), Is.True,
                "A rider's own shot should pass the jeep's crew by.");

            projectile.Shooter = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, -3.5f, 0.5f));
            Assert.That(seated.IsShotFromOwnVehicle(driver, shot), Is.False,
                "A shot from someone off the jeep should still hit its crew.");

            projectile.Shooter = null;
            projectile.Weapon = Gun(otherJeep);
            Assert.That(seated.IsShotFromOwnVehicle(driver, shot), Is.False,
                "Another jeep's gun should still hit this jeep's crew.");
        });
    }

    [Test]
    public async Task AnotherRoundSwapsTheBelt()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid gun = default;
        EntityUid user = default;
        EntityUid box = default;
        var ballLeft = 0;
        await Server.WaitPost(() =>
        {
            gun = Gun(jeep);
            var ammo = SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun);
            Assert.That(ammo.Proto?.Id, Is.EqualTo(BallRound));
            ballLeft = ammo.Count;
            Assert.That(ballLeft, Is.GreaterThan(0), "The gun jeep should come loaded.");

            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, -0.3f));
            box = SEntMan.SpawnEntity("CMUAmmoBoxCupolaAP", new EntityCoordinates(map.Grid, 1.45f, -0.3f));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, box), Is.True);
            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(jeep).Coordinates, jeep);
        });
        await RunSeconds(5f);

        await Server.WaitAssertion(() =>
        {
            var ammo = SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun);
            var apBox = SEntMan.GetComponent<BulletBoxComponent>(box);
            Assert.That(ammo.Proto?.Id, Is.EqualTo(ArmorPiercingRound), "The gun should now fire armor-piercing rounds.");
            Assert.That(ammo.Count, Is.EqualTo(200), "The whole armor-piercing crate should feed in.");
            Assert.That(apBox.Amount, Is.Zero);

            var handedBack = SEntMan.EntityQuery<BulletBoxComponent>()
                .Where(b => b.Owner != box && b.AmmoProto == BallRound)
                .Sum(b => b.Amount);
            Assert.That(handedBack, Is.EqualTo(ballLeft), "The ball rounds left in the gun should come back in a crate.");
        });
    }

    [Test]
    public async Task ArmorPiercingWearsArmoredHulls()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ballTarget = default;
        EntityUid apTarget = default;
        await Server.WaitPost(() =>
        {
            ballTarget = SEntMan.SpawnEntity("VehicleAPC", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            apTarget = SEntMan.SpawnEntity("VehicleAPC", new EntityCoordinates(map.Grid, 12.5f, 0.5f));
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var ball = SEntMan.SpawnEntity("VehicleBulletSentry10x50mm", MapCoordinates.Nullspace);
            var ap = SEntMan.SpawnEntity("CMUBulletCupolaAP", MapCoordinates.Nullspace);

            var whitelist = SEntMan.System<EntityWhitelistSystem>();
            var hitList = SEntMan.GetComponent<RequireProjectileTargetComponent>(apTarget).AlwaysHitWhitelist;
            Assert.That(whitelist.IsWhitelistPass(hitList, ap), Is.True,
                "Armor-piercing rounds should hit an APC without it being aimed at.");
            Assert.That(whitelist.IsWhitelistPass(hitList, ball), Is.False,
                "Ball rounds should keep passing armored vehicles that aren't aimed at.");

            var ballLoss = HullLoss(ballTarget, ball, 20);
            var apLoss = HullLoss(apTarget, ap, 20);
            Assert.That(apLoss, Is.GreaterThan(ballLoss * 2),
                $"Armor-piercing rounds should wear an APC far more than ball ({apLoss} against {ballLoss}).");
        });
    }

    /// <summary>
    /// Integrity the vehicle and its fitted parts lose to the round's damage, dealt as that many hits.
    /// </summary>
    private float HullLoss(EntityUid vehicle, EntityUid round, int hits)
    {
        var damage = SEntMan.GetComponent<ProjectileComponent>(round).Damage;
        var before = Integrity(vehicle);
        for (var i = 0; i < hits; i++)
        {
            SEntMan.System<DamageableSystem>().TryChangeDamage(vehicle, damage, origin: round, impact: DamageImpact.Projectile);
        }

        return before - Integrity(vehicle);
    }

    private float Integrity(EntityUid vehicle)
    {
        var total = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle).Integrity;
        foreach (var part in SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
                     .Where(slot => slot.Item != null)
                     .Select(slot => slot.Item!.Value)
                     .Distinct())
        {
            if (SEntMan.TryGetComponent(part, out HardpointIntegrityComponent? integrity))
                total += integrity.Integrity;
        }

        return total;
    }
}
