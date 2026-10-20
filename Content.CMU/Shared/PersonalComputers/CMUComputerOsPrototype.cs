using System.Linq;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// The operating system a personal computer boots: branding, palette, sounds and default networks.
/// </summary>
[Prototype("cmuComputerOs")]
public sealed partial class CMUComputerOsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Product name, e.g. "SEEGSON SYSTEM/3".</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Call the OS after the colony's network instead, the way Sevastopol's is Sevastolink: Stable Garrison
    /// becomes GARRISONLINK. Taken from the round's map name, so a new map needs no setup.
    /// </summary>
    [DataField]
    public bool NamedAfterColony;

    /// <summary>A logo entry that shows the OS name as text, e.g. the colony's network name.</summary>
    public const string NameLogo = "name";

    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "stable", "fort", "port", "new", "old", "upper", "lower", "camp", "station", "outpost", "colony",
    };

    /// <summary>
    /// The colony's network name: its most telling word, possessives and symbols dropped, plus LINK.
    /// Hadley's Hope becomes HADLEYLINK, Stable Garrison becomes GARRISONLINK.
    /// </summary>
    public static string ColonyLink(string mapName)
    {
        var words = mapName
            .Replace("'s", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0)
            .ToList();

        var word = words.FirstOrDefault(w => !Generic.Contains(w)) ?? words.FirstOrDefault() ?? "COLONY";
        return word.ToUpperInvariant() + "LINK";
    }

    /// <summary>Lines printed one by one while the computer boots.</summary>
    [DataField]
    public List<LocId> BootLines = new();

    /// <summary>Address the browser opens on.</summary>
    [DataField(required: true)]
    public string HomePage = string.Empty;

    /// <summary>Networks reached by computers that don't override them.</summary>
    [DataField(required: true)]
    public HashSet<ProtoId<CMUNetworkPrototype>> Networks = new();

    /// <summary>Phosphor colour: default text, links and window chrome.</summary>
    [DataField]
    public Color Phosphor = Color.FromHex("#46FF8E");

    [DataField]
    public Color Background = Color.FromHex("#040805");

    /// <summary>Taskbar, window frames and selection highlight. Defaults to the phosphor colour.</summary>
    [DataField]
    public Color? Accent;

    /// <summary>
    /// Boot splash logos, shown in order; the boot log runs under the last. A PNG is revealed with the CRT
    /// warm up animation; an RSI plays its <see cref="LogoState"/>. A missing file is skipped, and with none
    /// the OS name is animated instead, so a logo can be dropped in later. Strings rather than ResPaths so
    /// the YAML linter accepts a logo that has not been drawn yet.
    /// </summary>
    [DataField]
    public List<string> Logos = new();

    [DataField]
    public string LogoState = "logo";

    /// <summary>Short brand shown on the start button.</summary>
    [DataField(required: true)]
    public LocId StartLabel;

    /// <summary>Optional desktop wallpaper texture. Without one the desktop is a flat field with the OS name.</summary>
    [DataField]
    public ResPath? Wallpaper;

    /// <summary>Documents every computer running this OS has in Files.</summary>
    [DataField]
    public List<CMUComputerDocument> Documents = new();

    /// <summary>The casing drawn round the screen: Seegson beige, Weyland-Yutani gunmetal or Marshals navy.</summary>
    [DataField]
    public CMUComputerCasing Casing = CMUComputerCasing.Seegson;

    [DataField]
    public SoundSpecifier? BootSound;

    [DataField]
    public SoundSpecifier? KeySound;

    [DataField]
    public SoundSpecifier? ClickSound;

    /// <summary>The machine running, looped from the end of the boot sound until it is switched off.</summary>
    [DataField]
    public SoundSpecifier? RunningSound;

    /// <summary>How long the boot sound plays before the running loop takes over, in seconds.</summary>
    [DataField]
    public float BootSoundLength = 9.6f;

    [DataField]
    public SoundSpecifier? ShutdownSound;

    /// <summary>Over the boot sound on a hacked terminal, whose boot never comes up clean.</summary>
    [DataField]
    public SoundSpecifier? GlitchSound;

    /// <summary>New mail in the inbox.</summary>
    [DataField]
    public SoundSpecifier? MailSound;

    /// <summary>An ad's pop up jumping onto the page.</summary>
    [DataField]
    public SoundSpecifier? ErrorSound;
}

/// <summary>
/// The machine a computer is built into: its OS's by default, or the computer's own. Each has its own art in
/// Textures/CMU14/PersonalComputers/Machine/, named after it in lower case.
/// </summary>
public enum CMUComputerCasing : byte
{
    /// <summary>Seegson beige, the colony's own machines.</summary>
    Seegson,

    /// <summary>Weyland-Yutani gunmetal with gold trim.</summary>
    Weyland,

    /// <summary>Colonial Marshal Bureau navy with silver trim and gold stars.</summary>
    Cmb,

    /// <summary>Colony security's pale grey with the blue and white police checker.</summary>
    Security,

    /// <summary>Colonial Wilderness Preservation Service rangers' forest green with khaki trim.</summary>
    Rangers,

    /// <summary>Neroid Sector Policing Authority black with red trim.</summary>
    Nspa,
}
