using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

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
    private CMUPersonalComputerWindow? _window;
    private int _step = -1;
    private float _timer;

    public CMUPersonalComputerPreview()
    {
        _title = _osWindow.Title;
        _steps = new (string, float, Action)[]
        {
            ("seegson-rewire", 3.6f, () => Boot("CMUOsSeegson")),
            ("seegson-sevastolink", 3.4f, () => { }),
            ("seegson-desktop", 2f, () => { }),
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
            ("seegson-pop-out", 3f, () => _window?.PopOut()),
            ("seegson-pop-in", 2f, () => _window?.ReturnPopOut()),
            ("seegson-files", 2.5f, () => _window?.Desktop?.Open(CMUComputerApp.Files)),
            ("seegson-system", 2.5f, () => _window?.Desktop?.Open(CMUComputerApp.System)),
            ("weyland-splash", 1.6f, () => Boot("CMUOsWeyland")),
            ("weyland-boot-log", 3.6f, () => { }),
            ("weyland-browser", 2.5f, () => _window?.Desktop?.Open(CMUComputerApp.Browser)),
            ("weyland-memo", 3f, () => _window?.Desktop?.Browser?.Navigate("intranet.wy/memo")),
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

    private void Boot(string osId)
    {
        _window?.Close();
        var os = _prototype.Index<CMUComputerOsPrototype>(osId);
        _window = new CMUPersonalComputerWindow();
        _window.OpenCentered();
        _window.Boot(os, os.Networks, os.HomePage, NoDocuments, "Preview terminal", _boards);
    }

    /// <summary>Fabricated postings, so boards can be looked at without a server round.</summary>
    private sealed class PreviewBoards : ICMUNetBoardHost
    {
        private readonly Dictionary<ProtoId<CMUNetSitePrototype>, List<CMUNetBoardPost>> _posts = new();
        private int _nextId = 100;

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
            StateReceived?.Invoke(new CMUNetBoardStateMessage(site, list ?? new List<CMUNetBoardPost>(), true, true, true));
        }

        public void Post(ProtoId<CMUNetSitePrototype> site, string title, string body)
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
    }
}
