using System.Collections.Generic;
using System.Linq;
using Content.Client.CMU14.PersonalComputers;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

[TestFixture]
[TestOf(typeof(CMUExtraNetSystem))]
[TestOf(typeof(CMUNetSitePrototype))]
public sealed class CMUExtraNetTest : GameTest
{
    [Test]
    [Description("Every page of every site exists, parses, and only links to pages that exist.")]
    public async Task SitePagesParseAndLinksResolve()
    {
        var client = Pair.Client;
        await client.WaitIdleAsync();
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var resMan = client.ResolveDependency<IResourceManager>();
        var parser = client.ResolveDependency<DocumentParsingManager>();
        var net = client.System<CMUExtraNetSystem>();

        await client.WaitAssertion(() =>
        {
            var sites = protoMan.EnumeratePrototypes<CMUNetSitePrototype>().ToList();
            Assert.That(sites, Is.Not.Empty);
            var links = 0;
            Assert.Multiple(() =>
            {
                foreach (var site in sites)
                    links += ValidateSite(site, resMan, parser, net);
            });
            Assert.That(links, Is.Positive, "No netlink tags were rendered; the tag is not registered.");
        });
    }

    private static int ValidateSite(
        CMUNetSitePrototype site,
        IResourceManager resMan,
        DocumentParsingManager parser,
        CMUExtraNetSystem net)
    {
        Assert.That(site.Address, Is.EqualTo(site.Address.Trim().ToLowerInvariant()),
            "Site addresses must be lowercase.");
        Assert.That(site.Address, Does.Not.Contain("/").And.Not.Contain(" "),
            "Site addresses are a bare host without slashes or spaces.");
        Assert.That(net.Resolve(site.Address, null).Site, Is.SameAs(site),
            "Another site already uses this address.");
        Assert.That(site.Pages.Keys.Any(k => k.Trim('/').Length == 0),
            "Every site needs an index page listed under \"\".");

        foreach (var tab in site.Tabs)
        {
            Assert.That(CMUExtraNetSystem.TryGetPageFile(site, tab.Path.Trim('/').ToLowerInvariant(), out _),
                $"Tab {tab.Title} on {site.Address} points to {tab.Path}, which is not one of its pages.");
        }

        var links = 0;
        foreach (var (_, file) in site.Pages)
            links += ValidateDocument(site.Root / file, resMan, parser, net);

        return links;
    }

    /// <summary>The file exists, every tag in it parses, and every netlink points to a page.</summary>
    private static int ValidateDocument(
        ResPath resPath,
        IResourceManager resMan,
        DocumentParsingManager parser,
        CMUExtraNetSystem net)
    {
        if (!resMan.ContentFileExists(resPath))
        {
            Assert.Fail($"Missing document {resPath}.");
            return 0;
        }

        using var reader = resMan.ContentFileReadText(resPath);
        var document = new Document();
        Assert.That(parser.TryAddMarkup(document, reader.ReadToEnd()), $"Document {resPath} failed to parse.");

        var controls = Descendants(document).ToList();
        Assert.That(controls.OfType<GuidebookError>(), Is.Empty, $"Document {resPath} contains invalid markup.");

        var links = 0;
        foreach (var link in controls.OfType<CMUNetLinkLabel>())
        {
            links++;
            Assert.That(net.Resolve(link.Link, null).Result, Is.EqualTo(CMUNetResolveResult.Found),
                $"Document {resPath} links to {link.Link}, which is not a page.");
        }

        return links;
    }

    [Test]
    [Description("Each OS home page is reachable from that OS, and its documents and logo path are valid.")]
    public async Task OperatingSystemsAreValid()
    {
        var client = Pair.Client;
        await client.WaitIdleAsync();
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var resMan = client.ResolveDependency<IResourceManager>();
        var parser = client.ResolveDependency<DocumentParsingManager>();
        var net = client.System<CMUExtraNetSystem>();

        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var os in protoMan.EnumeratePrototypes<CMUComputerOsPrototype>())
                {
                    Assert.That(net.Resolve(os.HomePage, os.Networks).Result, Is.EqualTo(CMUNetResolveResult.Found),
                        $"{os.ID} home page {os.HomePage} is not a reachable page.");

                    // The logo file may not exist yet, but the path must be one the splash can load once it does.
                    foreach (var logo in os.Logos)
                    {
                        var path = new ResPath(logo);
                        Assert.That(path.IsRooted, $"{os.ID} logo must be an absolute path.");
                        Assert.That(path.Extension, Is.EqualTo("png").Or.EqualTo("rsi"), $"{os.ID} logo must be a .png or .rsi.");
                    }

                    foreach (var document in os.Documents)
                        ValidateDocument(document.Path, resMan, parser, net);
                }
            });
        });
    }

    [Test]
    [Description("Addresses ignore case, schemes and slashes; unreachable networks are denied.")]
    public async Task AddressesResolve()
    {
        var client = Pair.Client;
        await client.WaitIdleAsync();
        var net = client.System<CMUExtraNetSystem>();
        var publicOnly = new HashSet<ProtoId<CMUNetworkPrototype>> { "CMUNetPublic" };

        await client.WaitAssertion(() =>
        {
            var page = net.Resolve("HTTP://Colony.Seegson.NET/Housing/", publicOnly);
            Assert.That(page.Result, Is.EqualTo(CMUNetResolveResult.Found));
            Assert.That(page.Address, Is.EqualTo("colony.seegson.net/housing"));

            Assert.That(net.Resolve("colony.seegson.net/nowhere", publicOnly).Result,
                Is.EqualTo(CMUNetResolveResult.UnknownPage));
            Assert.That(net.Resolve("nothing.here", publicOnly).Result,
                Is.EqualTo(CMUNetResolveResult.UnknownHost));
            Assert.That(net.Resolve("intranet.wy", publicOnly).Result,
                Is.EqualTo(CMUNetResolveResult.AccessDenied));
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
