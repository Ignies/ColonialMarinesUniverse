using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Photography;

/// <summary>
/// A camera that photographs a square of tiles: click a tile while holding it and a print of that area, as the
/// photographer sees it, pops out there. Alt click cycles the frame size.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUPhotoCameraComponent : Component
{
    /// <summary>Frame sizes in tiles, all odd so the clicked tile is the centre.</summary>
    [DataField]
    public int[] Sizes = { 1, 3, 5, 7 };

    [DataField, AutoNetworkedField]
    public int Size = 3;

    /// <summary>How far from the photographer, in tiles, the clicked tile may be.</summary>
    [DataField]
    public float Range = 10;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(2);

    [DataField, AutoPausedField]
    public TimeSpan NextPhoto;
}

/// <summary>A printed photograph. The picture itself is held by the server and fetched by <see cref="Id"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPhotoComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Id;

    /// <summary>Frame size in tiles.</summary>
    [DataField, AutoNetworkedField]
    public int Size;

    [DataField, AutoNetworkedField]
    public string Photographer = string.Empty;
}

[Serializable, NetSerializable]
public enum CMUPhotoUiKey : byte
{
    Key,
}

public static class CMUPhotoLimits
{
    /// <summary>Pixels per tile in a print. Captures are resampled to this.</summary>
    public const int TilePixels = 32;

    public const int MaxBytes = 160 * 1024;

    /// <summary>How long the server waits for the photographer's client to send the picture.</summary>
    public static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Prints kept per round; the oldest pictures are forgotten past this.</summary>
    public const int PhotosPerRound = 400;
}

/// <summary>Server to photographer: capture this square of your view and send it back as a PNG.</summary>
[Serializable, NetSerializable]
public sealed class CMUPhotoCaptureRequestEvent(int request, NetCoordinates centre, int size) : EntityEventArgs
{
    public readonly int Request = request;
    public readonly NetCoordinates Centre = centre;
    public readonly int Size = size;
}

/// <summary>Photographer to server: the captured picture, or an empty array when the area was not in view.</summary>
[Serializable, NetSerializable]
public sealed class CMUPhotoCaptureResultEvent(int request, byte[] png) : EntityEventArgs
{
    public readonly int Request = request;
    public readonly byte[] Png = png;
}

[Serializable, NetSerializable]
public sealed class CMUPhotoImageRequestEvent(int photo) : EntityEventArgs
{
    public readonly int Photo = photo;
}

[Serializable, NetSerializable]
public sealed class CMUPhotoImageEvent(int photo, byte[] png) : EntityEventArgs
{
    public readonly int Photo = photo;
    public readonly byte[] Png = png;
}
