using System.Text;
using Content.Shared.Camera;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// A computer that has been hacked into the deep web: it reaches <see cref="Network"/>, boots with a corrupted
/// loader and gets The deeps.exi. Added by the server when the hack minigame is solved; lasts the round.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUDeepWebAccessComponent : Component
{
    [DataField]
    public ProtoId<CMUNetworkPrototype> Network = "CMUNetDeep";

    /// <summary>Addresses this computer has turned up with a scan, deep sites or ad pages, in the order found.</summary>
    [DataField, AutoNetworkedField]
    public List<string> Discovered = new();

    /// <summary>This round's random address of every deep site. Filled in by the server when the computer is hacked.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<CMUNetSitePrototype>, string> Aliases = new();

    /// <summary>Hidden sites this computer has reached a link to through the ad maze. Only these open.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<CMUNetSitePrototype>> Unlocked = new();

    /// <summary>The hidden site whose links open camera feeds, once it is unlocked.</summary>
    [DataField]
    public ProtoId<CMUNetSitePrototype> CameraSite = "CMUNetSiteDeepEyes";

    /// <summary>Camera networks a <c>cam://</c> link may open.</summary>
    [DataField]
    public HashSet<ProtoId<CameraNetworkPrototype>> CameraNetworks = new()
    {
        "CMUSurveillanceCameraColonyGOVFOR",
        "CMUSurveillanceCameraColonyWEYU",
        "CMUSurveillanceCameraColonyCMB",
    };

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextScan;

    [DataField]
    public TimeSpan ScanCooldown = TimeSpan.FromSeconds(45);
}

/// <summary>
/// Lets a computer be hacked with an access tuner by someone with enough engineering skill: lock onto the
/// terminal's carrier wave, then route it through the board to the core, before the security trace finishes.
/// The board only ever exists on the server.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMUDeepWebHackableComponent : Component
{
    [DataField]
    public int SkillLevel = 2;

    /// <summary>How long the trace takes to find the hacker, before skill.</summary>
    [DataField]
    public TimeSpan Trace = TimeSpan.FromSeconds(75);

    /// <summary>How long the trace takes on a retune, which is only the carrier lock, before skill.</summary>
    [DataField]
    public TimeSpan RetuneTrace = TimeSpan.FromSeconds(40);

    /// <summary>Trace time a wrong carrier lock costs.</summary>
    [DataField]
    public TimeSpan MissPenalty = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan Lockout = TimeSpan.FromSeconds(60);

    [DataField, AutoPausedField]
    public TimeSpan LockedUntil;

    [ViewVariables]
    public CMUDeepHackBoard? Board;
}

/// <summary>Server side state of one hack or retune attempt.</summary>
public sealed class CMUDeepHackBoard
{
    public required EntityUid User;

    /// <summary>A retune only locks the carrier, then moves the terminal on or off the colony network.</summary>
    public bool Retune;

    /// <summary>For a retune: whether it takes the terminal dark, or back onto the colony network.</summary>
    public bool ToDark;
    public CMUDeepHackStage Stage;
    public int Frequency;
    public int Amplitude;
    public int Phase;

    /// <summary>How much static hides the carrier on the scope, 0 to 1.</summary>
    public float Noise;
    public required int Width;
    public required int Height;
    public required int SourceRow;
    public required int CoreRow;
    public required List<CMUDeepHackCell> Cells;

    /// <summary>A rotation for every cell that solves the board; never sent to the client.</summary>
    public required List<byte> Solution;
    public TimeSpan TraceStarted;
    public TimeSpan TraceEnds;
    public readonly List<string> Log = new();
}

[Serializable, NetSerializable]
public enum CMUDeepHackUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUDeepHackState(
    CMUDeepHackStage stage,
    int frequency,
    int amplitude,
    int phase,
    float noise,
    int width,
    int height,
    int sourceRow,
    int coreRow,
    List<CMUDeepHackCell> cells,
    TimeSpan traceStarted,
    TimeSpan traceEnds,
    TimeSpan lockedUntil,
    List<string> log,
    bool retune = false,
    bool toDark = false) : BoundUserInterfaceState
{
    public readonly CMUDeepHackStage Stage = stage;
    public readonly int Frequency = frequency;
    public readonly int Amplitude = amplitude;
    public readonly int Phase = phase;
    public readonly float Noise = noise;
    public readonly int Width = width;
    public readonly int Height = height;
    public readonly int SourceRow = sourceRow;
    public readonly int CoreRow = coreRow;
    public readonly List<CMUDeepHackCell> Cells = cells;
    public readonly TimeSpan TraceStarted = traceStarted;
    public readonly TimeSpan TraceEnds = traceEnds;
    public readonly TimeSpan LockedUntil = lockedUntil;
    public readonly List<string> Log = log;
    public readonly bool Retune = retune;
    public readonly bool ToDark = toDark;
}

/// <summary>Tries to lock the tuner onto the carrier with these dial settings.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepHackLockMessage(int frequency, int amplitude, int phase) : BoundUserInterfaceMessage
{
    public readonly int Frequency = frequency;
    public readonly int Amplitude = amplitude;
    public readonly int Phase = phase;
}

/// <summary>Turns a board tile a quarter turn clockwise.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepHackRotateMessage(int cell) : BoundUserInterfaceMessage
{
    public readonly int Cell = cell;
}

/// <summary>Asks the server to reveal one more hidden deep site on this computer.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepScanMessage : BoundUserInterfaceMessage;

/// <summary>Asks the server for an ad page; ad pages are made up per round, so only the server has them.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepPageRequestMessage(string host) : BoundUserInterfaceMessage
{
    public readonly string Host = host;
}

/// <summary>An ad page, as page markup.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepPageMessage(string host, string markup) : BoundUserInterfaceMessage
{
    public readonly string Host = host;
    public readonly string Markup = markup;
}

/// <summary>A <c>cam://</c> link: opens a camera network on the computer's screen.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepCameraMessage(string network) : BoundUserInterfaceMessage
{
    public readonly string Network = network;
}

/// <summary>An insurgent seizes the hijackable boards' front pages.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepHijackMessage(string title, string body) : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly string Body = body;
}

public static class CMUDeepAddress
{
    public const string AdSuffix = ".ad";
    public const string CameraScheme = "cam://";
    public const int HostLength = 64;

    private const string AliasCharacters = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int AliasGroups = 8;

    /// <summary>A random address like <c>eb1a-a02d-ans9-202ab-0s9bf-ffa0-l2kc-998d.deep</c>.</summary>
    public static string NewAlias(System.Random rng, string suffix)
    {
        var alias = new StringBuilder();
        for (var group = 0; group < AliasGroups; group++)
        {
            if (group > 0)
                alias.Append('-');

            var length = rng.Next(4, 6);
            for (var i = 0; i < length; i++)
                alias.Append(AliasCharacters[rng.Next(AliasCharacters.Length)]);
        }

        return alias + suffix;
    }

    /// <summary>Ad hosts end in <c>.ad</c> and only use lowercase letters, digits, dashes and dots.</summary>
    public static bool IsAd(string host)
    {
        if (host.Length <= AdSuffix.Length
            || host.Length > HostLength
            || !host.EndsWith(AdSuffix, StringComparison.Ordinal))
            return false;

        foreach (var c in host)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '.'))
                return false;
        }

        return true;
    }
}

