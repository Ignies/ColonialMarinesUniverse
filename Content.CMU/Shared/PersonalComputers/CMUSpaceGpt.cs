using Content.Shared.Eui;
using Robust.Shared.Configuration;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>A colonist asks SpaceGPT a question from a terminal.</summary>
[Serializable, NetSerializable]
public sealed class CMUSpaceGptAskMessage(string question) : BoundUserInterfaceMessage
{
    public readonly string Question = question;
}

/// <summary>SpaceGPT's answer, as voted by the dead.</summary>
[Serializable, NetSerializable]
public sealed class CMUSpaceGptAnswerMessage(string question, string answer, int votes) : BoundUserInterfaceMessage
{
    public readonly string Question = question;
    public readonly string Answer = answer;

    /// <summary>
    /// How many ghosts voted for the answer. Zero means the network picked one itself; -1, that an admin denied the
    /// question and SpaceGPT's safeties answered.
    /// </summary>
    public readonly int Votes = votes;
}

/// <summary>Sent to every ghost: a question and the answers they may vote for.</summary>
[Serializable, NetSerializable]
public sealed class CMUSpaceGptPollEvent(int poll, string question, List<string> answers, TimeSpan ends, NetEntity terminal) : EntityEventArgs
{
    public readonly int Poll = poll;

    /// <summary>The terminal the question was asked at, for ghosts to follow there.</summary>
    public readonly NetEntity Terminal = terminal;
    public readonly string Question = question;
    public readonly List<string> Answers = answers;
    public readonly TimeSpan Ends = ends;
}

/// <summary>A ghost's vote.</summary>
[Serializable, NetSerializable]
public sealed class CMUSpaceGptVoteEvent(int poll, int answer) : EntityEventArgs
{
    public readonly int Poll = poll;
    public readonly int Answer = answer;
}

/// <summary>
/// Tells the asker an admin is holding their question back: their page fills the wait with SpaceGPT pretending to
/// work. False when it is let go.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUSpaceGptHoldMessage(bool held) : BoundUserInterfaceMessage
{
    public readonly bool Held = held;
}

/// <summary>Takes a question off the ghosts' lists: answered, denied or over.</summary>
[Serializable, NetSerializable]
public sealed class CMUSpaceGptCancelEvent(int poll) : EntityEventArgs
{
    public readonly int Poll = poll;
}

/// <summary>An admin rewriting a question's answers before the ghosts see them.</summary>
[Serializable, NetSerializable]
public sealed class CMUSpaceGptOptionsEuiState(int poll, string asker, string question, List<string> options) : EuiStateBase
{
    public readonly int Poll = poll;
    public readonly string Asker = asker;
    public readonly string Question = question;
    public readonly List<string> Options = options;
}

[Serializable, NetSerializable]
public sealed class CMUSpaceGptOptionsSaveMessage(List<string> options) : EuiMessageBase
{
    public readonly List<string> Options = options;
}

public static class CMUSpaceGptLimits
{
    /// <summary>How long admins have to hold, deny or rewrite a question before the ghosts get it.</summary>
    public static readonly TimeSpan AdminDelay = TimeSpan.FromSeconds(5);

    public const int AnswerLength = 120;

    public const int QuestionLength = 140;
    public const int Choices = 6;
    public static readonly TimeSpan Voting = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);
}

[CVarDefs]
public sealed class CMUSpaceGptCVars
{
    /// <summary>SpaceGPT, answered by ghosts voting.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("cmu.pc.spacegpt", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Whether this player, as a ghost, gets SpaceGPT questions to vote on.</summary>
    public static readonly CVarDef<bool> GhostPolls =
        CVarDef.Create("cmu.pc.spacegpt_ghost_polls", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
