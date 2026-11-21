using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Shows the kits bolted onto a vehicle, a bull bar or a roll cage, on its sprite and its overlay's:
/// each kit's layer is shown while it's fitted, worn while the hull is wrecked. Armored panels are
/// tinted on the vehicle as their items are.
/// </summary>
public sealed class CMUVehicleUpgradeVisualSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private CMUOpenVehicleSystem _jeep = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly HashSet<string> _fitted = new();
    private readonly HashSet<string> _layers = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleUpgradeComponent, ComponentStartup>(OnUpgradeStartup);
    }

    private void OnUpgradeStartup(Entity<CMUVehicleUpgradeComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Layer is { } layer)
            _layers.Add(layer);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehicleUpgradesComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var upgrades, out var sprite))
        {
            _fitted.Clear();
            if (_container.TryGetContainer(uid, upgrades.Container, out var kits))
            {
                foreach (var kit in kits.ContainedEntities)
                {
                    if (CompOrNull<CMUVehicleUpgradeComponent>(kit)?.Layer is { } layer)
                    {
                        _fitted.Add(layer);
                        _layers.Add(layer);
                    }
                }
            }

            var worn = _jeep.IsHullWrecked(uid) ? "1" : "0";
            Entity<SpriteComponent>? overlay = null;
            if (TryComp(uid, out CMUVehicleOverlayComponent? over)
                && TryComp(over.Overlay, out SpriteComponent? overSprite))
            {
                overlay = (over.Overlay.Value, overSprite);
            }

            foreach (var layer in _layers)
            {
                var shown = _fitted.Contains(layer);
                Show((uid, sprite), layer, $"{layer}_{worn}", shown);
                if (overlay != null)
                    Show(overlay.Value, layer, $"{layer}_overlay_{worn}", shown);
            }

            if (TryComp(uid, out CMUOpenVehicleComponent? jeep))
                TintArmor((uid, sprite), overlay, jeep);
        }
    }

    /// <summary>
    /// Armored panels show on the vehicle in their own tint: the plate on a door, the thick glass of a
    /// ballistic windshield. Stock ones show as drawn.
    /// </summary>
    private void TintArmor(
        Entity<SpriteComponent> vehicle,
        Entity<SpriteComponent>? overlay,
        CMUOpenVehicleComponent jeep)
    {
        foreach (var part in jeep.Parts)
        {
            if (part.Kind is not (CMUVehiclePartKind.Panel or CMUVehiclePartKind.Windshield) || part.Slot == null)
                continue;

            var tint = _jeep.PanelItem(vehicle, jeep, part.Id) is { } item
                && HasComp<CMUVehicleUpgradeComponent>(item)
                && TryComp(item, out SpriteComponent? itemSprite)
                    ? itemSprite.Color
                    : Color.White;

            Tint(vehicle, part.Id, tint);
            if (overlay != null)
                Tint(overlay.Value, part.Id, tint);

            // A raised hood is drawn on the overlay in a layer of its own.
            if (overlay != null && part.RaisedLayer is { } raised)
                Tint(overlay.Value, raised, tint);
        }
    }

    private void Tint(Entity<SpriteComponent> ent, string layer, Color tint)
    {
        if (_sprite.LayerMapTryGet(ent.AsNullable(), layer, out var index, false)
            && _sprite.TryGetLayer(ent.AsNullable(), index, out var current, false)
            && current.Color != tint)
        {
            _sprite.LayerSetColor(ent.AsNullable(), index, tint);
        }
    }

    private void Show(Entity<SpriteComponent> ent, string layer, string state, bool shown)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), layer, out var index, false)
            || !_sprite.TryGetLayer(ent.AsNullable(), index, out var current, false))
        {
            return;
        }

        if (current.Visible != shown)
            _sprite.LayerSetVisible(ent.AsNullable(), index, shown);

        if (shown && current.State.Name != state)
            _sprite.LayerSetRsiState(ent.AsNullable(), index, state);
    }
}
