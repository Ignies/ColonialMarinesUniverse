using Content.Shared.CMU14.PersonalComputers;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// What the desktop and browser need from the computer they run on: the session, saved links, page visit
/// reports and the Marshals' panels. The BUI implements it; previews fake it.
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

    IReadOnlyList<CMUNetShortcut> Shortcuts { get; }

    event Action<CMUCmbStateMessage>? CmbReceived;

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

    void RequestMail();

    void SendMail(string to, string subject, string body);

    void SaveNote(string name, string text);

    void DeleteNote(int index);

    void SetWallpaper(ProtoId<CMUWallpaperPrototype>? wallpaper);

    void AskSpaceGpt(string question);

    void RequestArcade(CMUArcadeGame game);

    void SubmitScore(CMUArcadeGame game, int score);

    void RequestBets();

    void PlaceBet(int match, bool home, int amount);
}
