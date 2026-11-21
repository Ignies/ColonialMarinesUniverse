using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Fires the handheld gun clamped in a vehicle gun cradle as the cradle's own: the cradle takes its
/// rounds from the gun and copies its rate, burst, speed and sound, with less spread and recoil, and the
/// gun's own touches on its shots, like damage falloff and IFF, still apply. It shoots as true as
/// wielded; its wield, skill and other hand-held checks don't apply on the mount.
/// </summary>
public sealed class CMUGunCradleSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    // The vehicle's gun mount slot.
    private const string TurretSlot = "turret";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGunCradleComponent, EntInsertedIntoContainerMessage>(OnGunInserted);
        SubscribeLocalEvent<CMUGunCradleComponent, EntRemovedFromContainerMessage>(OnGunRemoved);
        SubscribeLocalEvent<CMUGunCradleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUGunCradleComponent, CMUGunCradleClampDoAfterEvent>(OnClamp);
        SubscribeLocalEvent<CMUGunCradleComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUGunCradleComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<CMUGunCradleComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<CMUGunCradleComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<CMUGunCradleComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<CMUGunCradleComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<CMUCradledGunComponent, GetWeaponAccuracyEvent>(OnCradledAccuracy);
    }

    /// <summary>
    /// Hands <paramref name="used"/>, like a magazine, to the gun clamped in a cradle on the vehicle's
    /// gun mount, as if it were used on the gun itself. Returns whether the gun took it.
    /// </summary>
    public bool TryLoad(EntityUid vehicle, EntityUid user, EntityUid used, EntityCoordinates click)
    {
        if (FindCradle(vehicle) is not { } cradle || GetGun(cradle) is not { } gun)
            return false;

        var ev = new InteractUsingEvent(user, used, gun, click);
        RaiseLocalEvent(gun, ev);
        return ev.Handled;
    }

    /// <summary>
    /// The cradle fitted to the gun mount in the vehicle's turret slot, if there is one.
    /// </summary>
    private Entity<CMUGunCradleComponent>? FindCradle(EntityUid vehicle)
    {
        if (_itemSlots.GetItemOrNull(vehicle, TurretSlot) is not { } mount || !TryComp(mount, out ItemSlotsComponent? slots))
            return null;

        foreach (var slot in slots.Slots.Values)
        {
            if (slot.Item is { } item && TryComp(item, out CMUGunCradleComponent? cradle))
                return (item, cradle);
        }

        return null;
    }

    /// <summary>
    /// The gun clamped in the cradle, if any.
    /// </summary>
    public EntityUid? GetGun(Entity<CMUGunCradleComponent> cradle)
    {
        return _itemSlots.GetItemOrNull(cradle.Owner, cradle.Comp.Slot);
    }

    /// <summary>
    /// Whether the gun is screwed in and ready to fire.
    /// </summary>
    public bool IsClamped(Entity<CMUGunCradleComponent> cradle)
    {
        return GetGun(cradle) != null
            && _itemSlots.TryGetSlot(cradle.Owner, cradle.Comp.Slot, out var slot)
            && slot.Locked;
    }

    private void OnGunInserted(Entity<CMUGunCradleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        OnGunChanged(ent, args);
    }

    private void OnGunRemoved(Entity<CMUGunCradleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        OnGunChanged(ent, args);
    }

    private void OnGunChanged(Entity<CMUGunCradleComponent> ent, ContainerModifiedMessage args)
    {
        if (args.Container.ID != ent.Comp.Slot)
            return;

        // The gun keeps its marker only while it's in the cradle; it reads its accuracy afresh either way.
        if (!_timing.ApplyingState)
        {
            if (args is EntInsertedIntoContainerMessage)
                EnsureComp<CMUCradledGunComponent>(args.Entity);
            else
                RemComp<CMUCradledGunComponent>(args.Entity);
        }

        _gun.RefreshModifiers(args.Entity);

        if (GetGun(ent) is { } gun && TryComp(gun, out GunComponent? held) && TryComp(ent, out GunComponent? own))
            _gun.SelectFire(ent, own, held.SelectedMode);

        _gun.RefreshModifiers(ent.Owner);
    }

    private void OnInteractUsing(Entity<CMUGunCradleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, ent.Comp.ClampQuality))
            return;

        if (GetGun(ent) == null)
        {
            _popup.PopupClient(Loc.GetString("cmu-gun-cradle-empty"), ent, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = _tool.UseTool(args.Used, args.User, ent, (float) ent.Comp.ClampDelay.TotalSeconds,
            ent.Comp.ClampQuality, new CMUGunCradleClampDoAfterEvent());
    }

    private void OnClamp(Entity<CMUGunCradleComponent> ent, ref CMUGunCradleClampDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_itemSlots.TryGetSlot(ent.Owner, ent.Comp.Slot, out var slot) || !slot.HasItem)
            return;

        args.Handled = true;
        var clamped = !slot.Locked;
        _itemSlots.SetLock(ent.Owner, slot, clamped);
        var message = clamped ? "cmu-gun-cradle-clamped" : "cmu-gun-cradle-unclamped";
        _popup.PopupClient(Loc.GetString(message, ("gun", slot.Item!.Value)), ent, args.User);
    }

    private void OnExamined(Entity<CMUGunCradleComponent> ent, ref ExaminedEvent args)
    {
        if (GetGun(ent) is not { } gun)
        {
            args.PushMarkup(Loc.GetString("cmu-gun-cradle-examine-empty"));
            return;
        }

        var message = IsClamped(ent) ? "cmu-gun-cradle-examine-clamped" : "cmu-gun-cradle-examine-loose";
        args.PushMarkup(Loc.GetString(message, ("gun", gun)));
    }

    private void OnAttemptShoot(Entity<CMUGunCradleComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || IsClamped(ent))
            return;

        args.Cancelled = true;
        args.ResetCooldown = true;
        args.Message = Loc.GetString(GetGun(ent) == null ? "cmu-gun-cradle-empty" : "cmu-gun-cradle-loose");
    }

    private void OnTakeAmmo(Entity<CMUGunCradleComponent> ent, ref TakeAmmoEvent args)
    {
        if (GetGun(ent) is not { } gun)
            return;

        // The cradled gun hands over its rounds straight into the cradle's shot.
        var inner = new TakeAmmoEvent(args.Shots, args.Ammo, args.Coordinates, args.User);
        RaiseLocalEvent(gun, inner);
        args.Reason ??= inner.Reason;
    }

    private void OnGetAmmoCount(Entity<CMUGunCradleComponent> ent, ref GetAmmoCountEvent args)
    {
        if (GetGun(ent) is not { } gun)
            return;

        var inner = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref inner);
        args.Count = inner.Count;
        args.Capacity = inner.Capacity;
    }

    private void OnAmmoShot(Entity<CMUGunCradleComponent> ent, ref AmmoShotEvent args)
    {
        // The gun's own touches on its rounds, like damage falloff, still apply.
        if (GetGun(ent) is { } gun)
            RaiseLocalEvent(gun, args);
    }

    private void OnCradledAccuracy(Entity<CMUCradledGunComponent> ent, ref GetWeaponAccuracyEvent args)
    {
        // Nobody wields a gun in a cradle; the mount holds it steadier than both hands would.
        if (TryComp(ent, out RMCWeaponAccuracyComponent? accuracy) && accuracy.AccuracyMultiplierUnwielded > 0)
            args.AccuracyMultiplier *= accuracy.AccuracyMultiplier / accuracy.AccuracyMultiplierUnwielded;
    }

    private void OnRefreshModifiers(Entity<CMUGunCradleComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (GetGun(ent) is not { } gun || !TryComp(gun, out GunComponent? held))
            return;

        var spread = ent.Comp.DefaultSpread;
        if (TryComp(gun, out ItemComponent? item))
            spread = ent.Comp.SpreadBySize.GetValueOrDefault(item.Size, spread);

        args.SoundGunshot = held.SoundGunshotModified;
        args.CameraRecoilScalar = held.CameraRecoilScalarModified * ent.Comp.Recoil;
        args.AngleIncrease = held.AngleIncreaseModified * spread;
        args.AngleDecay = held.AngleDecayModified;
        args.MaxAngle = held.MaxAngleModified * spread;
        args.MinAngle = held.MinAngleModified * spread;
        args.ShotsPerBurst = held.ShotsPerBurstModified;
        args.FireRate = held.FireRateModified;
        args.ProjectileSpeed = held.ProjectileSpeedModified;
    }
}
