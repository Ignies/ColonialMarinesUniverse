using Robust.Shared.GameStates;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Component on the CLF Network Tap handheld device.
///     First use on an ATM installs a skimmer. Second use retrieves data.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ClfNetworkTapComponent : Component
{
    /// <summary>
    ///     Unique ID for this tap, used to match it to an installed skimmer.
    ///     Assigned on MapInit / spawn.
    /// </summary>
    [DataField]
    public int TapId;

    /// <summary>
    ///     The ATM this tap is currently linked to (skimmer installed).
    /// </summary>
    public EntityUid? LinkedAtm;
}
