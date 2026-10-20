using Content.Server.Administration.Logs;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Moves terminals between the colony network and dark. The access tuner does the tuning (see
/// <see cref="DeepWeb.CMUDeepWebSystem.TryStartRetune"/>); this applies the result. A dark terminal keeps its
/// files, notes and log and reaches the deep web if it is hacked, but no colony network, and nothing it does
/// reaches security or the Marshals. Corporate and bureau networks run on their own keyed lines, so a tuner can
/// take a terminal off the colony carrier or put it back, never onto someone else's network.
/// </summary>
public sealed class CMUComputerRetuneSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private CMUCmbSystem _cmb = default!;
    [Dependency] private CMUComputerSessionSystem _sessions = default!;
    [Dependency] private CMUSecurityNetSystem _security = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>
    /// Takes a terminal dark or brings it back. Security hears of it either way: going dark is the last thing the
    /// terminal reports. The terminal reboots onto its new carrier, closing its screen for whoever was at it.
    /// </summary>
    public bool SetDark(Entity<CMUPersonalComputerComponent?> terminal, bool dark, EntityUid? user = null)
    {
        if (!Resolve(terminal, ref terminal.Comp, false) || terminal.Comp.Dark == dark)
            return false;

        var computer = new Entity<CMUPersonalComputerComponent>(terminal, terminal.Comp);
        if (dark)
        {
            _security.Report(terminal, CMUSecurityEvent.WentDark);
            _cmb.CutTrace(computer);
        }

        terminal.Comp.Dark = dark;
        Dirty(computer);
        if (!dark)
            _security.Report(terminal, CMUSecurityEvent.Rejoined);

        _sessions.AddLog(computer, Loc.GetString(dark ? "cmu-pc-log-dark" : "cmu-pc-log-rejoined"));
        _ui.CloseUi(terminal.Owner, CMUPersonalComputerUiKey.Key);

        if (user is not { } actor)
            return true;

        _popup.PopupEntity(Loc.GetString(dark ? "cmu-pc-retune-dark-done" : "cmu-pc-retune-colony-done"), terminal, actor, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(actor):player} retuned terminal #{terminal.Comp.TerminalId} {(dark ? "off the colony network" : "back onto the colony network")}");
        return true;
    }
}
