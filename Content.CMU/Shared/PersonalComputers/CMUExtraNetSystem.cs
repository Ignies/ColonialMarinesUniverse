using System.Diagnostics.CodeAnalysis;
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

    private readonly Dictionary<string, CMUNetSitePrototype> _sites = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        RebuildSites();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUNetSitePrototype>())
            RebuildSites();
    }

    private void RebuildSites()
    {
        _sites.Clear();
        foreach (var site in _prototype.EnumeratePrototypes<CMUNetSitePrototype>())
        {
            if (!_sites.TryAdd(NormalizeHost(site.Address), site))
                Log.Error($"ExtraNet sites {_sites[NormalizeHost(site.Address)].ID} and {site.ID} share the address {site.Address}");
        }
    }

    public IEnumerable<CMUNetSitePrototype> Sites => _sites.Values;

    public HashSet<ProtoId<CMUNetworkPrototype>> GetNetworks(CMUPersonalComputerComponent computer)
    {
        if (computer.Networks != null)
            return computer.Networks;

        return _prototype.TryIndex(computer.Os, out var os) ? os.Networks : new HashSet<ProtoId<CMUNetworkPrototype>>();
    }

    public string GetHomePage(CMUPersonalComputerComponent computer)
    {
        if (computer.HomePage != null)
            return computer.HomePage;

        return _prototype.TryIndex(computer.Os, out var os) ? os.HomePage : string.Empty;
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
