using Content.Server.CMU14.Round;
using Content.Server.Maps;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Tells each computer which colony it is in, so colony sites are about this round's map. Every round spawns its
/// map's computers fresh, so a new map is picked up with no setup. Also tells them the round's GOVFOR platoon, for
/// sites with platoon pages, and keeps the pages saved to each desktop.
/// </summary>
public sealed partial class CMUPersonalComputerSystem : EntitySystem
{
    /// <summary>How often computers are checked against the round's GOVFOR platoon.</summary>
    private static readonly TimeSpan PlatoonCheckEvery = TimeSpan.FromSeconds(5);

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameMapManager _gameMaps = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private AuRoundSystem _round = default!;
    [Dependency] private CMUExtraNetSystem _net = default!;
    [Dependency] private DeepWeb.CMUDeepAliasSystem _aliases = default!;
    [Dependency] private CMUComputerSessionSystem _sessions = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextPlatoonCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPersonalComputerComponent, MapInitEvent>(OnMapInit);
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUNetSaveShortcutMessage>(OnSaveShortcut);
            subs.Event<CMUNetRemoveShortcutMessage>(OnRemoveShortcut);
            subs.Event<CMUNoteSaveMessage>(OnSaveNote);
            subs.Event<CMUNoteDeleteMessage>(OnDeleteNote);
            subs.Event<CMUNetSetWallpaperMessage>(OnSetWallpaper);
        });
    }

    private void OnSaveShortcut(Entity<CMUPersonalComputerComponent> ent, ref CMUNetSaveShortcutMessage args)
    {
        var address = args.Address.Trim();
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Shortcuts)
            || address.Length == 0
            || address.Length > CMUNetShortcutLimits.AddressLength
            || !CanSave(ent, address))
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

    /// <summary>Saves a note in Files, or replaces the one with the same name. Signed by whoever is logged in.</summary>
    private void OnSaveNote(Entity<CMUPersonalComputerComponent> ent, ref CMUNoteSaveMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Notes))
            return;

        var name = args.Name.Trim().Replace('\n', ' ');
        var text = args.Text.Trim();
        if (name.Length == 0 || text.Length == 0)
            return;

        if (name.Length > CMUNoteLimits.NameLength)
            name = name[..CMUNoteLimits.NameLength];

        if (text.Length > CMUNoteLimits.TextLength)
            text = text[..CMUNoteLimits.TextLength];

        ent.Comp.Notes.RemoveAll(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (ent.Comp.Notes.Count >= CMUNoteLimits.Count)
            ent.Comp.Notes.RemoveAt(0);

        ent.Comp.Notes.Add(new CMUComputerNote { Name = name, Author = ent.Comp.SessionUser ?? string.Empty, Text = text });
        Dirty(ent);
    }

    private void OnDeleteNote(Entity<CMUPersonalComputerComponent> ent, ref CMUNoteDeleteMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Notes) || args.Index < 0 || args.Index >= ent.Comp.Notes.Count)
            return;

        ent.Comp.Notes.RemoveAt(args.Index);
        Dirty(ent);
    }

    private void OnSetWallpaper(Entity<CMUPersonalComputerComponent> ent, ref CMUNetSetWallpaperMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Wallpapers)
            || args.Wallpaper is { } wallpaper && !_prototype.HasIndex(wallpaper))
            return;

        ent.Comp.Wallpaper = args.Wallpaper;
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
        Setup(ent);
    }

    /// <summary>Gives a computer its terminal number, colony and platoon; the console bridge calls it too.</summary>
    public void Setup(Entity<CMUPersonalComputerComponent> ent)
    {
        ent.Comp.TerminalId = _sessions.NextTerminalId();
        if (ent.Comp.Colony == null && CurrentColony() is { } colony)
            (ent.Comp.Colony, ent.Comp.ColonyName) = colony;

        ent.Comp.Platoon = _platoons.SelectedGovforPlatoon?.ID;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextPlatoonCheck)
            return;

        _nextPlatoonCheck = _timing.CurTime + PlatoonCheckEvery;
        RefreshPlatoon();
    }

    /// <summary>
    /// Gives every computer the round's GOVFOR platoon. The platoon is often voted in or set by an admin after the
    /// map's computers have spawned, and can change mid round, so this runs every few seconds.
    /// </summary>
    public void RefreshPlatoon()
    {
        var platoon = _platoons.SelectedGovforPlatoon?.ID;
        var computers = EntityQueryEnumerator<CMUPersonalComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
        {
            if (computer.Platoon == platoon)
                continue;

            computer.Platoon = platoon;
            Dirty(uid, computer);
        }
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
