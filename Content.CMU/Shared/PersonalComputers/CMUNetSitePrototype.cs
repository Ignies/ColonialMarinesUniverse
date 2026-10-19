using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

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

    /// <summary>Folder the page files are relative to.</summary>
    [DataField(required: true)]
    public ResPath Root;

    /// <summary>
    /// Page path to document file under <see cref="Root"/>. The empty path is the site's index.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, string> Pages = new();

    /// <summary>Default look of every page on the site. Pages override it with a CMUNetStyle tag.</summary>
    [DataField]
    public CMUNetPageStyle? Style;

    /// <summary>Makes the site a board of round postings shown under its index page.</summary>
    [DataField]
    public CMUNetBoardDefinition? Board;

    /// <summary>Hidden sites still resolve but are left out of directory listings.</summary>
    [DataField]
    public bool Unlisted;
}
