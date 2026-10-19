using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.PersonalComputers.DeepWeb;

public sealed class CMUDeepHackBui : BoundUserInterface
{
    private CMUDeepHackWindow? _window;

    public CMUDeepHackBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUDeepHackWindow>();
        _window.OnLock += (frequency, amplitude, phase) => SendMessage(new CMUDeepHackLockMessage(frequency, amplitude, phase));
        _window.OnRotate += cell => SendMessage(new CMUDeepHackRotateMessage(cell));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CMUDeepHackState hack)
            _window?.UpdateState(hack);
    }
}
