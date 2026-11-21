using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Fits open vehicles' armor and mobility upgrades and keeps their sum: the weight that slows the
/// vehicle past its free load, and the effects other systems read off it.
/// </summary>
public sealed class CMUVehicleUpgradeSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, ItemSlotInsertAttemptEvent>(OnSlotInsertAttempt);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, CMUVehicleKitDoAfterEvent>(OnKitDoAfter);
        SubscribeLocalEvent<CMUVehicleUpgradesComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// The share of top speed and acceleration the vehicle keeps under its upgrades' weight.
    /// </summary>
    public float SpeedMultiplier(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleUpgradesComponent? upgrades))
            return 1f;

        var over = MathF.Max(0f, upgrades.Weight - upgrades.FreeWeight);
        return MathF.Max(0f, 1f - over * upgrades.SpeedLossPerWeight);
    }

    public float CrashDamage(EntityUid vehicle)
    {
        return CompOrNull<CMUVehicleUpgradesComponent>(vehicle)?.CrashDamage ?? 1f;
    }

    public float CrashWheelDamage(EntityUid vehicle)
    {
        return CompOrNull<CMUVehicleUpgradesComponent>(vehicle)?.CrashWheelDamage ?? 1f;
    }

    public float EngineShare(EntityUid vehicle)
    {
        return CompOrNull<CMUVehicleUpgradesComponent>(vehicle)?.EngineShare ?? 1f;
    }

    public float WaterDrag(EntityUid vehicle)
    {
        return CompOrNull<CMUVehicleUpgradesComponent>(vehicle)?.WaterDrag ?? 1f;
    }

    public bool IsCrashProof(EntityUid vehicle)
    {
        return CompOrNull<CMUVehicleUpgradesComponent>(vehicle)?.CrashProof ?? false;
    }

    /// <summary>
    /// The upgrade fitted in one of the vehicle's slots, like an armored door, if it is one.
    /// </summary>
    public CMUVehicleUpgradeComponent? InSlot(EntityUid vehicle, string slot)
    {
        return _container.TryGetContainer(vehicle, slot, out var container) && container.ContainedEntities.Count > 0
            ? CompOrNull<CMUVehicleUpgradeComponent>(container.ContainedEntities[0])
            : null;
    }

    private void OnStartup(Entity<CMUVehicleUpgradesComponent> ent, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
        Recalculate(ent);
    }

    private void OnInserted(Entity<CMUVehicleUpgradesComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (HasComp<CMUVehicleUpgradeComponent>(args.Entity))
            Recalculate(ent);
    }

    private void OnRemoved(Entity<CMUVehicleUpgradesComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (HasComp<CMUVehicleUpgradeComponent>(args.Entity))
            Recalculate(ent);
    }

    /// <summary>
    /// Sums up every upgrade fitted anywhere on the vehicle. Both sides see the same containers, so
    /// each works it out for itself.
    /// </summary>
    private void Recalculate(Entity<CMUVehicleUpgradesComponent> ent)
    {
        var comp = ent.Comp;
        comp.Weight = 0f;
        comp.CrashDamage = 1f;
        comp.CrashWheelDamage = 1f;
        comp.EngineShare = 1f;
        comp.WaterDrag = 1f;
        comp.CrashProof = false;
        foreach (var upgrade in Fitted(ent))
        {
            comp.Weight += upgrade.Weight;
            comp.CrashDamage *= upgrade.CrashDamage;
            comp.CrashWheelDamage *= upgrade.CrashWheelDamage;
            comp.EngineShare *= upgrade.EngineShare;
            comp.WaterDrag *= upgrade.WaterDrag;
            comp.CrashProof |= upgrade.CrashProof;
        }
    }

    private IEnumerable<CMUVehicleUpgradeComponent> Fitted(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out ContainerManagerComponent? containers))
            yield break;

        foreach (var container in _container.GetAllContainers(vehicle, containers))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp(contained, out CMUVehicleUpgradeComponent? upgrade))
                    yield return upgrade;
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="user"/> can fit the upgrade: an engineer, and within the vehicle's most
    /// weight. Says why not if they can't.
    /// </summary>
    private bool CanFit(Entity<CMUVehicleUpgradesComponent> ent, EntityUid upgradeUid, CMUVehicleUpgradeComponent upgrade, EntityUid? user, float replacing = 0f)
    {
        string? why = null;
        if (user != null && !_skills.HasSkill(user.Value, upgrade.Skill, upgrade.SkillLevel))
            why = "cmu-vehicle-upgrade-unskilled";
        else if (ent.Comp.Weight - replacing + upgrade.Weight > ent.Comp.MaxWeight)
            why = "cmu-vehicle-upgrade-too-heavy";

        if (why == null)
            return true;

        if (user != null)
            _popup.PopupClient(Loc.GetString(why, ("upgrade", upgradeUid)), ent, user.Value, PopupType.SmallCaution);

        return false;
    }

    private void OnSlotInsertAttempt(Entity<CMUVehicleUpgradesComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || !TryComp(args.Item, out CMUVehicleUpgradeComponent? upgrade))
            return;

        // A heavier part swapped in for one already fitted only adds the difference.
        var replacing = args.Slot.Item is { } old && TryComp(old, out CMUVehicleUpgradeComponent? oldUpgrade)
            ? oldUpgrade.Weight
            : 0f;

        if (!CanFit(ent, args.Item, upgrade, args.User, replacing))
            args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<CMUVehicleUpgradesComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out CMUVehicleUpgradeComponent? upgrade) || !upgrade.Kit)
            return;

        args.Handled = true;
        if (HasKit(ent, MetaData(args.Used).EntityPrototype?.ID))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-upgrade-already", ("upgrade", args.Used)), ent, args.User);
            return;
        }

        if (!CanFit(ent, args.Used, upgrade, args.User))
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, upgrade.Delay,
            new CMUVehicleKitDoAfterEvent(true, GetNetEntity(args.Used)), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private bool HasKit(Entity<CMUVehicleUpgradesComponent> ent, string? prototype)
    {
        if (prototype == null || !_container.TryGetContainer(ent, ent.Comp.Container, out var kits))
            return false;

        foreach (var kit in kits.ContainedEntities)
        {
            if (MetaData(kit).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }

    private void OnGetVerbs(Entity<CMUVehicleUpgradesComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } tool
            || !_tool.HasQuality(tool, ent.Comp.KitQuality)
            || !_container.TryGetContainer(ent, ent.Comp.Container, out var kits))
        {
            return;
        }

        var user = args.User;
        foreach (var kit in kits.ContainedEntities)
        {
            if (!TryComp(kit, out CMUVehicleUpgradeComponent? upgrade))
                continue;

            var netKit = GetNetEntity(kit);
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-vehicle-upgrade-remove-verb", ("upgrade", kit)),
                Category = VerbCategory.Eject,
                Act = () =>
                {
                    if (!_skills.HasSkill(user, upgrade.Skill, upgrade.SkillLevel))
                    {
                        _popup.PopupClient(Loc.GetString("cmu-vehicle-upgrade-unskilled", ("upgrade", kit)), ent, user, PopupType.SmallCaution);
                        return;
                    }

                    _tool.UseTool(tool, user, ent, (float) upgrade.Delay.TotalSeconds, ent.Comp.KitQuality,
                        new CMUVehicleKitDoAfterEvent(false, netKit));
                },
            });
        }
    }

    private void OnKitDoAfter(Entity<CMUVehicleUpgradesComponent> ent, ref CMUVehicleKitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryGetEntity(args.Kit, out var kit))
            return;

        args.Handled = true;
        if (_net.IsClient || !_container.TryGetContainer(ent, ent.Comp.Container, out var kits))
            return;

        if (args.Fit)
        {
            // The kit can change hands, or another be fitted, while the do-after runs.
            if (!TryComp(kit, out CMUVehicleUpgradeComponent? upgrade)
                || !_hands.IsHolding(args.User, kit)
                || HasKit(ent, MetaData(kit.Value).EntityPrototype?.ID)
                || !CanFit(ent, kit.Value, upgrade, args.User))
            {
                return;
            }

            _container.Insert(kit.Value, kits);
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-upgrade-fitted", ("upgrade", kit.Value)), ent, args.User);
            return;
        }

        if (!kits.Contains(kit.Value))
            return;

        _container.Remove(kit.Value, kits);
        _hands.PickupOrDrop(args.User, kit.Value);
        _popup.PopupEntity(Loc.GetString("cmu-vehicle-upgrade-removed", ("upgrade", kit.Value)), ent, args.User);
    }

    private void OnExamined(Entity<CMUVehicleUpgradesComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Weight <= 0f)
            return;

        using (args.PushGroup(nameof(CMUVehicleUpgradesComponent)))
        {
            if (_container.TryGetContainer(ent, ent.Comp.Container, out var kits))
            {
                foreach (var kit in kits.ContainedEntities)
                {
                    args.PushMarkup(Loc.GetString("cmu-vehicle-upgrade-examine-kit", ("upgrade", kit)));
                }
            }

            var load = (int) MathF.Round(ent.Comp.Weight / ent.Comp.MaxWeight * 100f);
            var message = ent.Comp.Weight > ent.Comp.FreeWeight ? "cmu-vehicle-upgrade-examine-heavy" : "cmu-vehicle-upgrade-examine-load";
            args.PushMarkup(Loc.GetString(message, ("load", load)));
        }
    }
}
