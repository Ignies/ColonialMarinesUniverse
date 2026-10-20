using System.Diagnostics.CodeAnalysis;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

public enum CMUNetResolveResult : byte
{
    Found,
    UnknownHost,
    UnknownPage,
    AccessDenied,
}

/// <summary>
/// A resolved ExtraNet address. <see cref="File"/> is only set when the result is <see cref="CMUNetResolveResult.Found"/>.
/// </summary>
public readonly record struct CMUNetPage(
    CMUNetResolveResult Result,
    string Address,
    CMUNetSitePrototype? Site,
    ResPath? File);

/// <summary>
/// Maps ExtraNet addresses to sites and page files.
/// </summary>
public sealed partial class CMUExtraNetSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceManager _resources = default!;

    /// <summary>
    /// Where site packages live: any folder under here with a <c>site.yml</c> is a site, pages and art
    /// included, with no other file to edit. Contributors drop a folder in, or export one from the editor.
    /// </summary>
    public static readonly ResPath PackageRoot = new("/ServerInfo/CMU14/ExtraNet");

    public const string PackageManifest = "site.yml";

    private readonly Dictionary<string, CMUNetSitePrototype> _sites = new();
    private readonly Dictionary<string, ResPath> _packageFolders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadPackages();
        RebuildSites();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUNetSitePrototype>())
            RebuildSites();
    }

    /// <summary>
    /// Loads every site package's manifest as a site, rooted at its own folder unless it says otherwise.
    /// </summary>
    private void LoadPackages()
    {
        var changed = new Dictionary<Type, HashSet<string>>();
        foreach (var file in _resources.ContentFindFiles(PackageRoot))
        {
            if (file.Filename != PackageManifest)
                continue;

            var before = changed.TryGetValue(typeof(CMUNetSitePrototype), out var seen) ? new HashSet<string>(seen) : new HashSet<string>();
            using (var reader = _resources.ContentFileReadText(file))
            {
                _prototype.LoadFromStream(reader, true, changed);
            }

            if (!changed.TryGetValue(typeof(CMUNetSitePrototype), out var now))
                continue;

            foreach (var id in now)
            {
                if (!before.Contains(id))
                    _packageFolders[id] = file.Directory;
            }
        }

        if (changed.Count == 0)
            return;

        _prototype.ReloadPrototypes(changed);
    }

    /// <summary>In a site package, <c>"./banner.png"</c> means the file next to the page.</summary>
    public static string LocalPaths(string text, CMUNetSitePrototype site)
    {
        return text.Replace("\"./", $"\"{site.Root.ToString().TrimEnd('/')}/", StringComparison.Ordinal);
    }

    /// <summary>A title or tab name: a locale id, or plain text in a site package.</summary>
    public static string Text(string text)
    {
        return Robust.Shared.Localization.Loc.TryGetString(text, out var localized) ? localized : text;
    }

    private void RebuildSites()
    {
        _sites.Clear();
        foreach (var site in _prototype.EnumeratePrototypes<CMUNetSitePrototype>())
        {
            // A package without a root is rooted at its own folder; reapplied after every reload.
            if (site.Root == default && _packageFolders.TryGetValue(site.ID, out var folder))
                site.Root = folder;

            if (!_sites.TryAdd(NormalizeHost(site.Address), site))
                Log.Error($"ExtraNet sites {_sites[NormalizeHost(site.Address)].ID} and {site.ID} share the address {site.Address}");
        }
    }

    public IEnumerable<CMUNetSitePrototype> Sites => _sites.Values;

    /// <summary>The networks a computer reaches: its own or its OS's, plus the deep web once hacked.</summary>
    public HashSet<ProtoId<CMUNetworkPrototype>> GetNetworks(Entity<CMUPersonalComputerComponent> computer)
    {
        var networks = new HashSet<ProtoId<CMUNetworkPrototype>>();
        if (computer.Comp.Networks != null)
            networks.UnionWith(computer.Comp.Networks);
        else if (_prototype.TryIndex(computer.Comp.Os, out var os))
            networks.UnionWith(os.Networks);

        if (TryComp(computer, out DeepWeb.CMUDeepWebAccessComponent? deep))
            networks.Add(deep.Network);

        return networks;
    }

    public string GetHomePage(CMUPersonalComputerComponent computer)
    {
        if (computer.HomePage != null)
            return computer.HomePage;

        return _prototype.TryIndex(computer.Os, out var os) ? os.HomePage : string.Empty;
    }

    public static readonly ProtoId<CMUNetworkPrototype> DeepNetwork = "CMUNetDeep";

    /// <summary>Deep sites get a new random address every round; see <see cref="CMUNetSitePrototype.AliasSuffix"/>.</summary>
    public static bool IsDeep(CMUNetSitePrototype site)
    {
        return site.Networks.Contains(DeepNetwork);
    }

    /// <summary>A link to a random listed site the reader's terminal reaches, like an old webring.</summary>
    public const string RandomAddress = "net://random";

    /// <summary>Addresses the browser draws or picks itself: the directory, The deeps.exi home, a random site.</summary>
    public static bool IsBuiltIn(string address)
    {
        return address.StartsWith("sys://", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith("deeps://", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith("net://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a site came from a site package. Packages stand alone, so their links to other hosts are not
    /// checked: the host may live on another branch, and a missing one shows the unknown host page.
    /// </summary>
    public bool IsPackage(CMUNetSitePrototype site)
    {
        return _packageFolders.ContainsKey(site.ID);
    }

    /// <summary>Whether a computer can open a site: on its networks, and unlocked through the ad maze if hidden.</summary>
    public bool CanOpen(EntityUid computer, CMUNetSitePrototype site, IReadOnlySet<ProtoId<CMUNetworkPrototype>> networks)
    {
        if (!CanReach(site, networks))
            return false;

        return !site.Hidden
            || TryComp(computer, out DeepWeb.CMUDeepWebAccessComponent? deep) && deep.Unlocked.Contains(site.ID);
    }

    public static bool CanReach(CMUNetSitePrototype site, IReadOnlySet<ProtoId<CMUNetworkPrototype>> networks)
    {
        return site.Networks.Overlaps(networks);
    }

    /// <summary>
    /// Resolves an address typed by a player or found in a link.
    /// Pass null networks to skip the access check, as the link validation test does.
    /// </summary>
    public CMUNetPage Resolve(string address, IReadOnlySet<ProtoId<CMUNetworkPrototype>>? networks)
    {
        var (host, path) = Split(address);
        var normalized = path.Length == 0 ? host : $"{host}/{path}";

        if (!_sites.TryGetValue(host, out var site))
            return new CMUNetPage(CMUNetResolveResult.UnknownHost, normalized, null, null);

        if (networks != null && !CanReach(site, networks))
            return new CMUNetPage(CMUNetResolveResult.AccessDenied, normalized, site, null);

        if (!TryGetPageFile(site, path, out var file))
            return new CMUNetPage(CMUNetResolveResult.UnknownPage, normalized, site, null);

        return new CMUNetPage(CMUNetResolveResult.Found, normalized, site, file);
    }

    public static bool TryGetPageFile(CMUNetSitePrototype site, string path, [NotNullWhen(true)] out ResPath? file)
    {
        foreach (var (key, value) in site.Pages)
        {
            if (!string.Equals(NormalizePath(key), path, StringComparison.Ordinal))
                continue;

            file = site.Root / value;
            return true;
        }

        file = null;
        return false;
    }

    /// <summary>
    /// Splits an address into a lowercase host and a page path without surrounding slashes.
    /// A scheme such as <c>http://</c> is ignored.
    /// </summary>
    public static (string Host, string Path) Split(string address)
    {
        var trimmed = address.Trim();
        var scheme = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            trimmed = trimmed[(scheme + 3)..];

        var slash = trimmed.IndexOf('/');
        if (slash < 0)
            return (NormalizeHost(trimmed), string.Empty);

        return (NormalizeHost(trimmed[..slash]), NormalizePath(trimmed[(slash + 1)..]));
    }

    private static string NormalizeHost(string host)
    {
        return host.Trim().ToLowerInvariant();
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Trim('/').ToLowerInvariant();
    }
}
