using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// Arcade games downloaded from the ExtraNet to a computer's desktop, kept in
/// <see cref="CMUPersonalComputerComponent.Downloads"/> for the round like saved links.
/// </summary>
public static class CMUGameDownloads
{
    /// <summary>What a downloaded game's desktop icon shows as its address, e.g. <c>game://HuggerRun</c>.</summary>
    public static string Address(CMUArcadeGame game)
    {
        return "game://" + game;
    }
}

/// <summary>Puts an arcade game on this computer's desktop, once its download has finished.</summary>
[Serializable, NetSerializable]
public sealed class CMUGameDownloadMessage(CMUArcadeGame game) : BoundUserInterfaceMessage
{
    public readonly CMUArcadeGame Game = game;
}

/// <summary>Deletes a downloaded game from this computer's desktop.</summary>
[Serializable, NetSerializable]
public sealed class CMUGameDeleteMessage(CMUArcadeGame game) : BoundUserInterfaceMessage
{
    public readonly CMUArcadeGame Game = game;
}
