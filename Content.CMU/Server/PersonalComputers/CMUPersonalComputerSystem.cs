using Content.Server.CMU14.Round;
using Content.Server.Maps;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Tells each computer which colony it is in, so colony sites are about this round's map. Every round spawns its
/// map's computers fresh, so a new map is picked up with no setup. Also keeps the pages saved to each desktop.
/// </summary>
public sealed partial class CMUPersonalComputerSystem : EntitySystem
{
    [Dependency] private IGameMapManager _gameMaps = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private AuRoundSystem _round = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private DeepWeb.CMUDeepAliasSystem _aliases = default!;
    [Dependency] private CMUComputerSessionSystem _sessions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPersonalComputerComponent, MapInitEvent>(OnMapInit);
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUNetSaveShortcutMessage>(OnSaveShortcut);
            subs.Event<CMUNetRemoveShortcutMessage>(OnRemoveShortcut);
        });
    }

    private void OnSaveShortcut(Entity<CMUPersonalComputerComponent> ent, ref CMUNetSaveShortcutMessage args)
    {
        var address = args.Address.Trim();
        if (address.Length == 0 || address.Length > CMUNetShortcutLimits.AddressLength || !CanSave(ent, address))
            return;

        var title = args.Title.Trim().Replace('\n', ' ');
        if (title.Length > CMUNetShortcutLimits.TitleLength)
            title = title[..CMUNetShortcutLimits.TitleLength];

        ent.Comp.Shortcuts.RemoveAll(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
        if (ent.Comp.Shortcuts.Count >= CMUNetShortcutLimits.Count)
            ent.Comp.Shortcuts.RemoveAt(0);

        ent.Comp.Shortcuts.Add(new CMUNetShortcut { Address = address, Title = title.Length == 0 ? address : title });
        Dirty(ent);
    }

    private void OnRemoveShortcut(Entity<CMUPersonalComputerComponent> ent, ref CMUNetRemoveShortcutMessage args)
    {
        var address = args.Address;
        if (ent.Comp.Shortcuts.RemoveAll(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase)) > 0)
            Dirty(ent);
    }

    /// <summary>Only pages this computer can open: its sites, and on a hacked one the deep home and ad pages.</summary>
    public bool CanSave(Entity<CMUPersonalComputerComponent> computer, string address)
    {
        var deep = HasComp<CMUDeepWebAccessComponent>(computer);
        if (address.StartsWith("deeps://", StringComparison.OrdinalIgnoreCase))
            return deep;

        if (CMUExtraNetSystem.IsBuiltIn(address))
            return true;

        var (host, path) = CMUExtraNetSystem.Split(address);
        if (CMUDeepAddress.IsAd(host))
            return deep;

        // Deep sites by this round's address only; their fixed names are just for links between pages.
        var networks = _net.GetNetworks(computer);
        if (_aliases.TryGetSite(host, out var aliased) && _prototype.TryIndex(aliased, out var deepSite))
            address = path.Length == 0 ? deepSite.Address : $"{deepSite.Address}/{path}";

        var page = _net.Resolve(address, networks);
        if (page.Site != null && CMUExtraNetSystem.IsDeep(page.Site) && aliased != page.Site.ID)
            return false;

        return page.Result == CMUNetResolveResult.Found && _net.CanOpen(computer, page.Site!, networks);
    }

    private void OnMapInit(Entity<CMUPersonalComputerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.TerminalId = _sessions.NextTerminalId();
        if (ent.Comp.Colony == null && CurrentColony() is { } colony)
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
