using Content.Server.CMU14.Round;
using Content.Server.Maps;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Tells each computer which colony it is in, so colony sites are about this round's map. Every round spawns its
/// map's computers fresh, so a new map is picked up with no setup.
/// </summary>
public sealed partial class CMUPersonalComputerSystem : EntitySystem
{
    [Dependency] private IGameMapManager _gameMaps = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private AuRoundSystem _round = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPersonalComputerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUPersonalComputerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Colony != null || CurrentColony() is not { } colony)
            return;

        (ent.Comp.Colony, ent.Comp.ColonyName) = colony;
        Dirty(ent);
    }

    /// <summary>
    /// The round's map as a page folder and a name. The folder is the map id, or the shared folder of a
    /// <see cref="CMUNetColonyPrototype"/> listing it; the name is always the map's own name.
    /// </summary>
    public (string Key, string Name)? CurrentColony()
    {
        var map = _round.ActivePlanet?.MapId;
        if (string.IsNullOrEmpty(map))
            map = _gameMaps.GetSelectedMap()?.ID;

        if (string.IsNullOrEmpty(map))
            return null;

        var name = _prototype.TryIndex<GameMapPrototype>(map, out var proto) ? proto.MapName : map;
        foreach (var colony in _prototype.EnumeratePrototypes<CMUNetColonyPrototype>())
        {
            if (colony.Maps.Contains(map))
                return (colony.ID, name);
        }

        return (map, name);
    }
}
