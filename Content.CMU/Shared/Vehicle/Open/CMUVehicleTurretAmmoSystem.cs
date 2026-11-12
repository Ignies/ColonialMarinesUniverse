using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Loads a jeep's mounted gun from an ammo box used on its turret, or anywhere on the jeep: the box's
/// own transfer, as if the gun were used on the box. A box of another round of the same calibre, such as
/// armor-piercing for ball, swaps the belt instead: the rounds left in the gun come out in a box of
/// their own, so a belt never mixes two kinds of round under one.
/// </summary>
public sealed class CMUVehicleTurretAmmoSystem : EntitySystem
{
    [Dependency] private BulletBoxSystem _bulletBox = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleTurretVisualComponent, InteractUsingEvent>(OnTurretInteractUsing);
        SubscribeLocalEvent<CMUOpenVehicleComponent, InteractUsingEvent>(OnJeepInteractUsing);
        SubscribeLocalEvent<BulletBoxComponent, CMUVehicleBeltSwapDoAfterEvent>(OnBeltSwap);
    }

    private void OnTurretInteractUsing(Entity<VehicleTurretVisualComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !HasComp<BulletBoxComponent>(args.Used) ||
            !TryGetEntity(ent.Comp.Turret, out var turret) ||
            !_turret.TryGetVehicle(turret.Value, out var vehicle) ||
            !HasComp<CMUOpenVehicleComponent>(vehicle))
        {
            return;
        }

        args.Handled = TryLoad(turret.Value, args.User, args.Used);
    }

    private void OnJeepInteractUsing(Entity<CMUOpenVehicleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<BulletBoxComponent>(args.Used))
            return;

        args.Handled = TryLoad(ent, args.User, args.Used);
    }

    /// <summary>
    /// Starts loading the first gun fitted on or in <paramref name="owner"/> from the box, if it takes
    /// the box's rounds and has room. Says why not if it can't.
    /// </summary>
    private bool TryLoad(EntityUid owner, EntityUid user, EntityUid used)
    {
        if (!TryComp(used, out BulletBoxComponent? box) || !TryFindGun(owner, out var gun, out var refill, out var ammo))
            return false;

        var swap = IsOtherRound(box, ammo);
        string? why = null;
        if (box.BulletType != refill.BulletType)
            why = "rmc-bullet-box-wrong-rounds";
        else if (!swap && ammo.Count >= ammo.Capacity)
            why = "rmc-bullet-box-mag-full";
        else if (box.Amount <= 0)
            why = "rmc-bullet-box-box-empty";

        if (why != null)
        {
            _popup.PopupClient(Loc.GetString(why), owner, user);
            return true;
        }

        DoAfterEvent ev = swap ? new CMUVehicleBeltSwapDoAfterEvent() : new BulletBoxTransferDoAfterEvent(false);
        var doAfter = new DoAfterArgs(EntityManager, user, swap ? box.Delay * 2 : box.Delay, ev, used, used, gun)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    /// <summary>
    /// Whether the box holds another kind of round than the one the gun is loaded with. A gun with
    /// nothing in it takes any round its boxes carry, and so does one fed from boxes that don't say.
    /// </summary>
    private static bool IsOtherRound(BulletBoxComponent box, BallisticAmmoProviderComponent ammo)
    {
        return ammo.Count > 0 && box.AmmoProto is { } round && ammo.Proto != round;
    }

    /// <summary>
    /// Takes the belt out of the gun into a box of its own round, at the loader's feet or in a free
    /// hand, then feeds the gun from the box in hand.
    /// </summary>
    private void OnBeltSwap(Entity<BulletBoxComponent> ent, ref CMUVehicleBeltSwapDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Handled ||
            args.Used is not { } gun ||
            !TryComp(gun, out BallisticAmmoProviderComponent? ammo) ||
            ent.Comp.AmmoProto is not { } round ||
            !IsOtherRound(ent.Comp, ammo))
        {
            return;
        }

        args.Handled = true;

        // The box handed back is spawned on the server alone, and the gun's count follows from there.
        if (_net.IsClient)
            return;

        var left = UnloadBelt((gun, ammo), args.User);
        _gun.SetBallisticProto((gun, ammo), round);
        var loaded = Math.Min(ammo.Capacity - ammo.Count, ent.Comp.Amount);
        if (loaded > 0 && _bulletBox.TryConsume(ent, loaded))
            _gun.SetBallisticUnspawned((gun, ammo), ammo.UnspawnedCount + loaded);

        _popup.PopupEntity(Loc.GetString("cmu-vehicle-ammo-belt-swapped",
            ("taken", left), ("loaded", loaded), ("gun", gun)), gun, args.User);
    }

    /// <summary>
    /// Empties the gun into boxes of the round it was loaded with, as many as the rounds fill, and
    /// returns how many rounds came out. Rounds without a box of their own are lost.
    /// </summary>
    private int UnloadBelt(Entity<BallisticAmmoProviderComponent> gun, EntityUid user)
    {
        var count = gun.Comp.Count;
        if (count <= 0)
            return 0;

        // Chambered entity rounds are spent here; the box carries them back as unspawned rounds.
        if (gun.Comp.Container.ContainedEntities.Count > 0)
        {
            var taken = new List<(EntityUid? Entity, IShootable Shootable)>();
            RaiseLocalEvent(gun, new TakeAmmoEvent(gun.Comp.Container.ContainedEntities.Count, taken, Transform(gun).Coordinates, user));
            foreach (var (round, _) in taken)
            {
                if (round is { } roundUid)
                    QueueDel(roundUid);
            }
        }

        _gun.SetBallisticUnspawned(gun, 0);

        if (_bulletBox.GetBoxForAmmo(gun.Comp.Proto) is not { } boxProto)
            return 0;

        var remaining = count;
        while (remaining > 0)
        {
            var spawned = Spawn(boxProto, Transform(user).Coordinates);
            if (!TryComp(spawned, out BulletBoxComponent? box) || box.Max <= 0)
            {
                Del(spawned);
                break;
            }

            var amount = Math.Min(remaining, box.Max);
            _bulletBox.TrySetAmount((spawned, box), amount);
            _hands.PickupOrDrop(user, spawned);
            remaining -= amount;
        }

        return count - remaining;
    }

    private bool TryFindGun(
        EntityUid owner,
        out EntityUid gun,
        [NotNullWhen(true)] out RefillableByBulletBoxComponent? refill,
        [NotNullWhen(true)] out BallisticAmmoProviderComponent? ammo)
    {
        gun = owner;
        if (TryComp(owner, out refill) && TryComp(owner, out ammo))
            return true;

        // An entity without containers has no ContainerManager, and its container enumerator throws.
        if (!TryComp(owner, out ContainerManagerComponent? containers))
        {
            refill = null;
            ammo = null;
            return false;
        }

        foreach (var container in _container.GetAllContainers(owner, containers))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryFindGun(contained, out gun, out refill, out ammo))
                    return true;
            }
        }

        refill = null;
        ammo = null;
        return false;
    }
}
