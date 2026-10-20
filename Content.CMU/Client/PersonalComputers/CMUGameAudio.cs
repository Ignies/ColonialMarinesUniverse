using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Sounds for the ExtraNet's games and arenas. They play only for the player at the screen, only while the control
/// that makes them is on screen, and not at all while the games are muted. The same sound is not stacked within a
/// few frames, so a burst of hits does not get louder than one.
/// </summary>
public static class CMUGameAudio
{
    /// <summary>Base volume of every game sound, in dB, so a computer screen sits under the round's own audio.</summary>
    public const float BaseVolume = -5f;

    private static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(45);
    private static readonly Dictionary<string, TimeSpan> Last = new();

    /// <summary>Mutes every game and arena for this client until the round ends or it is turned back on.</summary>
    public static bool Muted;

    public static event Action? MutedChanged;

    public static void SetMuted(bool muted)
    {
        if (Muted == muted)
            return;

        Muted = muted;
        MutedChanged?.Invoke();
    }

    /// <summary>Plays a sound from <c>/Audio/...</c> for this player when <paramref name="source"/> is visible.</summary>
    public static void Play(Control source, string path, float volume = 0f, float pitch = 1f)
    {
        if (Muted || !source.VisibleInTree)
            return;

        var timing = IoCManager.Resolve<IGameTiming>();
        if (Last.TryGetValue(path, out var last) && timing.RealTime - last < Spacing)
            return;

        Last[path] = timing.RealTime;
        var audio = IoCManager.Resolve<IEntityManager>().System<AudioSystem>();
        audio.PlayGlobal(new ResolvedPathSpecifier(path), Filter.Local(), false,
            AudioParams.Default.WithVolume(BaseVolume + volume).WithPitchScale(pitch));
    }

    /// <summary>Plays one of several takes of a sound, picked at random, with a little pitch variation.</summary>
    public static void PlayAny(Control source, IReadOnlyList<string> paths, float volume = 0f, float spread = 0.08f)
    {
        if (paths.Count == 0)
            return;

        var random = IoCManager.Resolve<IRobustRandom>();
        Play(source, random.Pick(paths), volume, 1f + random.NextFloat(-spread, spread));
    }

    /// <summary>The scream takes in <c>/Audio/Voice/Human/</c>.</summary>
    public static readonly string[] MaleScreams =
    {
        "/Audio/Voice/Human/malescream_1.ogg", "/Audio/Voice/Human/malescream_2.ogg",
        "/Audio/Voice/Human/malescream_3.ogg", "/Audio/Voice/Human/malescream_4.ogg",
        "/Audio/Voice/Human/malescream_5.ogg", "/Audio/Voice/Human/malescream_6.ogg",
    };

    public static readonly string[] FemaleScreams =
    {
        "/Audio/Voice/Human/femalescream_1.ogg", "/Audio/Voice/Human/femalescream_2.ogg",
        "/Audio/Voice/Human/femalescream_3.ogg", "/Audio/Voice/Human/femalescream_4.ogg",
        "/Audio/Voice/Human/femalescream_5.ogg",
    };

    public static void Scream(Control source, bool female, float volume = 0f)
    {
        PlayAny(source, female ? FemaleScreams : MaleScreams, volume);
    }
}
