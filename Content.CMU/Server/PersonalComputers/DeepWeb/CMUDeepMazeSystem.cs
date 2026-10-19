using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Text;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// The ad maze that hides the deep web's hidden sites. Each round lays out a short chain of ad pages for every
/// hidden site, the last one linking to the site. Every other ad page is junk made up from its address: more
/// ads linking to more ads, now and then back into a chain. On a chain page one link is the door, marked with
/// <see cref="DoorMark"/>. Reaching the page that links to a hidden site unlocks the site on that computer.
/// Ad pages only exist on the server and reach the browser as page markup.
/// </summary>
public sealed partial class CMUDeepMazeSystem : EntitySystem
{
    private static readonly ProtoId<LocalizedDatasetPrototype> Slogans = "CMUDeepAdSlogans";
    private static readonly ProtoId<LocalizedDatasetPrototype> Headlines = "CMUDeepAdHeadlines";
    private static readonly ProtoId<LocalizedDatasetPrototype> PopupTitles = "CMUDeepAdPopupTitles";
    private static readonly ProtoId<LocalizedDatasetPrototype> PopupTexts = "CMUDeepAdPopupTexts";
    private static readonly ProtoId<LocalizedDatasetPrototype> PopupActions = "CMUDeepAdPopupActions";

    /// <summary>
    /// Ad art: every <c>banner_*.png</c> and <c>icon_*.png</c> in this folder is used, and every state of every
    /// RSI in it as an animation, so new art needs no code.
    /// </summary>
    private static readonly ResPath[] ArtFolders =
    {
        new("/Textures/CMU14/PersonalComputers/Sites/Ads/"),
        new("/Textures/CMU14/PersonalComputers/Retro/"),
    };

    /// <summary>Ends the one link on a chain page that leads on. The deeps.exi home hints at it.</summary>
    public const string DoorMark = " ::";

    /// <summary>Chance that a junk page links back into some chain, so a lost visitor can find the way again.</summary>
    private const double ChainFallback = 0.3;

    private static readonly (string Background, string Text, string Heading, string Link, string Font)[] Palettes =
    {
        ("#FFF200", "#1A1A1A", "#E4002B", "#0033CC", "Display"),
        ("#0B0B2E", "#E0E0FF", "#FF2FD0", "#3FF2FF", "Pixel"),
        ("#1E0000", "#FFD0D0", "#FF3030", "#FFE600", "Sans"),
        ("#00140A", "#B8FFB8", "#7CFF00", "#FF9AE6", "Terminal"),
        ("#F2F2F2", "#202020", "#FF5A00", "#6A00FF", "Sans"),
        ("#2B0033", "#F6D6FF", "#FFB000", "#00FFC8", "Display"),
    };

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private CMUDeepAliasSystem _aliases = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private bool _built;
    private int _seed;
    private readonly Dictionary<string, string> _doors = new();
    private readonly Dictionary<string, ProtoId<CMUNetSitePrototype>> _ends = new();
    private readonly Dictionary<ProtoId<CMUNetSitePrototype>, List<string>> _chains = new();
    private readonly List<string> _entries = new();
    private readonly List<string> _chainPages = new();
    private readonly List<string> _banners = new();
    private readonly List<string> _icons = new();
    private readonly List<string> _ads = new();
    private readonly List<(ResPath Rsi, string State, int Scale)> _animations = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        Subs.BuiEvents<CMUDeepWebAccessComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUDeepPageRequestMessage>(OnRequest);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _built = false;
        _doors.Clear();
        _ends.Clear();
        _chains.Clear();
        _entries.Clear();
        _chainPages.Clear();
    }

    /// <summary>The first page of every chain, which scans can turn up.</summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            Build();
            return _entries;
        }
    }

    /// <summary>This round's chain of ad pages leading to a hidden site, first page first.</summary>
    public IReadOnlyList<string> ChainTo(ProtoId<CMUNetSitePrototype> site)
    {
        Build();
        return _chains.TryGetValue(site, out var chain) ? chain : Array.Empty<string>();
    }

    /// <summary>A junk ad page no chain goes through.</summary>
    public string JunkHost()
    {
        Build();
        return NewHost(new System.Random(_random.Next()));
    }

    private void Build()
    {
        if (_built)
            return;

        _built = true;
        _seed = _random.Next();
        _banners.Clear();
        _icons.Clear();
        _animations.Clear();
        foreach (var file in ArtFolders.SelectMany(folder => _resources.ContentFindFiles(folder)))
        {
            if (file.Filename == "meta.json")
            {
                AddAnimations(file);
                continue;
            }

            if (file.Extension != "png" || file.Directory.Extension == "rsi")
                continue;

            if (file.Filename.StartsWith("banner_", StringComparison.Ordinal))
                _banners.Add(file.ToString());
            else if (file.Filename.StartsWith("icon_", StringComparison.Ordinal))
                _icons.Add(file.ToString());
        }

        _banners.Sort(StringComparer.Ordinal);
        _ads.Clear();
        foreach (var ad in _prototype.EnumeratePrototypes<CMUDeepAdPrototype>())
            _ads.Add(ad.ID);

        _ads.Sort(StringComparer.Ordinal);
        _animations.Sort((a, b) => string.CompareOrdinal($"{a.Rsi}/{a.State}", $"{b.Rsi}/{b.State}"));
        _icons.Sort(StringComparer.Ordinal);
        foreach (var site in _prototype.EnumeratePrototypes<CMUNetSitePrototype>())
        {
            if (!site.Hidden)
                continue;

            var rng = new System.Random(_random.Next());
            var chain = new List<string>();
            var length = _random.Next(2, 4);
            for (var i = 0; i < length; i++)
                chain.Add(NewHost(rng));

            for (var i = 0; i < chain.Count; i++)
                _doors[chain[i]] = i + 1 < chain.Count ? chain[i + 1] : _aliases.GetAlias(site);

            _ends[chain[^1]] = site.ID;
            _chains[site.ID] = chain;
            _entries.Add(chain[0]);
            _chainPages.AddRange(chain);
        }
    }

    private void OnRequest(Entity<CMUDeepWebAccessComponent> ent, ref CMUDeepPageRequestMessage args)
    {
        var host = args.Host.Trim().ToLowerInvariant();
        if (!CMUDeepAddress.IsAd(host) || !_cfg.GetCVar(CMUPersonalComputerCVars.DeepWeb))
            return;

        var markup = Generate(host);
        if (_ends.TryGetValue(host, out var site) && ent.Comp.Unlocked.Add(site))
            Dirty(ent);

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key, new CMUDeepPageMessage(host, markup), args.Actor);
    }

    /// <summary>The page at an ad host. The same host gives the same page for the whole round.</summary>
    public string Generate(string host)
    {
        Build();
        var rng = new System.Random(_seed ^ StableHash(host));
        var (background, text, heading, link, font) = Palettes[rng.Next(Palettes.Length)];

        var links = new List<(string Text, string Address)>();
        var count = rng.Next(5, 9);
        for (var i = 0; i < count; i++)
            links.Add((Slogan(rng), NewHost(rng)));

        if (_chainPages.Count > 0 && rng.NextDouble() < ChainFallback)
            links[rng.Next(links.Count)] = (Slogan(rng), _chainPages[rng.Next(_chainPages.Count)]);

        if (_doors.TryGetValue(host, out var door))
            links.Insert(rng.Next(links.Count + 1), (Slogan(rng) + DoorMark, door));

        var marquee = $"{Slogan(rng)}  ///  {Slogan(rng)}  ///  {Slogan(rng)}";
        var headlines = _prototype.Index(Headlines).Values;
        var headline = Loc.GetString(headlines[rng.Next(headlines.Count)]);

        var page = new StringBuilder();
        page.AppendLine("<Document>");
        page.AppendLine($"  <CMUNetStyle Background=\"{background}\" Text=\"{text}\" Heading=\"{heading}\" Link=\"{link}\" Font=\"{font}\"/>");
        var rate = (0.35 + rng.NextDouble() * 0.5).ToString("0.00", CultureInfo.InvariantCulture);
        page.AppendLine($"  <CMUNetBlink Rate=\"{rate}\">");
        page.AppendLine($"    [color={heading}][bold]{headline}[/bold][/color]");
        page.AppendLine("  </CMUNetBlink>");
        page.AppendLine();
        page.AppendLine($"  {Loc.GetString("cmu-deep-ad-sponsored", ("host", host))}");
        page.AppendLine(Ad(rng, 96));

        // Banners and badges are scattered between the links, so every page looks different.
        for (var i = 0; i < links.Count; i++)
        {
            var (label, address) = links[i];
            page.AppendLine();
            if (_animations.Count > 0 && rng.NextDouble() < 0.3)
            {
                var (rsi, state, scale) = _animations[rng.Next(_animations.Count)];
                var align = rng.Next(3) switch { 0 => "Left", 1 => "Center", _ => "Right" };
                page.AppendLine($"  <CMUNetSprite Rsi=\"{rsi}\" State=\"{state}\" Scale=\"{scale}\" Align=\"{align}\"/>");
            }

            page.AppendLine($"  [netlink=\"{label}\" link=\"{address}\"]");
            if (rng.NextDouble() < 0.3)
                page.AppendLine(Ad(rng, 72));
        }

        // One or two pop ups over the page, each leading to yet another ad.
        var popups = rng.Next(1, 3);
        for (var i = 0; i < popups; i++)
            page.AppendLine(Popup(rng, background, link));

        page.AppendLine();
        page.AppendLine($"  <CMUNetMarquee Text=\"{marquee}\" Speed=\"{rng.Next(50, 110)}\" Color=\"{link}\"/>");
        page.AppendLine("</Document>");
        return page.ToString();
    }

    /// <summary>
    /// An ad block: mostly a product ad drawn from the item's own sprite, sometimes one of the painted banners
    /// for brands with no item in the game.
    /// </summary>
    private string Ad(System.Random rng, int bannerHeight)
    {
        if (_ads.Count > 0 && (_banners.Count == 0 || rng.NextDouble() < 0.7))
        {
            var flip = rng.Next(2) == 0 ? "true" : "false";
            return $"  <CMUNetAd Id=\"{_ads[rng.Next(_ads.Count)]}\" Flip=\"{flip}\"/>";
        }

        return _banners.Count > 0
            ? $"  <CMUGuideImage Path=\"{_banners[rng.Next(_banners.Count)]}\" MaxHeight=\"{bannerHeight}\"/>"
            : string.Empty;
    }

    private string Popup(System.Random rng, string background, string accent)
    {
        var titles = _prototype.Index(PopupTitles).Values;
        var texts = _prototype.Index(PopupTexts).Values;
        var actions = _prototype.Index(PopupActions).Values;
        var icon = _icons.Count > 0 ? $" Icon=\"{_icons[rng.Next(_icons.Count)]}\"" : string.Empty;
        var culture = CultureInfo.InvariantCulture;
        return $"  <CMUNetPopup Title=\"{Loc.GetString(titles[rng.Next(titles.Count)])}\"" +
            $" Text=\"{Loc.GetString(texts[rng.Next(texts.Count)])}\"" +
            $" Link=\"{NewHost(rng)}\" LinkText=\"{Loc.GetString(actions[rng.Next(actions.Count)])}\"{icon}" +
            $" X=\"{rng.NextDouble().ToString("0.00", culture)}\" Y=\"{(rng.NextDouble() * 0.8).ToString("0.00", culture)}\"" +
            $" Delay=\"{(0.5 + rng.NextDouble() * 3).ToString("0.0", culture)}\"" +
            $" Background=\"{background}\" Accent=\"{accent}\"/>";
    }

    /// <summary>Every state of an RSI in the art folder becomes an animation ad pages can show.</summary>
    private void AddAnimations(ResPath meta)
    {
        try
        {
            using var stream = _resources.ContentFileRead(meta);
            using var json = JsonDocument.Parse(stream);

            // Small badges are drawn twice their size, bigger art at its own size.
            var width = json.RootElement.GetProperty("size").GetProperty("x").GetInt32();
            var scale = width >= 64 && width < 96 ? 1 : 2;
            foreach (var state in json.RootElement.GetProperty("states").EnumerateArray())
                _animations.Add((meta.Directory, state.GetProperty("name").GetString()!, scale));
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Error($"Could not read ad animations from {meta}: {e.Message}");
        }
    }

    private string Slogan(System.Random rng)
    {
        var slogans = _prototype.Index(Slogans).Values;
        return Loc.GetString(slogans[rng.Next(slogans.Count)]);
    }

    private static string NewHost(System.Random rng)
    {
        return CMUDeepAddress.NewAlias(rng, CMUDeepAddress.AdSuffix);
    }

    /// <summary>FNV-1a, so a host maps to the same page however often the server restarts the hash seed.</summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = (int) 2166136261;
            foreach (var c in text)
                hash = (hash ^ c) * 16777619;

            return hash;
        }
    }
}
