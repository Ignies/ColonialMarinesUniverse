using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// An engineer's sentry on the gun jeep's sentry pintle fires by itself on whoever its rules make a
/// target: not on factions it holds fire on, unless they're armed and it's told to fire on them armed,
/// and on the closest or the biggest threat as set.
/// </summary>
[TestFixture]
public sealed class CMUJeepSentryMountTest : GameTest
{
    private static readonly string Bureau = "Bureau";
    private static readonly string WeYu = "AUWeYu";
    private static readonly string Provost = "PROVOST";
    private static readonly string Govfor = "GOVFOR";

    private async Task<(TestMapData Map, EntityUid Jeep, Entity<CMUJeepSentryMountComponent> Mount)> SentryJeep()
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

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);

        var mount = await FitSentry(jeep);
        return (map, jeep, mount);
    }

    private async Task<Entity<CMUJeepSentryMountComponent>> FitSentry(EntityUid jeep)
    {
        Entity<CMUJeepSentryMountComponent> mount = default;
        await Server.WaitPost(() =>
        {
            // Swap the gun jeep's machine gun mount for a sentry pintle and set a sentry on it.
            var containers = SEntMan.System<SharedContainerSystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(containers.TryGetContainer(jeep, "turret", out var turret), Is.True);
            foreach (var old in turret!.ContainedEntities.ToArray())
            {
                SEntMan.DeleteEntity(old);
            }

            var pintle = SEntMan.SpawnEntity("CMUJeepSentryMount", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(jeep, "turret", pintle, null), Is.True, "The sentry pintle should fit the gun jeep's pedestal.");
            var sentry = SEntMan.SpawnEntity("RMCSentry", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(pintle, "jeep-sentry", sentry, null), Is.True, "An engineer's sentry should sit on the pintle.");
            mount = (pintle, SEntMan.GetComponent<CMUJeepSentryMountComponent>(pintle));
        });
        await RunTicksSync(5);
        return mount;
    }

    private EntityUid GunnerSeat(EntityUid jeep)
    {
        return SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
            .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Gunner);
    }

    private EntityUid Person(TestMapData map, float x, float y, string? faction, bool armed)
    {
        var person = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, x, y));
        var factions = SEntMan.System<NpcFactionSystem>();
        factions.ClearFactions(person);
        if (faction != null)
            factions.AddFaction(person, faction);

        if (armed)
        {
            var gun = SEntMan.SpawnEntity("RMCWeaponPistolB92FS", SEntMan.GetComponent<TransformComponent>(person).Coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(person, gun), Is.True);
        }

        return person;
    }

    [Test]
    public async Task ArmedFriendliesCanBeFiredOn()
    {
        var (map, _, mount) = await SentryJeep();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<CMUJeepSentryMountSystem>();
            system.SetRules(mount, [Bureau, WeYu], [Bureau]);
            var sentry = system.GetSentry(mount)!.Value;

            var unarmedBureau = Person(map, 4.5f, 0.5f, Bureau, armed: false);
            var armedBureau = Person(map, 4.5f, 2.5f, Bureau, armed: true);
            var armedWeYu = Person(map, 4.5f, -1.5f, WeYu, armed: true);
            var stranger = Person(map, -3.5f, 0.5f, null, armed: false);

            Assert.Multiple(() =>
            {
                Assert.That(system.IsTarget(mount, sentry, unarmedBureau), Is.False, "It should hold fire on an unarmed friendly.");
                Assert.That(system.IsTarget(mount, sentry, armedBureau), Is.True, "It should fire on a friendly faction it's told to fire on once armed.");
                Assert.That(system.IsTarget(mount, sentry, armedWeYu), Is.False, "It should hold fire on an armed friendly it isn't told to fire on armed.");
                Assert.That(system.IsTarget(mount, sentry, stranger), Is.True, "It should fire on someone of no friendly faction.");
            });

            // Holding fire on no one but WeYu, a Bureau agent is a target armed or not.
            system.SetRules(mount, [WeYu], []);
            Assert.That(system.IsTarget(mount, sentry, unarmedBureau), Is.True);
        });
    }

    [Test]
    public async Task ItTurnsAllTheWayRoundToFire()
    {
        var (map, jeep, mount) = await SentryJeep();
        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []);
            // Behind the jeep: the machine gun's arc never reaches here.
            target = Person(map, 0.5f, 6.5f, null, armed: false);
        });
        await RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            var toTarget = transform.GetWorldPosition(target) - transform.GetWorldPosition(jeep);
            var facing = SEntMan.GetComponent<VehicleTurretComponent>(mount).WorldRotation;
            var off = Math.Abs(Angle.ShortestDistance(facing, toTarget.ToWorldAngle()).Degrees);
            Assert.That(off, Is.LessThan(30), "The sentry should have turned round to face its target behind the jeep.");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(target).Float(), Is.GreaterThan(0f),
                "The sentry should fire on a target behind the jeep.");
        });
    }

    [Test]
    public async Task TheSentryTakesTheGunnersSeat()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid gunner = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
        });
        await RunTicksSync(5);
        await Server.WaitPost(() => SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(GunnerSeat(jeep), gunner));
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).BuckledTo, Is.EqualTo(GunnerSeat(jeep)), "The gunner should sit at the machine gun."));

        await FitSentry(jeep);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.False, "Fitting a sentry should put the gunner out of their seat."));

        await Server.WaitPost(() => SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(GunnerSeat(jeep), gunner));
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.False, "With a sentry on the pintle, nobody should sit in the gunner's seat."));
    }

    [Test]
    public async Task OnlyTheKeyHolderSetsIt()
    {
        var (map, jeep, _) = await SentryJeep();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<CMUJeepSentryMountSystem>();
            var holder = Person(map, 2.5f, 0.5f, null, armed: false);
            var stranger = Person(map, 2.5f, 1.5f, null, armed: false);

            // The gun jeep comes with its key in; take it out into the holder's hands.
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryEjectToHands(jeep, SEntMan.GetComponent<CMUVehicleIgnitionComponent>(jeep).KeySlot, holder), Is.True);

            Assert.That(system.HoldsKey(holder, jeep), Is.True, "Holding the jeep's key, they should be able to set its sentry.");
            Assert.That(system.HoldsKey(stranger, jeep), Is.False, "Without the key, they shouldn't.");
        });
    }

    [Test]
    public async Task BiggestThreatOverClosest()
    {
        var (map, jeep, mount) = await SentryJeep();
        EntityUid near = default;
        EntityUid far = default;
        await Server.WaitPost(() =>
        {
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []);
            near = Person(map, 2.5f, 0.5f, null, armed: false);
            far = Person(map, 5.5f, 0.5f, null, armed: true);
        });
        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(mount.Comp.Target, Is.EqualTo(near), "On closest threat, it should track the nearer target."));

        await Server.WaitPost(() =>
            Send(mount, KeyHolder(map, mount), new CMUJeepSentryModeMessage(CMUJeepSentryTargetMode.Biggest)));
        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(mount.Comp.Target, Is.EqualTo(far), "On biggest threat, it should track the armed target over the unarmed one."));
    }

    [Test]
    public async Task ItFiresOnItsTarget()
    {
        var (map, _, mount) = await SentryJeep();
        EntityUid target = default;
        EntityUid friend = default;
        await Server.WaitPost(() =>
        {
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []);
            target = Person(map, 0.5f, -4.5f, null, armed: false);
            friend = Person(map, -3.5f, 0.5f, WeYu, armed: true);
        });
        await RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(target).Float(), Is.GreaterThan(0f), "The sentry should fire on its target.");
            Assert.That(damageable.GetTotalDamage(friend).Float(), Is.Zero, "The sentry should hold fire on a friendly.");
        });
    }

    /// <summary>
    /// Someone off to the side holding a copy of the jeep's key, so the key stays in its ignition.
    /// </summary>
    private EntityUid KeyHolder(TestMapData map, EntityUid mount)
    {
        Assert.That(SEntMan.System<VehicleTurretSystem>().TryGetVehicle(mount, out var jeep), Is.True);
        var holder = Person(map, -6.5f, -6.5f, WeYu, armed: false);
        var key = SEntMan.SpawnEntity("CMUJeepKey", SEntMan.GetComponent<TransformComponent>(holder).Coordinates);
        SEntMan.GetComponent<CMUVehicleKeyComponent>(key).Code = SEntMan.GetComponent<CMUVehicleIgnitionComponent>(jeep).KeyCode;
        Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(holder, key), Is.True);
        return holder;
    }

    /// <summary>
    /// The settings window's message, as its user sends it.
    /// </summary>
    private void Send(EntityUid mount, EntityUid actor, BoundUserInterfaceMessage message)
    {
        message.Actor = actor;
        message.UiKey = CMUJeepSentryUiKey.Key;
        SEntMan.EventBus.RaiseLocalEvent(mount, (object) message);
    }

    [Test]
    public async Task OnlyTheKeyHolderChangesItsSettings()
    {
        var (map, _, mount) = await SentryJeep();
        await Server.WaitAssertion(() =>
        {
            var holder = KeyHolder(map, mount);
            var stranger = Person(map, -6.5f, 6.5f, null, armed: false);

            Send(mount, stranger, new CMUJeepSentryFriendlyMessage("UPP", true));
            Send(mount, stranger, new CMUJeepSentryPowerMessage(false));
            Assert.That(mount.Comp.FriendlyFactions, Does.Not.Contain("UPP"), "Without the key, they shouldn't change who it holds fire on.");
            Assert.That(mount.Comp.Powered, Is.True, "Without the key, they shouldn't switch it off.");

            Send(mount, holder, new CMUJeepSentryFriendlyMessage("UPP", true));
            Send(mount, holder, new CMUJeepSentryArmedMessage("UPP", true));
            Send(mount, holder, new CMUJeepSentryModeMessage(CMUJeepSentryTargetMode.Biggest));
            Send(mount, holder, new CMUJeepSentryPowerMessage(false));
            Assert.Multiple(() =>
            {
                Assert.That(mount.Comp.FriendlyFactions, Does.Contain("UPP"));
                Assert.That(mount.Comp.ArmedHostileFactions, Does.Contain("UPP"));
                Assert.That(mount.Comp.Mode, Is.EqualTo(CMUJeepSentryTargetMode.Biggest));
                Assert.That(mount.Comp.Powered, Is.False);
            });

            // Holding fire on a faction no longer, it fires on them armed or not: the armed rule goes too.
            Send(mount, holder, new CMUJeepSentryFriendlyMessage("UPP", false));
            Assert.That(mount.Comp.ArmedHostileFactions, Does.Not.Contain("UPP"));
        });
    }

    [Test]
    public async Task ItsOwnRidersAreNeverItsTargets()
    {
        var (map, jeep, mount) = await SentryJeep();
        EntityUid passenger = default;
        await Server.WaitPost(() =>
        {
            // Unconfigured, it fires on no one while they climb in.
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [], []);
            passenger = Person(map, -0.5f, 0.75f, null, armed: true);
            var seat = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
                .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s) is { Driver: false, Gunner: false });
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, passenger);
        });
        await RunSeconds(4f);
        await Server.WaitPost(() => SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []));
        await RunSeconds(3f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(passenger).Buckled, Is.True, "The passenger should be seated.");
            Assert.That(mount.Comp.Target, Is.Null, "An armed stranger riding the jeep shouldn't become its sentry's target.");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(passenger).Float(), Is.Zero);
        });
    }

    [Test]
    public async Task FactionsNoSentryShootsStaySafeArmed()
    {
        var (map, _, mount) = await SentryJeep();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<CMUJeepSentryMountSystem>();
            // Every faction it holds fire on, it fires on once armed.
            system.SetRules(mount, [Bureau], [Bureau]);
            var sentry = system.GetSentry(mount)!.Value;

            var provost = Person(map, 4.5f, 0.5f, Provost, armed: true);
            var bureau = Person(map, 4.5f, 2.5f, Bureau, armed: true);
            Assert.That(system.IsTarget(mount, sentry, provost), Is.False, "No sentry should fire on the Provost, armed or not.");
            Assert.That(system.IsTarget(mount, sentry, bureau), Is.True);
        });
    }

    [Test]
    public async Task ItGoesQuietWithTheKeyOutOrItsPintleBroken()
    {
        var (map, jeep, mount) = await SentryJeep();
        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []);
            var holder = Person(map, -6.5f, -6.5f, WeYu, armed: false);
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryEjectToHands(jeep, SEntMan.GetComponent<CMUVehicleIgnitionComponent>(jeep).KeySlot, holder), Is.True);
            target = Person(map, 0.5f, -4.5f, null, armed: false);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Comp.Target, Is.Null, "With its key out, the jeep's sentry shouldn't run.");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(target).Float(), Is.Zero);
        });

        await Server.WaitPost(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            var key = SEntMan.EntityQuery<CMUVehicleKeyComponent>().Select(k => k.Owner).Single();
            Assert.That(slots.TryInsert(jeep, SEntMan.GetComponent<CMUVehicleIgnitionComponent>(jeep).KeySlot, key, null), Is.True);
            var hardpoints = SEntMan.System<HardpointSystem>();
            hardpoints.DamageHardpoint(jeep, mount, SEntMan.GetComponent<HardpointIntegrityComponent>(mount).Integrity);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Comp.Target, Is.Null, "With its pintle broken, the jeep's sentry shouldn't run.");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(target).Float(), Is.Zero);
        });
    }

    [Test]
    public async Task AMagazineOnTheJeepLoadsItsSentry()
    {
        var (map, jeep, mount) = await SentryJeep();
        EntityUid sentry = default;
        await Server.WaitPost(() =>
        {
            sentry = SEntMan.System<CMUJeepSentryMountSystem>().GetSentry(mount)!.Value;
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(sentry, "gun_magazine", out var magazineSlot), Is.True);
            foreach (var spent in magazineSlot!.ContainedEntities.ToArray())
            {
                SEntMan.DeleteEntity(spent);
            }

            var loader = Person(map, 1.5f, -0.5f, WeYu, armed: false);
            var magazine = SEntMan.SpawnEntity("RMCMagazineSentry", SEntMan.GetComponent<TransformComponent>(loader).Coordinates);
            var gun = SEntMan.SpawnEntity("RMCWeaponPistolB92FS", SEntMan.GetComponent<TransformComponent>(loader).Coordinates);
            var system = SEntMan.System<CMUJeepSentryMountSystem>();
            var at = SEntMan.GetComponent<TransformComponent>(jeep).Coordinates;
            Assert.That(system.TryLoadSentry(jeep, loader, gun, at), Is.False, "A gun isn't a sentry magazine.");
            Assert.That(system.TryLoadSentry(jeep, loader, magazine, at), Is.True, "A sentry magazine used on the jeep should load its sentry.");
        });
        await RunSeconds(8f);

        await Server.WaitAssertion(() =>
        {
            var ammo = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(sentry, ref ammo);
            Assert.That(ammo.Count, Is.GreaterThan(0), "The sentry should be loaded.");
        });
    }

    [Test]
    public async Task ALoadedPintleTakesTheGunnersSeat()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid gunner = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
        });
        await RunTicksSync(5);
        await Server.WaitPost(() => SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(GunnerSeat(jeep), gunner));
        await RunSeconds(3f);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.True);
            var containers = SEntMan.System<SharedContainerSystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(containers.TryGetContainer(jeep, "turret", out var turret), Is.True);
            foreach (var old in turret!.ContainedEntities.ToArray())
            {
                SEntMan.DeleteEntity(old);
            }

            // The sentry goes on the pintle first, and the pintle on the jeep after.
            var pintle = SEntMan.SpawnEntity("CMUJeepSentryMount", MapCoordinates.Nullspace);
            var sentry = SEntMan.SpawnEntity("RMCSentry", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(pintle, "jeep-sentry", sentry, null), Is.True);
            Assert.That(slots.TryInsert(jeep, "turret", pintle, null), Is.True);
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.False, "Fitting a loaded pintle should put the gunner out of their seat."));
    }

    [Test]
    public async Task ItsAlliesFollowItsSide()
    {
        var (map, _, mount) = await SentryJeep();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<CMUJeepSentryMountSystem>();
            var targeting = SEntMan.System<SharedSentryTargetingSystem>();
            system.SetRules(mount, [Govfor], []);
            var sentry = system.GetSentry(mount)!.Value;
            var agent = Person(map, 4.5f, 0.5f, Bureau, armed: false);
            Assert.That(system.IsTarget(mount, sentry, agent), Is.True);

            // Government forces ally with the Bureau: a government-side sentry holds fire on them.
            targeting.AddAllianceFriendlyFaction(Govfor, Bureau);
            Assert.That(system.IsTarget(mount, sentry, agent), Is.False, "It should hold fire on its side's allies.");

            // Switched to another side, the old side's allies are no longer its own.
            system.SetRules(mount, ["UPP"], []);
            Assert.That(system.IsTarget(mount, sentry, agent), Is.True, "Its old side's allies shouldn't stay its allies.");
        });
    }

    [Test]
    public async Task ItHoldsFireUntilItFacesItsTarget()
    {
        var (map, _, mount) = await SentryJeep();
        EntityUid sentry = default;
        var full = 0;
        await Server.WaitPost(() =>
        {
            sentry = SEntMan.System<CMUJeepSentryMountSystem>().GetSentry(mount)!.Value;
            full = Ammo(sentry);
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []);
            // Behind the jeep: half a turn away from where the pintle starts.
            Person(map, 0.5f, 6.5f, null, armed: false);
        });
        await RunSeconds(0.7f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Comp.Target, Is.Not.Null, "It should have picked its target.");
            Assert.That(Ammo(sentry), Is.EqualTo(full), "Still turning round, it shouldn't have fired yet.");
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(Ammo(sentry), Is.LessThan(full), "Facing its target, it should fire."));
    }

    [Test]
    public async Task ItKeepsItsTargetUnlessAnotherIsClearlyCloser()
    {
        var (map, _, mount) = await SentryJeep();
        EntityUid first = default;
        await Server.WaitPost(() =>
        {
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []);
            first = Person(map, 4.5f, 0.5f, null, armed: false);
        });
        await RunSeconds(1f);
        await Server.WaitAssertion(() => Assert.That(mount.Comp.Target, Is.EqualTo(first)));

        EntityUid much = default;
        await Server.WaitPost(() =>
        {
            // A little closer than the first: not enough to turn to.
            Person(map, 0.5f, -3f, null, armed: false);
        });
        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(mount.Comp.Target, Is.EqualTo(first), "It shouldn't turn to a target only a little closer."));

        await Server.WaitPost(() => much = Person(map, -1.5f, 0.5f, null, armed: false));
        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(mount.Comp.Target, Is.EqualTo(much), "It should turn to a target much closer."));
    }

    [Test]
    public async Task ItGoesQuietOnAWrittenOffJeep()
    {
        var (map, jeep, mount) = await SentryJeep();
        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            SEntMan.System<CMUJeepSentryMountSystem>().SetRules(mount, [WeYu], []);
            SEntMan.AddComponent<VehicleTotaledComponent>(jeep);
            target = Person(map, 0.5f, -4.5f, null, armed: false);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Comp.Target, Is.Null, "On a written-off jeep, the sentry shouldn't run.");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(target).Float(), Is.Zero);
        });
    }

    [Test]
    public async Task TakingTheSentryOffFreesTheGunnersSeat()
    {
        var (map, jeep, mount) = await SentryJeep();
        EntityUid gunner = default;
        await Server.WaitPost(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryEject(mount.Owner, "jeep-sentry", null, out _), Is.True);
            Assert.That(SEntMan.System<CMUJeepSentryMountSystem>().HasSentry(jeep), Is.False);
            gunner = Person(map, 0.5f, 2.5f, null, armed: false);
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(GunnerSeat(jeep), gunner);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(gunner).Buckled, Is.True,
                "With the sentry off the pintle, the gunner's seat should be free again."));
    }

    [Test]
    public async Task WeylandYutaniSentriesFitToo()
    {
        var (map, _, mount) = await SentryJeep();
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryEject(mount.Owner, "jeep-sentry", null, out _), Is.True);
            var sentry = SEntMan.SpawnEntity("AU14SentryOmniWeYu", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(mount.Owner, "jeep-sentry", sentry, null), Is.True, "A Weyland-Yutani sentry should sit on the pintle.");

            // Its corporate IFF is locked: it keeps its factions, and the window can't change them.
            Assert.That(mount.Comp.FriendlyFactions, Does.Contain(WeYu));
            Send(mount, KeyHolder(map, mount), new CMUJeepSentryFriendlyMessage("UPP", true));
            Assert.That(mount.Comp.FriendlyFactions, Does.Not.Contain("UPP"), "A locked sentry's factions shouldn't change.");
        });
    }

    private int Ammo(EntityUid sentry)
    {
        var ammo = new GetAmmoCountEvent();
        SEntMan.EventBus.RaiseLocalEvent(sentry, ref ammo);
        return ammo.Count;
    }
}
