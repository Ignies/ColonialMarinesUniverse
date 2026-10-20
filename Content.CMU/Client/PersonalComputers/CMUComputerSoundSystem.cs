using Content.Shared.CMU14.PersonalComputers;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// The machine sounds a terminal makes for whoever has its window open: the boot, the glitch over a hacked boot, the
/// running hum and the shutdown. Kept per terminal rather than per window, because the engine can close and reopen
/// a window within a tick or two when its user steps away or presses the close button: a short grace keeps the hum
/// going through that instead of cutting it and starting the boot again on top.
/// </summary>
public sealed class CMUComputerSoundSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>How long a closed window keeps the machine's sounds, in case it opens again straight away.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(0.4);

    private readonly Dictionary<EntityUid, Machine> _machines = new();

    private sealed class Machine
    {
        public readonly List<EntityUid> Sounds = new();
        public SoundSpecifier? Hum;
        public EntityUid? Humming;
        public TimeSpan? HumAt;
        public TimeSpan? StopAt;
    }

    /// <summary>A window on the terminal opened: anything it was about to stop keeps playing.</summary>
    public void Watch(EntityUid computer)
    {
        if (_machines.TryGetValue(computer, out var machine))
            machine.StopAt = null;
    }

    /// <summary>The window closed: the machine goes quiet for this player unless it opens again within the grace.</summary>
    public void Leave(EntityUid computer)
    {
        if (_machines.TryGetValue(computer, out var machine))
            machine.StopAt = _timing.RealTime + Grace;
    }

    /// <summary>Switched on in front of us: the boot sound, then the hum once it settles.</summary>
    public void Boot(EntityUid computer, CMUComputerOsPrototype os, bool glitch)
    {
        var machine = Fresh(computer);
        Track(machine, Play(os.BootSound, -4f));
        if (glitch)
            Track(machine, Play(os.GlitchSound, -6f));

        machine.Hum = os.RunningSound;
        machine.HumAt = _timing.RealTime + TimeSpan.FromSeconds(os.BootSoundLength);
    }

    /// <summary>Already running when we got there: just the hum, unless it is humming already.</summary>
    public void Running(EntityUid computer, CMUComputerOsPrototype os)
    {
        if (!_machines.TryGetValue(computer, out var machine))
        {
            machine = new Machine();
            _machines[computer] = machine;
        }

        machine.StopAt = null;
        if (machine.Humming != null || machine.HumAt != null)
            return;

        machine.Hum = os.RunningSound;
        machine.HumAt = _timing.RealTime;
    }

    /// <summary>Switched off in front of us: everything stops and the shutdown plays, once.</summary>
    public void ShutDown(EntityUid computer, CMUComputerOsPrototype os)
    {
        Stop(computer);
        Play(os.ShutdownSound, -4f);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_machines.Count == 0)
            return;

        var now = _timing.RealTime;
        foreach (var (computer, machine) in _machines)
        {
            if (machine.StopAt is { } stop && now >= stop || TerminatingOrDeleted(computer))
            {
                Stop(computer);
                return;
            }

            if (machine.HumAt is not { } at || now < at)
                continue;

            machine.HumAt = null;
            machine.Humming = Play(machine.Hum, -10f, true);
            Track(machine, machine.Humming);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var computer in new List<EntityUid>(_machines.Keys))
            Stop(computer);
    }

    private Machine Fresh(EntityUid computer)
    {
        Stop(computer);
        var machine = new Machine();
        _machines[computer] = machine;
        return machine;
    }

    private void Stop(EntityUid computer)
    {
        if (!_machines.Remove(computer, out var machine))
            return;

        foreach (var sound in machine.Sounds)
            _audio.Stop(sound);
    }

    private static void Track(Machine machine, EntityUid? sound)
    {
        if (sound is { } uid)
            machine.Sounds.Add(uid);
    }

    private EntityUid? Play(SoundSpecifier? sound, float volume, bool loop = false)
    {
        if (sound == null)
            return null;

        return _audio.PlayGlobal(sound, Filter.Local(), false,
            AudioParams.Default.WithVolume(volume).WithLoop(loop))?.Entity;
    }
}
