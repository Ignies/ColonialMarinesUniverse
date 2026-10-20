using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>One entry in a site's tab strip.</summary>
[DataDefinition]
public sealed partial class CMUNetTab
{
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Page path on the site, as listed in its pages; empty for the index.</summary>
    [DataField]
    public string Path = string.Empty;
}

/// <summary>
/// A site on the ExtraNet: an address and the guidebook style documents served under it.
/// </summary>
[Prototype("cmuNetSite")]
public sealed partial class CMUNetSitePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Host players type in the address bar, e.g. <c>colony.seegson.net</c>. Lowercase, no slashes.</summary>
    [DataField(required: true)]
    public string Address = string.Empty;

    /// <summary>Shown in the browser title bar and the directory.</summary>
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Networks the site is on. A computer reaches the site if it shares at least one.</summary>
    [DataField(required: true)]
    public HashSet<ProtoId<CMUNetworkPrototype>> Networks = new();

    /// <summary>
    /// Folder the page files are relative to. A site package may leave it out: it is then the folder its
    /// <c>site.yml</c> is in.
    /// </summary>
    [DataField]
    public ResPath Root;

    /// <summary>
    /// Page path to document file under <see cref="Root"/>. The empty path is the site's index.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, string> Pages = new();

    /// <summary>Default look of every page on the site. Pages override it with a CMUNetStyle tag.</summary>
    [DataField]
    public CMUNetPageStyle? Style;

    /// <summary>Tab strip shown above every page of the site, in order.</summary>
    [DataField]
    public List<CMUNetTab> Tabs = new();

    /// <summary>Makes the site a board of round postings shown under its index page.</summary>
    [DataField]
    public CMUNetBoardDefinition? Board;

    /// <summary>
    /// Serves <c>&lt;root&gt;/&lt;platoon&gt;/&lt;file&gt;</c> when the round's GOVFOR platoon has its own version
    /// of a page, before any colony version. The folder is a <c>PlatoonPrototype</c> id.
    /// </summary>
    [DataField]
    public bool PlatoonPages;

    /// <summary>Hidden sites still resolve but are left out of directory listings.</summary>
    [DataField]
    public bool Unlisted;

    /// <summary>
    /// Only reachable through the deep web's ad maze: scans never turn it up, and a computer cannot open it until
    /// it has reached a page that links to it.
    /// </summary>
    [DataField]
    public bool Hidden;

    /// <summary>
    /// Ending of the random address a deep site gets every round, e.g. <c>.deep</c>, <c>.file</c> or <c>.cam</c>.
    /// Deep sites only answer at that address; <see cref="Address"/> is just how pages link to them.
    /// </summary>
    [DataField]
    public string AliasSuffix = ".deep";
}
