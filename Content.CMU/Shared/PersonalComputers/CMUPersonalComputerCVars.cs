using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// Switches for every personal computer feature, so any of them can be turned off mid round from the console,
/// e.g. <c>cvar cmu.pc.deep_web false</c>. They replicate to clients, which hide what is switched off.
/// </summary>
[CVarDefs]
public sealed class CMUPersonalComputerCVars
{
    /// <summary>Every personal computer. Off: terminals show as offline and nothing below runs.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("cmu.pc.enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Colony consoles run the PC OS, with their own screens as programs on its desktop. Off: every bridged console
    /// goes straight back to opening its own screen, as before; consoles need no changes either way.
    /// </summary>
    public static readonly CVarDef<bool> Bridge =
        CVarDef.Create("cmu.pc.bridge", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Terminals need the area's equipment power to work.</summary>
    public static readonly CVarDef<bool> NeedsPower =
        CVarDef.Create("cmu.pc.needs_power", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The colony network rides on the sensor tower: with every tower on the colony down, terminals only reach their
    /// own files. A colony without a tower keeps its network.
    /// </summary>
    public static readonly CVarDef<bool> NeedsTower =
        CVarDef.Create("cmu.pc.needs_tower", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Round postings and comments on board sites.</summary>
    public static readonly CVarDef<bool> Boards =
        CVarDef.Create("cmu.pc.boards", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Saving pages as desktop links.</summary>
    public static readonly CVarDef<bool> Shortcuts =
        CVarDef.Create("cmu.pc.shortcuts", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>ID card logins. Off: terminals open straight to the desktop.</summary>
    public static readonly CVarDef<bool> Login =
        CVarDef.Create("cmu.pc.login", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Terminal logs read with a multitool.</summary>
    public static readonly CVarDef<bool> Logs =
        CVarDef.Create("cmu.pc.logs", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Hacking terminals into the deep web, The deeps.exi, scans and the ad maze.</summary>
    public static readonly CVarDef<bool> DeepWeb =
        CVarDef.Create("cmu.pc.deep_web", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Random ATM and wanted list leaks on the deep web's leak boards.</summary>
    public static readonly CVarDef<bool> Leaks =
        CVarDef.Create("cmu.pc.leaks", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Insurgents seizing the Herald's front page.</summary>
    public static readonly CVarDef<bool> Hijack =
        CVarDef.Create("cmu.pc.hijack", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Camera grids tapped from eyes on the deep web.</summary>
    public static readonly CVarDef<bool> CameraTap =
        CVarDef.Create("cmu.pc.camera_tap", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Cash bounties on bounty boards.</summary>
    public static readonly CVarDef<bool> Bounties =
        CVarDef.Create("cmu.pc.bounties", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>The Herald wire: short automatic news items about what happens in the round.</summary>
    public static readonly CVarDef<bool> Wire =
        CVarDef.Create("cmu.pc.wire", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Deep forums reacting to the round: sightings of the red dog, missing colonists.</summary>
    public static readonly CVarDef<bool> LoreEvents =
        CVarDef.Create("cmu.pc.lore_events", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>The Company intranet putting a cheerful spin on every bad thing that happens.</summary>
    public static readonly CVarDef<bool> CompanySpin =
        CVarDef.Create("cmu.pc.company_spin", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Marshals tracing a terminal from the map to find who is logged in.</summary>
    public static readonly CVarDef<bool> Trace =
        CVarDef.Create("cmu.pc.trace", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Skilled engineers wiping or forging terminal logs.</summary>
    public static readonly CVarDef<bool> LogTamper =
        CVarDef.Create("cmu.pc.log_tamper", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Mail between colonists, and the Company and spammers mailing them.</summary>
    public static readonly CVarDef<bool> Mail =
        CVarDef.Create("cmu.pc.mail", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Notes saved in a terminal's Files program.</summary>
    public static readonly CVarDef<bool> Notes =
        CVarDef.Create("cmu.pc.notes", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Wallpapers found on the ExtraNet.</summary>
    public static readonly CVarDef<bool> Wallpapers =
        CVarDef.Create("cmu.pc.wallpapers", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Arcade games with round high score boards.</summary>
    public static readonly CVarDef<bool> Arcade =
        CVarDef.Create("cmu.pc.arcade", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Betting ATM credits on zero-g ball matches.</summary>
    public static readonly CVarDef<bool> Betting =
        CVarDef.Create("cmu.pc.betting", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>The insurgents' contraband market on the deep web.</summary>
    public static readonly CVarDef<bool> Market =
        CVarDef.Create("cmu.pc.market", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Hacker tools on hacked terminals: packet sniffer and camera loop.</summary>
    public static readonly CVarDef<bool> HackerTools =
        CVarDef.Create("cmu.pc.hacker_tools", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>The Marshals' bureau network: lookup alerts, terminal map, fax copies and records.</summary>
    public static readonly CVarDef<bool> Cmb =
        CVarDef.Create("cmu.pc.cmb", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Retuning a terminal with an access tuner, between the colony network and dark. Off: the option is gone and
    /// terminals already dark are back on the colony network.
    /// </summary>
    public static readonly CVarDef<bool> Retune =
        CVarDef.Create("cmu.pc.retune", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>The colony security network: the terminal map and the unauthorized use log.</summary>
    public static readonly CVarDef<bool> SecurityNet =
        CVarDef.Create("cmu.pc.security_net", true, CVar.SERVER | CVar.REPLICATED);
}
