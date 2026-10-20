using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Client.Guidebook.Richtext;
using Content.Shared.CMU14.PersonalComputers;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// A search box on a page: <c>&lt;CMUNetSearch/&gt;</c>. Searches the words of every listed page the terminal can
/// reach, titles and addresses first, and lists what it finds as links with a line of each page. Deep, hidden and
/// unlisted sites are never found. Optional colours: <c>Text</c>, <c>Link</c>, <c>Background</c>, <c>Muted</c>.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUNetSearch : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;

    private const int MaxResults = 10;

    private readonly LineEdit _query;
    private readonly BoxContainer _results;
    private Color _text = Color.FromHex("#1A1030");
    private Color _link = Color.FromHex("#0033CC");
    private Color _background = Color.FromHex("#F4F0FF");
    private Color _muted = Color.FromHex("#7A6A90");

    public CMUNetSearch()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(0, 4);
        SeparationOverride = 4;

        _query = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("cmu-net-search-placeholder"),
        };
        _query.OnTextEntered += _ => Search();
        var go = new Button { Text = Loc.GetString("cmu-net-search-button") };
        go.OnPressed += _ => Search();
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children = { _query, go },
        });

        _results = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 6 };
        AddChild(_results);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        _text = Colour(args, "Text", _text);
        _link = Colour(args, "Link", _link);
        _background = Colour(args, "Background", _background);
        _muted = Colour(args, "Muted", _muted);
        control = this;
        return true;
    }

    private static Color Colour(Dictionary<string, string> args, string key, Color fallback)
    {
        return args.TryGetValue(key, out var value) && Color.TryFromHex(value, out var colour) ? colour : fallback;
    }

    /// <summary>Searches as if the words were typed and entered, for previews.</summary>
    public void Run(string query)
    {
        _query.Text = query;
        Search();
    }

    private void Search()
    {
        _results.RemoveAllChildren();
        var terms = CMUNetSearchIndex.Terms(_query.Text);
        if (terms.Count == 0)
            return;

        var networks = Browser()?.Networks;
        var found = CMUNetSearchIndex.Get(_entities.System<CMUExtraNetSystem>()).Find(terms, networks).Take(MaxResults).ToList();
        _results.AddChild(Line(found.Count == 0
            ? Loc.GetString("cmu-net-search-none", ("query", _query.Text.Trim()))
            : Loc.GetString("cmu-net-search-count", ("count", found.Count), ("query", _query.Text.Trim())), _muted));

        foreach (var hit in found)
        {
            var link = new CMUNetLinkLabel(hit.Title, hit.Address);
            link.SetColors(_link, _background);
            link.HorizontalAlignment = HAlignment.Left;
            var entry = new BoxContainer { Orientation = LayoutOrientation.Vertical };
            entry.AddChild(link);
            entry.AddChild(Line(hit.Address, _muted));
            if (hit.Snippet.Length > 0)
                entry.AddChild(Line(hit.Snippet, _text));

            _results.AddChild(entry);
        }
    }

    private static Label Line(string text, Color colour)
    {
        return new Label { Text = text, FontColorOverride = colour, ClipText = true, HorizontalExpand = true };
    }

    private CMUNetBrowser? Browser()
    {
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is CMUNetBrowser browser)
                return browser;
        }

        return null;
    }
}

/// <summary>A page the search found: what to show and where it goes.</summary>
public readonly record struct CMUNetSearchHit(string Title, string Address, string Snippet, int Score);

/// <summary>
/// The words of every listed, non-deep ExtraNet page, read once per client from the page files the client already
/// ships, with their markup stripped. Rebuilt if the set of sites changes, as it does when a site package loads.
/// </summary>
public sealed class CMUNetSearchIndex
{
    private static CMUNetSearchIndex? _built;
    private static int _builtFrom;

    private readonly List<Entry> _entries = new();

    private sealed record Entry(CMUNetSitePrototype Site, string Title, string Address, string Text, string Lower);

    private const string Markup = @"<[^>]*>|\[[^\]]*\]";
    private const string Space = @"\s+";

    public static CMUNetSearchIndex Get(CMUExtraNetSystem net)
    {
        var sites = net.Sites.ToList();
        var key = sites.Count;
        foreach (var site in sites)
            key = key * 31 + site.ID.GetHashCode();

        if (_built == null || _builtFrom != key)
        {
            _built = new CMUNetSearchIndex(sites);
            _builtFrom = key;
        }

        return _built;
    }

    public static List<string> Terms(string query)
    {
        return query.ToLowerInvariant()
            .Split(new[] { ' ', '\t', ',', '.', '/', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();
    }

    private CMUNetSearchIndex(List<CMUNetSitePrototype> sites)
    {
        var resources = IoCManager.Resolve<IResourceManager>();
        foreach (var site in sites)
        {
            if (site.Unlisted || site.Hidden || CMUExtraNetSystem.IsDeep(site))
                continue;

            var siteTitle = CMUExtraNetSystem.Text(site.Title);
            foreach (var (path, page) in site.Pages)
            {
                var file = site.Root / page;
                var text = string.Empty;
                if (resources.ContentFileExists(file))
                {
                    using var reader = resources.ContentFileReadText(file);
                    text = Clean(reader.ReadToEnd());
                }

                var clean = path.Trim('/');
                var tab = site.Tabs.FirstOrDefault(t => string.Equals(t.Path.Trim('/'), clean, StringComparison.OrdinalIgnoreCase));
                var title = clean.Length == 0 || tab == null ? siteTitle : $"{siteTitle}: {CMUExtraNetSystem.Text(tab.Title)}";
                var address = clean.Length == 0 ? site.Address : $"{site.Address}/{clean}";
                _entries.Add(new Entry(site, title, address, text, text.ToLowerInvariant()));
            }
        }
    }

    /// <summary>Page text without tags, markup or heading marks, on one line.</summary>
    private static string Clean(string page)
    {
        var text = Regex.Replace(page, Markup, " ").Replace("#", " ").Replace("{colony}", "the colony");
        return Regex.Replace(text, Space, " ").Trim();
    }

    /// <summary>Pages with every term, best first: a term in the title or address counts most.</summary>
    public IEnumerable<CMUNetSearchHit> Find(List<string> terms, IReadOnlySet<ProtoId<CMUNetworkPrototype>>? networks)
    {
        var hits = new List<CMUNetSearchHit>();
        foreach (var entry in _entries)
        {
            if (networks != null && !CMUExtraNetSystem.CanReach(entry.Site, networks))
                continue;

            var title = entry.Title.ToLowerInvariant();
            var address = entry.Address.ToLowerInvariant();
            var score = 0;
            var all = true;
            foreach (var term in terms)
            {
                var inText = Count(entry.Lower, term);
                var inTitle = title.Contains(term, StringComparison.Ordinal);
                var inAddress = address.Contains(term, StringComparison.Ordinal);
                if (inText == 0 && !inTitle && !inAddress)
                {
                    all = false;
                    break;
                }

                score += Math.Min(inText, 10) + (inTitle ? 20 : 0) + (inAddress ? 10 : 0);
            }

            if (all)
                hits.Add(new CMUNetSearchHit(entry.Title, entry.Address, Snippet(entry, terms[0]), score));
        }

        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Address, StringComparer.Ordinal);
    }

    private static int Count(string text, string term)
    {
        var count = 0;
        for (var at = text.IndexOf(term, StringComparison.Ordinal); at >= 0; at = text.IndexOf(term, at + term.Length, StringComparison.Ordinal))
            count++;

        return count;
    }

    /// <summary>About a line of the page round the first place the term appears.</summary>
    private static string Snippet(Entry entry, string term)
    {
        const int before = 40;
        const int length = 110;
        var at = entry.Lower.IndexOf(term, StringComparison.Ordinal);
        if (at < 0)
            at = 0;

        var start = Math.Max(0, at - before);
        var end = Math.Min(entry.Text.Length, start + length);
        var snippet = new StringBuilder();
        if (start > 0)
            snippet.Append("...");

        snippet.Append(entry.Text, start, end - start);
        if (end < entry.Text.Length)
            snippet.Append("...");

        return snippet.ToString();
    }
}
