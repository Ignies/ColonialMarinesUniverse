using Content.Shared.CMU14.Vehicle.Open;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Vehicle;

[UsedImplicitly]
public sealed class CMUJeepSentryBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUJeepSentryWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUJeepSentryWindow>();
        _window.OnFriendly += (faction, friendly) => SendMessage(new CMUJeepSentryFriendlyMessage(faction, friendly));
        _window.OnArmed += (faction, armed) => SendMessage(new CMUJeepSentryArmedMessage(faction, armed));
        _window.OnMode += mode => SendMessage(new CMUJeepSentryModeMessage(mode));
        _window.OnPower += powered => SendMessage(new CMUJeepSentryPowerMessage(powered));
        Update();
    }

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out CMUJeepSentryMountComponent? mount))
            return;

        var system = EntMan.System<CMUJeepSentryMountSystem>();
        var sentry = system.GetSentry((Owner, mount));
        var sentryName = sentry is { } fitted ? EntMan.GetComponent<MetaDataComponent>(fitted).EntityName : null;
        var targetName = mount.Target is { } target && EntMan.TryGetComponent(target, out MetaDataComponent? meta)
            ? meta.EntityName
            : null;

        _window.SetState(mount, sentryName, targetName, system.IsLocked((Owner, mount)));
    }
}
