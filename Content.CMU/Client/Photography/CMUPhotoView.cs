using System.Numerics;
using Content.Shared.CMU14.Photography;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Photography;

/// <summary>A print's picture, shown at a whole number scale once the server has sent it.</summary>
public sealed class CMUPhotoView : TextureRect
{
    private readonly CMUPhotoClientSystem _photos;
    private readonly int _id;

    public CMUPhotoView(int id, int scale)
    {
        _id = id;
        _photos = IoCManager.Resolve<IEntityManager>().System<CMUPhotoClientSystem>();
        TextureScale = new Vector2(scale, scale);
        Stretch = StretchMode.KeepCentered;
        Texture = _photos.GetPhoto(id);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _photos.PhotoLoaded += OnLoaded;
        Texture ??= _photos.GetPhoto(_id);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _photos.PhotoLoaded -= OnLoaded;
    }

    private void OnLoaded(int id)
    {
        if (id == _id)
            Texture = _photos.GetPhoto(id);
    }
}

/// <summary>Opens a print full size when it is used.</summary>
public sealed class CMUPhotoBui : BoundUserInterface
{
    private DefaultWindow? _window;

    public CMUPhotoBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        if (!EntMan.TryGetComponent(Owner, out CMUPhotoComponent? photo))
        {
            Close();
            return;
        }

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("cmu-photo-window-title", ("photographer", photo.Photographer));
        // Small frames are blown up more so every print opens at a similar size.
        var scale = Math.Max(1, 448 / Math.Max(1, photo.Size * CMUPhotoLimits.TilePixels));
        _window.Contents.AddChild(new CMUPhotoView(photo.Id, scale));
    }
}
