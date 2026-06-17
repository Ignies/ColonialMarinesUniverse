using Robust.Shared.GameStates;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Marks a console entity as an admin bank terminal.
///     Provides full account/transaction visibility and admin operations.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ColonyBankAdminComponent : Component { }
