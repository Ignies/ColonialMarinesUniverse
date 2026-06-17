using Robust.Shared.GameStates;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Added to an ATM when a CLF Network Tap is installed on it.
///     Captures account data from every card swiped and stores the installer's DNA
///     for forensic investigation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ColonyAtmSkimmerComponent : Component
{
    /// <summary>
    ///     The tap ID that installed this skimmer, used to match the same tap on retrieval.
    /// </summary>
    public int InstallerTapId;

    /// <summary>
    ///     The EntityUid of the person who installed the skimmer.
    /// </summary>
    public EntityUid InstallerEntity;

    /// <summary>
    ///     DNA string of the installer (from DnaComponent), stored at install time.
    ///     Read by forensic scanners.
    /// </summary>
    public string InstallerDna = string.Empty;

    /// <summary>
    ///     Accounts captured since the skimmer was installed.
    /// </summary>
    public List<SkimmedAccount> CapturedAccounts = new();
}

/// <summary>
///     Added to an ATM permanently once it has been tampered with (survives skimmer removal).
///     Causes multitool inspection to warn about tampering.
/// </summary>
[RegisterComponent]
public sealed partial class ColonyAtmTamperedComponent : Component { }
