using Content.Client.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Walks a <see cref="CMUPersonalComputerWindow"/> through both operating systems for <c>cmu.panel_preview=pc</c>,
/// so the splash, desktop and pages can be captured without standing at a desktop. Steps are mirrored into the
/// OS window title for the capture script.
/// </summary>
public sealed class CMUPersonalComputerPreview
{
    private static readonly List<CMUComputerDocument> NoDocuments = new();

    private readonly IClydeWindow _osWindow = IoCManager.Resolve<IClyde>().MainWindow;
    private readonly IPrototypeManager _prototype = IoCManager.Resolve<IPrototypeManager>();
    private readonly ISawmill _log = Logger.GetSawmill("cmu.pc.preview");
    private readonly string _title;
    private readonly (string Name, float Seconds, Action Run)[] _steps;
    private readonly PreviewBoards _boards = new();
    private readonly PreviewDeep _deep = new();
    private CMUPersonalComputerWindow? _window;
    private int _step = -1;
    private System.Numerics.Vector2 _defaultSize;
    private float _timer;

    public CMUPersonalComputerPreview()
    {
        _title = _osWindow.Title;
        _steps = new (string, float, Action)[]
        {
            ("panel-off", 2.5f, () =>
            {
                _deep.PressPower();
                Boot("CMUOsSeegson");
            }),
            ("panel-card-out", 2f, () => _deep.UseReader()),
            ("panel-booting", 1.5f, () => _deep.PressPower()),
            ("panel-login", 9f, () => { }),
            ("panel-card-in", 2.5f, () => _deep.UseReader()),
            ("seegson-rewire", 3.6f, () => Boot("CMUOsSeegson")),
            ("seegson-colonylink", 3.4f, () => { }),
            ("seegson-desktop", 2f, () => { }),
            // The smallest the window can be dragged to, to check nothing on the desktop is cut off.
            ("seegson-min", 2.5f, () =>
            {
                if (_window == null)
                    return;

                _defaultSize = _window.SetSize;
                _window.SetSize = _window.MinSize;
            }),
            ("seegson-max-back", 1f, () =>
            {
                if (_window != null)
                    _window.SetSize = _defaultSize;
            }),
            ("seegson-start-menu", 2f, () => _window?.Desktop?.ToggleStartMenu()),
            ("seegson-browser", 2.5f, () =>
            {
                _window?.Desktop?.ToggleStartMenu();
                _window?.Desktop?.Open(CMUComputerApp.Browser);
            }),
            ("seegson-radio", 3f, () => _window?.Desktop?.Browser?.Navigate("colony.seegson.net/radio")),
            ("seegson-denied", 2f, () => _window?.Desktop?.Browser?.Navigate("intranet.wy")),
            ("seegson-herald", 3f, () => _window?.Desktop?.Browser?.Navigate("herald.colony.news")),
            ("seegson-administration", 3f, () => _window?.Desktop?.Browser?.Navigate("admin.colony.gov")),
            ("seegson-govfor", 3f, () => _window?.Desktop?.Browser?.Navigate("command.govfor.mil")),
            ("seegson-embassy-upp", 3f, () => _window?.Desktop?.Browser?.Navigate("embassy.upp")),
            ("seegson-embassy-twe", 3f, () => _window?.Desktop?.Browser?.Navigate("embassy.twe")),
            ("seegson-embassy-ua", 3f, () => _window?.Desktop?.Browser?.Navigate("embassy.ua")),
            ("seegson-embassy-icsc", 3f, () => _window?.Desktop?.Browser?.Navigate("embassy.icsc")),
            ("seegson-embassy-cca", 3f, () => _window?.Desktop?.Browser?.Navigate("embassy.cca")),
            ("seegson-herald-history", 3f, () => _window?.Desktop?.Browser?.Navigate("herald.colony.news/history")),
            ("seegson-herald-classifieds", 3f, () => _window?.Desktop?.Browser?.Navigate("herald.colony.news/classifieds")),
            ("seegson-administration-charter", 3f, () => _window?.Desktop?.Browser?.Navigate("admin.colony.gov/charter")),
            ("seegson-govfor-garrison", 3f, () => _window?.Desktop?.Browser?.Navigate("command.govfor.mil/garrison")),
            ("seegson-upp-about", 3f, () => _window?.Desktop?.Browser?.Navigate("embassy.upp/about")),
            ("seegson-pop-out", 3f, () => _window?.PopOut()),
            ("seegson-pop-in", 2f, () => _window?.ReturnPopOut()),
            ("seegson-files", 2.5f, () => _window?.Desktop?.Open(CMUComputerApp.Files)),
            ("seegson-system", 2.5f, () => _window?.Desktop?.Open(CMUComputerApp.System)),
            ("weyland-splash", 1.6f, () => Boot("CMUOsWeyland")),
            ("weyland-boot-log", 3.6f, () => { }),
            ("weyland-browser", 2.5f, () => _window?.Desktop?.Open(CMUComputerApp.Browser)),
            ("weyland-memo", 3f, () => _window?.Desktop?.Browser?.Navigate("intranet.wy/memo")),
            ("cmb-splash", 6f, () => Boot("CMUOsCMB")),
            ("cmb-desktop", 3f, () => { }),
            // The law enforcers' casings, each booted to its desktop.
            ("law-cmb", 11f, () => Boot("CMUOsCMB", casing: CMUComputerCasing.Cmb)),
            ("law-security", 11f, () => Boot("CMUOsSeegson", casing: CMUComputerCasing.Security)),
            ("law-rangers", 11f, () => Boot("CMUOsSeegson", casing: CMUComputerCasing.Rangers)),
            ("law-nspa", 11f, () => Boot("CMUOsSeegson", casing: CMUComputerCasing.Nspa)),
            // Colony security's desk on its own network, then a terminal tuned dark.
            ("security-desk", 11f, () => Boot("CMUOsSeegson", casing: CMUComputerCasing.Security, network: "CMUNetSecurity")),
            ("security-browser", 2.5f, () => _window?.Desktop?.Open(CMUComputerApp.Browser)),
            ("security-log", 3f, () => _window?.Desktop?.Browser?.Navigate("security.seegson.net")),
            ("security-map", 3f, () => _window?.Desktop?.Browser?.Navigate("security.seegson.net/map")),
            ("dark-boot", 4.5f, () =>
            {
                _deep.Dark = true;
                Boot("CMUOsSeegson");
            }),
            ("dark-browser", 3f, () => _window?.Desktop?.Open(CMUComputerApp.Browser)),
            ("dark-system", 2.5f, () =>
            {
                _window?.Desktop?.Open(CMUComputerApp.System);
                _deep.Dark = false;
            }),
            ("deep-boot", 4.5f, () => Boot("CMUOsSeegson", true)),
            ("deep-desktop", 2.5f, () => { }),
            ("deep-home", 3f, () => _window?.Desktop?.Open(CMUComputerApp.Deeps)),
            ("deep-ad", 3f, () => _window?.Desktop?.Browser?.Navigate("eb1a-a02d-ans9-202ab-0s9bf-ffa0-l2kc-998d.ad")),
            ("deep-forum", 3f, () => _window?.Desktop?.Browser?.Navigate(_deep.AliasOf("CMUNetSiteDeepDump"))),
            ("deep-eyes", 3f, () => _window?.Desktop?.Browser?.Navigate(_deep.AliasOf("CMUNetSiteDeepEyes"))),
            ("deep-herald-hijacked", 3f, () =>
            {
                _boards.Hijacked = true;
                _window?.Desktop?.Browser?.Navigate("herald.colony.news");
            }),
        };
    }

    public void Update(float frameTime)
    {
        _timer -= frameTime;
        if (_timer > 0 || _step >= _steps.Length - 1)
            return;

        _step++;
        var (name, seconds, run) = _steps[_step];
        _timer = seconds;
        run();
        _log.Info($"step {_step} {name}");
        _osWindow.Title = $"{_title} [pc-preview {_step} {name}]";
    }

    private void Boot(string osId, bool jacked = false, CMUComputerCasing? casing = null, string? network = null)
    {
        _window?.Close();
        var os = _prototype.Index<CMUComputerOsPrototype>(osId);
        var networks = _deep.Dark ? new HashSet<ProtoId<CMUNetworkPrototype>>() : new HashSet<ProtoId<CMUNetworkPrototype>>(os.Networks);
        if (jacked)
            networks.Add("CMUNetDeep");

        if (network != null)
            networks.Add(network);

        _window = new CMUPersonalComputerWindow();
        _window.OpenCenteredAt(new System.Numerics.Vector2(0.32f, 0.1f));
        _window.Boot(os, networks, os.HomePage, NoDocuments, "Preview terminal", _boards, "StableGarrison", "Stable Garrison",
            jacked ? _deep : null, _deep, casing);
    }

    /// <summary>A hacked computer that has found a few things, with links saved to its desktop.</summary>
    private sealed class PreviewDeep : ICMUDeepHost, ICMUComputerHost
    {
        private readonly List<string> _discovered = new();
        private readonly HashSet<ProtoId<CMUNetSitePrototype>> _unlocked = new();
        private readonly Dictionary<ProtoId<CMUNetSitePrototype>, string> _aliases = new();
        private readonly List<CMUNetShortcut> _shortcuts = new();

        public PreviewDeep()
        {
            var rng = new System.Random(1979);
            foreach (var site in IoCManager.Resolve<IPrototypeManager>().EnumeratePrototypes<CMUNetSitePrototype>())
            {
                if (!CMUExtraNetSystem.IsDeep(site))
                    continue;

                _aliases[site.ID] = CMUDeepAddress.NewAlias(rng, site.AliasSuffix);
                if (site.Hidden)
                    _unlocked.Add(site.ID);
            }

            _discovered.Add(AliasOf("CMUNetSiteDeepCell"));
            _discovered.Add("eb1a-a02d-ans9-202ab-0s9bf-ffa0-l2kc-998d.ad");
            _discovered.Add(AliasOf("CMUNetSiteDeepWyLeaks"));
            _shortcuts.Add(new CMUNetShortcut { Address = AliasOf("CMUNetSiteDeepDump"), Title = "THE DUMP" });
            _shortcuts.Add(new CMUNetShortcut { Address = AliasOf("CMUNetSiteDeepEyes"), Title = "EYES" });
            _shortcuts.Add(new CMUNetShortcut { Address = "herald.colony.news", Title = "Colony Herald" });
        }

        public string AliasOf(string site)
        {
            return _aliases.TryGetValue(site, out var alias) ? alias : site;
        }

        public IReadOnlyDictionary<ProtoId<CMUNetSitePrototype>, string> Aliases => _aliases;
        public int TerminalId => 23;
        public string? SessionUser => CardPrototype != null && On ? "Preview" : null;
        public string? SessionJob => "Journalist";
        public bool RequireLogin => true;
        public TimeSpan TraceStarted => TimeSpan.Zero;
        public TimeSpan TraceEnds => TimeSpan.Zero;

        public bool Carrier => true;

        public bool Dark { get; set; }

        public string? Platoon => null;

        public bool On { get; private set; } = true;

        public string? CardPrototype { get; private set; } = "CMIDCardGold";

        public void PressPower()
        {
            On = !On;
        }

        public void PopupShown()
        {
        }

        public IReadOnlyList<string> ConsoleApps { get; } = new List<string> { "colony budget console", "criminal records computer" };

        public void OpenConsoleApp(int app)
        {
        }

        public void UseReader()
        {
            CardPrototype = CardPrototype == null ? "CMIDCardGold" : null;
        }

        public void Trace(NetEntity terminal)
        {
        }

        private readonly List<CMUComputerNote> _notes = new();

        public event Action<CMUMailStateMessage>? MailReceived;
        public event Action<CMUSpaceGptAnswerMessage>? SpaceGptAnswered;
        public event Action<CMUArcadeStateMessage>? ArcadeReceived;
        public event Action<CMUBetStateMessage>? BetsReceived;

        public void RequestArcade(CMUArcadeGame game)
        {
            ArcadeReceived?.Invoke(new CMUArcadeStateMessage(game, new List<CMUArcadeScore> { new("Ann Kowalski", 340), new("Preview", 120) }));
        }

        public void SubmitScore(CMUArcadeGame game, int score)
        {
        }

        public void RequestBets()
        {
            // the preview plays a sprint that started a moment ago, so the arena has something to show
            var now = IoCManager.Resolve<IGameTiming>().CurTime;
            BetsReceived?.Invoke(new CMUBetStateMessage(new List<CMUBetMatch>
            {
                new(3, CMUSport.Sprint, new List<int> { 0, 1, 2, 3, 4 }, new List<float> { 3.1f, 4.4f, 6.0f, 3.8f, 5.2f }, now - TimeSpan.FromSeconds(4), 7, 3, null),
                new(4, CMUSport.Boxing, new List<int> { 5, 6 }, new List<float> { 1.6f, 2.4f }, now + TimeSpan.FromMinutes(2), 0, -1, null),
                new(2, CMUSport.ZeroGBall, new List<int> { 0, 1 }, new List<float> { 1.8f, 2.2f }, TimeSpan.Zero, 11, 0, "3 - 1"),
                new(1, CMUSport.Baseball, new List<int> { 2, 4 }, new List<float> { 2.1f, 1.6f }, TimeSpan.Zero, 5, 1, "2 - 4"),
            }, new List<string>()));
        }

        public void PlaceBet(int match, int entrant, int amount)
        {
        }

        public void AskSpaceGpt(string question)
        {
            SpaceGptAnswered?.Invoke(new CMUSpaceGptAnswerMessage(question, "The signs point to yes.", 3));
        }

        public IReadOnlyList<CMUComputerNote> Notes => _notes;

        public ProtoId<CMUWallpaperPrototype>? Wallpaper { get; private set; }

        public void RequestMail()
        {
            MailReceived?.Invoke(new CMUMailStateMessage(new List<CMUMailMessage>
            {
                new(1, "W-Y Human Resources", "Preview", "Welcome to your colony mailbox", "Your mailbox is ready.", TimeSpan.Zero),
            }, new List<CMUMailMessage>(), new List<string> { "Ann Kowalski", "Simon Hartley" }));
        }

        public void SendMail(string to, string subject, string body)
        {
        }

        public void SaveNote(string name, string text)
        {
            _notes.Add(new CMUComputerNote { Name = name, Author = "Preview", Text = text });
        }

        public void DeleteNote(int index)
        {
            _notes.RemoveAt(index);
        }

        public void SetWallpaper(ProtoId<CMUWallpaperPrototype>? wallpaper)
        {
            Wallpaper = wallpaper;
        }

        public event Action<CMUCmbStateMessage>? CmbReceived;

        public void Visited(string address, bool found)
        {
        }

        public void Logout()
        {
        }

        public event Action<CMUSecurityStateMessage>? SecurityReceived;

        /// <summary>A shift's worth of unauthorized use; the preview has no grid, so the map says so.</summary>
        public void RequestSecurity(CMUSecurityPanel panel)
        {
            var minute = TimeSpan.FromMinutes(1);
            var entries = new List<CMUSecurityEntry>
            {
                new(minute * 48, 31, CMUSecurityEvent.WentDark, "Cargo Bay", "Ann Kowalski"),
                new(minute * 41, 31, CMUSecurityEvent.HackStarted, "Cargo Bay"),
                new(minute * 33, 12, CMUSecurityEvent.DeepLookup, "Bar", "Simon Hartley", "eb1a-a02d-ans9.deep"),
                new(minute * 20, 7, CMUSecurityEvent.LoginRefused, "Colony Administration", "Simon Hartley"),
                new(minute * 9, 7, CMUSecurityEvent.LogWiped, "Colony Administration"),
            };
            SecurityReceived?.Invoke(new CMUSecurityStateMessage(panel, panel == CMUSecurityPanel.Log ? entries : new List<CMUSecurityEntry>(),
                new List<CMUSecurityTerminal>(), null));
        }

        public void RequestCmb(CMUCmbPanel panel)
        {
            CmbReceived?.Invoke(new CMUCmbStateMessage(panel, new List<CMUCmbAlert>(), new List<CMUCmbTerminal>(),
                new List<CMUCmbFax>(), new List<CMUCmbRecord>(), null));
        }

        public IReadOnlyList<string> Discovered => _discovered;
        public IReadOnlySet<ProtoId<CMUNetSitePrototype>> Unlocked => _unlocked;
        public TimeSpan NextScan => TimeSpan.Zero;
        public bool IsInsurgent => true;
        public IReadOnlyList<CMUNetShortcut> Shortcuts => _shortcuts;

        public event Action<CMUDeepPageMessage>? PageReceived;
        public event Action<CMUMarketStateMessage>? MarketReceived;
        public event Action<CMUDeepToolResultMessage>? ToolResult;

        public void RequestMarket()
        {
            MarketReceived?.Invoke(new CMUMarketStateMessage(new List<CMUMarketListing>
            {
                new(1, "greywire", "Spare ID card, blank", "Clean, never swiped.", 400),
            }, new List<CMUMarketPurchase>(), true));
        }

        public void ListItem(string title, string description, int price, string drop)
        {
        }

        public void Buy(int listing)
        {
        }

        public void Sniff(int terminal)
        {
            ToolResult?.Invoke(new CMUDeepToolResultMessage($"Terminal #{terminal}", new List<string> { "Opened herald.colony.news" }));
        }

        public void Loop(string network)
        {
        }

        public void Scan()
        {
        }

        public void RequestPage(string host)
        {
            PageReceived?.Invoke(new CMUDeepPageMessage(host, """
                <Document>
                  <CMUNetStyle Background="#FFF200" Text="#1A1A1A" Heading="#E4002B" Link="#0033CC" Font="Display"/>
                  <CMUNetBlink Rate="0.5">
                    [color=#E4002B][bold]YOU HAVE BEEN SELECTED[/bold][/color]
                  </CMUNetBlink>

                  Sponsored content from hot-synth-4471.ad
                  <CMUNetAd Id="CMUDeepAdPowerLoader"/>

                  <CMUNetSprite Rsi="/Textures/CMU14/PersonalComputers/Sites/Ads/badges.rsi" State="wycoin" Scale="2" Align="Left"/>
                  [netlink="Seegson Working Joe, the helping hand that never tires" link="seegson-joe-221.ad"]

                  [netlink="Aspen, the coldest beer this side of Zeta Reticuli" link="aspen-galley-9011.ad"]
                  <CMUNetAd Id="CMUDeepAdMre" Flip="true"/>
                  <CMUGuideImage Path="/Textures/CMU14/PersonalComputers/Sites/Ads/banner_10.png" MaxHeight="72"/>

                  [netlink="Outer Rim Surplus, uniforms and parts by the crate ::" link="dump.deep"]

                  <CMUNetSprite Rsi="/Textures/CMU14/PersonalComputers/Sites/Ads/badges.rsi" State="joewave" Scale="2" Align="Right"/>
                  [netlink="Transit tickets to the core worlds, ask your depot" link="transit-ticket-5120.ad"]

                  <CMUNetPopup Title="SEEGSON NOTICE" Text="Your Working Joe is due for its service cycle. Book a technician today." Link="seegson-store-404.ad" LinkText="Book a technician" Icon="/Textures/CMU14/PersonalComputers/Sites/Ads/icon_05.png" X="0.85" Y="0.35" Delay="0.5" Background="#FFF200" Accent="#0033CC"/>

                  <CMUNetMarquee Text="BUILDING BETTER WORLDS  ///  NOW ON COLONY SHELVES  ///  SLEEP SOUND, WAKE READY" Speed="70" Color="#0033CC"/>
                </Document>
                """));
        }

        public void OpenCamera(string network)
        {
        }

        public void Hijack(string title, string body)
        {
        }

        public void SaveShortcut(string address, string title)
        {
            _shortcuts.Add(new CMUNetShortcut { Address = address, Title = title });
        }

        public void RemoveShortcut(string address)
        {
            _shortcuts.RemoveAll(s => s.Address == address);
        }
    }

    /// <summary>Fabricated postings, so boards can be looked at without a server round.</summary>
    private sealed class PreviewBoards : ICMUNetBoardHost
    {
        private readonly Dictionary<ProtoId<CMUNetSitePrototype>, List<CMUNetBoardPost>> _posts = new();
        private int _nextId = 100;

        /// <summary>Shows the Herald seized by insurgents.</summary>
        public bool Hijacked;

        public event Action<CMUNetBoardStateMessage>? StateReceived;

        public PreviewBoards()
        {
            var comments = new List<CMUNetBoardComment>
            {
                new("Ellen Ripley", "Colonist", "Took them long enough. The vents on block C smelled for a week.", TimeSpan.FromMinutes(31)),
                new("Dwayne Hicks", "Rifleman", "Patrols will keep an eye on the processor.", TimeSpan.FromMinutes(36)),
            };
            Add("CMUNetSiteColonyHerald", "ATMOSPHERE PROCESSOR BACK TO FULL OUTPUT",
                "Engineers finished repairs on the atmosphere processor early this morning. The metallic smell reported near the residential vents should clear by the end of the shift.\n\nThe Administration thanks residents for their patience.",
                "Ann Kowalski", "Journalist", TimeSpan.FromMinutes(24), comments);
            Add("CMUNetSiteColonyHerald", "MARKET PRICES STEADY", "Ration voucher exchange held at last week's rate.",
                "Ann Kowalski", "Journalist", TimeSpan.FromMinutes(12), new List<CMUNetBoardComment>());
            Add("CMUNetSiteColonyAdministration", "CURFEW EXTENDED TO 2300", "By order of the Colony Administrator, exterior hatches now lock at 2300 colony time.",
                "Simon Hartley", "Colony Administrator", TimeSpan.FromMinutes(18), new List<CMUNetBoardComment>
                {
                    new("Jonesy Mills", "Miner", "Does this cover the mine shift?", TimeSpan.FromMinutes(22)),
                });
            Add("CMUNetSiteGovforCommand", "SECURITY ZONE AROUND THE LANDING PAD",
                "Civilians are not to approach within fifty metres of the landing pad until further notice. Patrols are authorised to detain trespassers.",
                "Maj. R. Dallas", "Commanding Officer", TimeSpan.FromMinutes(15), new List<CMUNetBoardComment>());
            Add("CMUNetSiteDeepDump", "FRESH DUMP #4410",
                "acct 48213 | Ann Kowalski | bal ~4300 | pin 4 _ 7 _\nacct 51920 | Simon Hartley | bal ~9100 | pin _ 2 _ 8",
                "nullbyte", "unknown hacker", TimeSpan.FromMinutes(26), new List<CMUNetBoardComment>
                {
                    new("Unknown", "Colonist", "the second one is good, got in at the canteen ATM", TimeSpan.FromMinutes(29)),
                });
            Add("CMUNetSiteEmbassyUPP", "THE UNION STANDS WITH THE COLONISTS",
                "The Ambassador offers the mission's medical staff to any colonist in need, free of charge.",
                "Ambassador Volkova", "UPP Ambassador", TimeSpan.FromMinutes(20), new List<CMUNetBoardComment>());
        }

        private void Add(string site, string title, string body, string author, string role, TimeSpan time, List<CMUNetBoardComment> comments)
        {
            if (!_posts.TryGetValue(site, out var list))
                _posts[site] = list = new List<CMUNetBoardPost>();

            list.Insert(0, new CMUNetBoardPost(_nextId++, title, body, author, role, time, comments));
        }

        public void Request(ProtoId<CMUNetSitePrototype> site)
        {
            _posts.TryGetValue(site, out var list);
            var hijack = Hijacked && site == "CMUNetSiteColonyHerald"
                ? new CMUNetBoardHijack("Colonial Liberation Front", "THE COMPANY LIES TO YOU",
                    "Every credit you earn here is owed back to them before you see it. Ask the Administrator where the last supply drop went.",
                    TimeSpan.FromHours(1))
                : null;
            StateReceived?.Invoke(new CMUNetBoardStateMessage(site, list ?? new List<CMUNetBoardPost>(), true, true, true, hijack));
        }

        public void Payout(ProtoId<CMUNetSitePrototype> site, int post, int comment)
        {
        }

        public void Withdraw(ProtoId<CMUNetSitePrototype> site, int post)
        {
        }

        public void Post(ProtoId<CMUNetSitePrototype> site, string title, string body, int reward)
        {
            Add(site, title, body, "Preview", "Journalist", TimeSpan.Zero, new List<CMUNetBoardComment>());
            Request(site);
        }

        public void Comment(ProtoId<CMUNetSitePrototype> site, int post, string body)
        {
        }

        public void Delete(ProtoId<CMUNetSitePrototype> site, int post)
        {
        }

        public void Restore(ProtoId<CMUNetSitePrototype> site)
        {
            Hijacked = false;
            Request(site);
        }
    }
}
