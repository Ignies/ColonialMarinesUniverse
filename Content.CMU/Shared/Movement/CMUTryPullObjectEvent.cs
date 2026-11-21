namespace Content.Shared.CMU14.Movement;

/// <summary>
/// Raised on whatever someone Ctrl+clicks to pull, before the pull is tried. Set <see cref="Handled"/>
/// to use the click for something else, such as bracing against a vehicle to push it.
/// </summary>
[ByRefEvent]
public record struct CMUTryPullObjectEvent(EntityUid User, bool Handled = false);
