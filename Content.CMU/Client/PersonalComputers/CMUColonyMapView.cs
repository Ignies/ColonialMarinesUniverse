using System.Numerics;
using Content.Client._RMC14.TacticalMap;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>A terminal pinned on the colony map: where it stands, its colour, and whether it is flagged.</summary>
public readonly record struct CMUColonyMapPin(NetEntity Entity, Vector2 Position, Color Colour, bool Flagged);

/// <summary>
/// The colony's own map, the one the tactical map tables and the overwatch consoles draw, with terminals pinned on it
/// as map blips. Built from the colony grid's areas, so every colony map works with no extra art. Clicking a pin
/// passes its terminal on. <see cref="TryCreate"/> gives nothing for a grid without area colours; the panels fall
/// back to the outline map there.
/// </summary>
public static class CMUColonyMapView
{
    private static readonly ResPath Blips = new("/Textures/_RMC14/Interface/map_blips.rsi");

    public static Control? TryCreate(IEntityManager entities, EntityUid grid, IReadOnlyList<CMUColonyMapPin> pins,
        string background, Action<NetEntity>? clicked, float height = 420)
    {
        if (!entities.TryGetComponent(grid, out AreaGridComponent? areas) || areas.Colors.Count == 0)
            return null;

        var map = new TacticalMapControl
        {
            HorizontalExpand = true,
            SetHeight = height,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
        };
        map.UpdateTexture((grid, areas));

        var blips = new TacticalMapBlip[pins.Count];
        var byTile = new Dictionary<Vector2i, NetEntity>();
        for (var i = 0; i < pins.Count; i++)
        {
            var pin = pins[i];
            var tile = new Vector2i((int) MathF.Floor(pin.Position.X), (int) MathF.Floor(pin.Position.Y));
            byTile[tile] = pin.Entity;
            blips[i] = new TacticalMapBlip(
                tile,
                new SpriteSpecifier.Rsi(Blips, pin.Flagged ? "gm_exclamation" : "gm_circle"),
                pin.Colour,
                TacticalMapBlipStatus.Alive,
                new SpriteSpecifier.Rsi(Blips, background),
                false);
        }

        map.UpdateBlips(blips);
        if (clicked != null)
        {
            map.OnBlipClicked += tile =>
            {
                if (byTile.TryGetValue(tile, out var entity))
                    clicked(entity);
            };
        }

        return map;
    }
}
