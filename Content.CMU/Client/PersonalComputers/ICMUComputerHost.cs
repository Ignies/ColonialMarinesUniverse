using Content.Shared.CMU14.PersonalComputers;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// What the desktop and browser need from the computer they run on: the session, saved links, page visit
/// reports, and the Marshals' and colony security's panels. The BUI implements it; previews fake it.
/// </summary>
public interface ICMUComputerHost
{
    int TerminalId { get; }

    /// <summary>Who is logged in, or null while the terminal waits for an ID card.</summary>
    string? SessionUser { get; }

    string? SessionJob { get; }

    bool RequireLogin { get; }

    /// <summary>A Marshal trace on this terminal, or zero when nobody is tracing it.</summary>
    TimeSpan TraceStarted { get; }

    TimeSpan TraceEnds { get; }

    /// <summary>The round's GOVFOR platoon id, for sites with platoon pages; null before one is picked.</summary>
    string? Platoon { get; }

    /// <summary>Whether the terminal reaches the network: a colony sensor tower is up.</summary>
    bool Carrier { get; }

    /// <summary>Tuned off the colony network with an access tuner: no colony sites, nothing reported.</summary>
    bool Dark { get; }

    /// <summary>Switched on with the front panel's power button.</summary>
    bool On { get; }

    /// <summary>The card in the reader, for the panel to draw; null when it is empty.</summary>
    string? CardPrototype { get; }

    void PressPower();

    /// <summary>How long ago the machine was switched on; zero when it just was.</summary>
    TimeSpan SinceSwitchedOn { get; }

    /// <summary>The programs left open on the terminal's screen this session.</summary>
    IReadOnlyList<CMUComputerScreenWindow> Screen { get; }

    /// <summary>Tells the terminal what is on screen now.</summary>
    void SaveScreen(List<CMUComputerScreenWindow> screen);

    /// <summary>An ad's pop up jumped onto a page.</summary>
    void PopupShown();

    /// <summary>A console's own screens, as program names; empty on a plain desktop.</summary>
    IReadOnlyList<string> ConsoleApps { get; }

    /// <summary>Opens the console screen at this index in a window of its own.</summary>
    void OpenConsoleApp(int app);

    /// <summary>Puts the user's card in the reader, or takes the one inside out.</summary>
    void UseReader();

    IReadOnlyList<CMUNetShortcut> Shortcuts { get; }

    event Action<CMUCmbStateMessage>? CmbReceived;

    event Action<CMUSecurityStateMessage>? SecurityReceived;

    event Action<CMUMailStateMessage>? MailReceived;

    event Action<CMUSpaceGptAnswerMessage>? SpaceGptAnswered;

    event Action<CMUArcadeStateMessage>? ArcadeReceived;

    event Action<CMUBetStateMessage>? BetsReceived;

    IReadOnlyList<CMUComputerNote> Notes { get; }

    /// <summary>A wallpaper found on the ExtraNet and put on this desktop, or null for the OS one.</summary>
    ProtoId<CMUWallpaperPrototype>? Wallpaper { get; }

    void SaveShortcut(string address, string title);

    void RemoveShortcut(string address);

    /// <summary>Reports a page the browser opened, or failed to.</summary>
    void Visited(string address, bool found);

    void Logout();

    void RequestCmb(CMUCmbPanel panel);

    void Trace(NetEntity terminal);

    void RequestSecurity(CMUSecurityPanel panel);

    void RequestMail();

    void SendMail(string to, string subject, string body);

    void SaveNote(string name, string text);

    void DeleteNote(int index);

    void SetWallpaper(ProtoId<CMUWallpaperPrototype>? wallpaper);

    void AskSpaceGpt(string question);

    void RequestArcade(CMUArcadeGame game);

    void SubmitScore(CMUArcadeGame game, int score);

    void RequestBets();

    void PlaceBet(int match, int entrant, int amount);
}
