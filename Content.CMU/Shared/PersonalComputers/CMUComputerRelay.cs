using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// A terminal the insurgents have bugged with an access tuner, turning it into a relay. From their own hacked
/// terminal they route their deep web traffic through it, so the lookups land on this terminal's log and reach the
/// Marshals and colony security under this terminal's number, location and whoever is logged in here, never the
/// insurgent's. The bug corrupts this terminal's screen more and more until someone formats the terminal with the
/// same tuner, which clears it. Lasts the round or until formatted; a dark or hacked terminal cannot be bugged.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUComputerRelayComponent : Component
{
    /// <summary>When the bug went in. The screen corruption grows with the time since.</summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan PlantedAt;

    /// <summary>How long after planting the screen reaches full corruption.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CorruptionTime = TimeSpan.FromMinutes(8);

    /// <summary>The insurgent faction that planted it, for the relay list. Server only; flavour for the admin log.</summary>
    [ViewVariables]
    public string? Faction;
}

/// <summary>Asks a hacked terminal for the insurgents' live relays, to route the deep traffic through one.</summary>
[Serializable, NetSerializable]
public sealed class CMURelayListRequestMessage : BoundUserInterfaceMessage;

/// <summary>One bugged terminal the insurgents can route through, as the relay list shows it.</summary>
[Serializable, NetSerializable]
public sealed class CMURelayInfo(NetEntity terminal, int terminalId, string location, string? user, bool online, bool routed)
{
    public readonly NetEntity Terminal = terminal;
    public readonly int TerminalId = terminalId;
    public readonly string Location = location;

    /// <summary>Who is logged in at the relay, or null while it waits at its login screen. The one a trace would frame.</summary>
    public readonly string? User = user;

    /// <summary>Powered, switched on and reaching the colony network: only then does routing through it carry.</summary>
    public readonly bool Online = online;

    /// <summary>The hacked terminal asking is routing its deep traffic through this relay right now.</summary>
    public readonly bool Routed = routed;
}

[Serializable, NetSerializable]
public sealed class CMURelayListMessage(List<CMURelayInfo> relays) : BoundUserInterfaceMessage
{
    public readonly List<CMURelayInfo> Relays = relays;
}

/// <summary>Routes this hacked terminal's deep traffic through the given relay, or stops routing when null.</summary>
[Serializable, NetSerializable]
public sealed class CMURelayRouteMessage(NetEntity? terminal) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Terminal = terminal;
}

/// <summary>
/// Turns clean text into bugged text: a share of its letters and digits, growing with the bug's age, become glitch
/// glyphs. The choice is deterministic per character position, so the corruption spreads steadily instead of
/// flickering and never heals. It leaves the page's markup intact: it never touches anything inside an XML tag
/// (<c>&lt;...&gt;</c>), a rich-text tag (<c>[...]</c>) or an entity (<c>&amp;...;</c>), only ever replaces a letter
/// or digit, and never with a glyph that could open a tag, so corrupted markup still parses and renders.
/// </summary>
public static class CMUTextCorruption
{
    // No '<', '>', '[', ']' or '&': a glitch glyph must never be able to start a tag or an entity.
    private const string Glitch = "%@$?/\\~^*_+=";

    /// <summary>At most this share of characters is ever corrupted, so the text stays readable enough to notice.</summary>
    public const float MaxFraction = 0.85f;

    public static string Corrupt(string text, float fraction, int seed)
    {
        if (fraction <= 0f || text.Length == 0)
            return text;

        fraction = Math.Clamp(fraction, 0f, MaxFraction);
        var chars = text.ToCharArray();
        var inAngle = false;   // inside an XML tag <...>
        var inBracket = false; // inside a rich-text tag [...]
        var inEntity = false;  // inside an entity &...;
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            switch (c)
            {
                case '<': inAngle = true; continue;
                case '>': inAngle = false; continue;
                case '[': inBracket = true; continue;
                case ']': inBracket = false; continue;
                case '&': inEntity = true; continue;
                case ';' when inEntity: inEntity = false; continue;
            }

            // Only plain letters and digits in visible text corrupt; markup, punctuation and whitespace are left be.
            if (inAngle || inBracket || inEntity || !char.IsLetterOrDigit(c))
                continue;

            // A stable 0..1 value for this position and terminal; the lowest-valued share of characters corrupts.
            var h = (uint) HashCode.Combine(seed, i);
            if (h / (float) uint.MaxValue < fraction)
                chars[i] = Glitch[(int) (h % (uint) Glitch.Length)];
        }

        return new string(chars);
    }
}
