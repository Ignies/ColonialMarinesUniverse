using Content.Shared.CMU14.PersonalComputers;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>Arcade games downloaded to this computer's desktop.</summary>
public sealed partial class CMUPersonalComputerBui
{
    private static readonly List<CMUArcadeGame> NoDownloads = new();

    public IReadOnlyList<CMUArcadeGame> Downloads =>
        EntMan.TryGetComponent(Owner, out CMUPersonalComputerComponent? computer) ? computer.Downloads : NoDownloads;

    public void DownloadGame(CMUArcadeGame game)
    {
        SendMessage(new CMUGameDownloadMessage(game));
    }

    public void DeleteGame(CMUArcadeGame game)
    {
        SendMessage(new CMUGameDeleteMessage(game));
    }
}
