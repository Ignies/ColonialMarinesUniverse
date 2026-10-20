using Content.Shared.CMU14.PersonalComputers;
using Robust.Shared.Configuration;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Keeps the arcade games downloaded to each computer's desktop. A download needs the arcade on and, like any page,
/// the network; deleting one stays on the machine.
/// </summary>
public sealed class CMUGameDownloadSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUGameDownloadMessage>(OnDownload);
            subs.Event<CMUGameDeleteMessage>(OnDelete);
        });
    }

    private void OnDownload(Entity<CMUPersonalComputerComponent> ent, ref CMUGameDownloadMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Enabled)
            || !_cfg.GetCVar(CMUPersonalComputerCVars.Arcade)
            || !Enum.IsDefined(args.Game)
            || ent.Comp.Downloads.Contains(args.Game))
            return;

        ent.Comp.Downloads.Add(args.Game);
        Dirty(ent);
    }

    private void OnDelete(Entity<CMUPersonalComputerComponent> ent, ref CMUGameDeleteMessage args)
    {
        if (ent.Comp.Downloads.Remove(args.Game))
            Dirty(ent);
    }
}
