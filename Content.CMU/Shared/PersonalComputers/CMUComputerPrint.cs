using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>Prints a note from Files onto paper, which comes out of the terminal's printer.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerPrintNoteMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

/// <summary>Tells whoever printed that the paper is coming out, for the screen to show it.</summary>
[Serializable, NetSerializable]
public sealed class CMUComputerPrintedMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

public static class CMUComputerPrinting
{
    /// <summary>How long a terminal's printer takes between sheets.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    /// <summary>Desk machines have a printer; laptops do not.</summary>
    public static bool CanPrint(CMUComputerCasing casing)
    {
        return casing != CMUComputerCasing.Laptop;
    }
}
