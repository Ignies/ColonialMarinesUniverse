using Content.Shared.UserInterface;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// Points a bridged console's activation at the PC OS, on both sides so prediction agrees, and points it back when the
/// bridge lets go. The console's own open checks (access, skills, round rules) still run first, as ever.
/// </summary>
public sealed class CMUComputerBridgeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUComputerConsoleComponent, ComponentStartup>((uid, comp, _) => Apply((uid, comp)));
        SubscribeLocalEvent<CMUComputerConsoleComponent, AfterAutoHandleStateEvent>((uid, comp, _) => Apply((uid, comp)));
        SubscribeLocalEvent<CMUComputerConsoleComponent, ComponentShutdown>(OnShutdown);
    }

    public void Apply(Entity<CMUComputerConsoleComponent> ent)
    {
        if (ent.Comp.OriginalKey == null || !TryComp(ent, out ActivatableUIComponent? activatable))
            return;

        activatable.Key = CMUPersonalComputerUiKey.Key;
    }

    private void OnShutdown(Entity<CMUComputerConsoleComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.OriginalKey is not { } original
            || !TryComp(ent, out ActivatableUIComponent? activatable)
            || !Equals(activatable.Key, CMUPersonalComputerUiKey.Key))
            return;

        activatable.Key = original;
    }
}
