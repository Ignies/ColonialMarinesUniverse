using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Turns a cargo jeep into a technical: the mount frame bolted down in its bed carries a gun for a
/// gunner standing in the bed.
/// </summary>
public sealed class CMUJeepTechnicalSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUVehicleSeatSystem _seats = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJeepTechnicalComponent, CMUVehicleCargoSecuredEvent>(OnSecured);
        SubscribeLocalEvent<CMUJeepTechnicalComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CMUJeepTechnicalComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnSecured(Entity<CMUJeepTechnicalComponent> ent, ref CMUVehicleCargoSecuredEvent args)
    {
        // Containers move on the server; the slot and seat follow by state.
        if (_net.IsClient)
            return;

        if (args.Secured)
            Fit(ent, args.Vehicle);
        else
            Unfit(ent, args.Vehicle);
    }

    private void Fit(Entity<CMUJeepTechnicalComponent> ent, EntityUid vehicle)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var stowed)
            || stowed.ContainedEntities.Count == 0
            || !_itemSlots.TryGetSlot(vehicle, ent.Comp.TurretSlot, out var slot)
            || slot.HasItem)
        {
            return;
        }

        var mount = stowed.ContainedEntities[0];
        _container.Remove(mount, stowed);
        if (!_itemSlots.TryInsert(vehicle, ent.Comp.TurretSlot, mount, null))
            _container.Insert(mount, stowed);
    }

    private void Unfit(Entity<CMUJeepTechnicalComponent> ent, EntityUid vehicle)
    {
        if (!_itemSlots.TryGetSlot(vehicle, ent.Comp.TurretSlot, out var slot)
            || slot.Item is not { } mount
            || slot.ContainerSlot is not { } fitted)
        {
            return;
        }

        _seats.ClearGunnerSeats(vehicle);
        var stowed = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        _container.Remove(mount, fitted);
        _container.Insert(mount, stowed);
    }

    private void OnTerminating(Entity<CMUJeepTechnicalComponent> ent, ref EntityTerminatingEvent args)
    {
        // A frame destroyed while bolted down takes its mount and gun with it.
        if (_net.IsClient
            || !TryComp(ent, out CMUVehicleCargoCrateComponent? crate)
            || !TryComp(crate.Vehicle, out CMUVehicleCargoComponent? bed)
            || bed.Crate != ent.Owner
            || !bed.Secured
            || !_itemSlots.TryGetSlot(crate.Vehicle.Value, ent.Comp.TurretSlot, out var slot)
            || slot.Item is not { } mount)
        {
            return;
        }

        _seats.ClearGunnerSeats(crate.Vehicle.Value);
        PredictedQueueDel(mount);
    }

    private void OnOpenAttempt(Entity<CMUJeepTechnicalComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("cmu-jeep-technical-no-open"), ent, args.User);
    }
}
