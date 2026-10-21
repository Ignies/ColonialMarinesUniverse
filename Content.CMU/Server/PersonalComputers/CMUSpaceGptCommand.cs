using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Eui;
using Robust.Shared.Console;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Admin control over a SpaceGPT question, run from the links in admin chat: <c>spacegpt hold|release|deny|options
/// &lt;question&gt;</c>.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class CMUSpaceGptCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private EuiManager _eui = default!;

    public string Command => "spacegpt";
    public string Description => Loc.GetString("cmu-cmd-spacegpt-desc");
    public string Help => Loc.GetString("cmu-cmd-spacegpt-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[1], out var id))
        {
            shell.WriteError(Help);
            return;
        }

        var spaceGpt = _entities.System<CMUSpaceGptSystem>();
        var admin = shell.Player?.Name ?? "console";
        var done = args[0] switch
        {
            "hold" => spaceGpt.Hold(id, admin),
            "release" => spaceGpt.Release(id, admin),
            "deny" => spaceGpt.Deny(id, admin),
            "options" when shell.Player is { } player => OpenOptions(spaceGpt, id, admin, player),
            _ => false,
        };

        if (!done)
            shell.WriteError(Loc.GetString("cmu-cmd-spacegpt-gone", ("id", id)));
    }

    /// <summary>Holds the question while the admin rewrites its answers, so the ghosts never see the old ones.</summary>
    private bool OpenOptions(CMUSpaceGptSystem spaceGpt, int id, string admin, Robust.Shared.Player.ICommonSession player)
    {
        if (!spaceGpt.TryGetPoll(id, out _, out _, out _))
            return false;

        spaceGpt.Hold(id, admin);
        _eui.OpenEui(new CMUSpaceGptOptionsEui(id), player);
        return true;
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromOptions(new[] { "hold", "release", "deny", "options" })
            : CompletionResult.Empty;
    }
}

/// <summary>An admin's window for a SpaceGPT question's answers.</summary>
public sealed class CMUSpaceGptOptionsEui(int poll) : BaseEui
{
    public override EuiStateBase GetNewState()
    {
        var spaceGpt = IoCManager.Resolve<IEntityManager>().System<CMUSpaceGptSystem>();
        spaceGpt.TryGetPoll(poll, out var asker, out var question, out var options);
        return new CMUSpaceGptOptionsEuiState(poll, asker, question, options);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is not CMUSpaceGptOptionsSaveMessage save)
            return;

        IoCManager.Resolve<IEntityManager>().System<CMUSpaceGptSystem>().SetOptions(poll, save.Options, Player.Name);
        Close();
    }
}
