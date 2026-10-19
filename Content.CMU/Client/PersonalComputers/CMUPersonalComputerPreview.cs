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
        _window.Boot(os, os.Networks, os.HomePage, NoDocuments, "Preview terminal");
    }
}
