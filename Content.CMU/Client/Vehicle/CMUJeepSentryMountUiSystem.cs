using Content.Shared.CMU14.Vehicle.Open;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Refreshes an open jeep sentry settings window when the pintle's settings or target change.
/// </summary>
public sealed class CMUJeepSentryMountUiSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJeepSentryMountComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<CMUJeepSentryMountComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, CMUJeepSentryUiKey.Key, out var bui))
            bui.Update();
    }
}
