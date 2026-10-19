using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// Gives every deep site a new random address each round, so nobody can skip the hack or the ad maze by
/// remembering an address from an earlier round. Hacked computers get the table when they are hacked.
/// </summary>
public sealed partial class CMUDeepAliasSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly Dictionary<ProtoId<CMUNetSitePrototype>, string> _aliases = new();
    private readonly Dictionary<string, ProtoId<CMUNetSitePrototype>> _sites = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _aliases.Clear();
            _sites.Clear();
        });
        SubscribeLocalEvent<CMUDeepWebAccessComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CMUDeepWebAccessComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Aliases = new Dictionary<ProtoId<CMUNetSitePrototype>, string>(Aliases);
        Dirty(ent);
    }

    public IReadOnlyDictionary<ProtoId<CMUNetSitePrototype>, string> Aliases
    {
        get
        {
            Build();
            return _aliases;
        }
    }

    /// <summary>This round's address of a deep site.</summary>
    public string GetAlias(CMUNetSitePrototype site)
    {
        Build();
        return _aliases.TryGetValue(site.ID, out var alias) ? alias : site.Address;
    }

    /// <summary>The deep site at one of this round's addresses.</summary>
    public bool TryGetSite(string host, out ProtoId<CMUNetSitePrototype> site)
    {
        Build();
        return _sites.TryGetValue(host, out site);
    }

    private void Build()
    {
        if (_aliases.Count > 0)
            return;

        var rng = new System.Random(_random.Next());
        foreach (var site in _prototype.EnumeratePrototypes<CMUNetSitePrototype>())
        {
            if (!CMUExtraNetSystem.IsDeep(site))
                continue;

            string alias;
            do
            {
                alias = CMUDeepAddress.NewAlias(rng, site.AliasSuffix);
            } while (_sites.ContainsKey(alias));

            _aliases[site.ID] = alias;
            _sites[alias] = site.ID;
        }
    }
}
