using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared.CMU14.Photography;

/// <summary>Frame size selection and examine text for photo cameras and prints.</summary>
public sealed partial class CMUPhotoCameraSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPhotoCameraComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUPhotoCameraComponent, ExaminedEvent>(OnCameraExamined);
        SubscribeLocalEvent<CMUPhotoComponent, ExaminedEvent>(OnPhotoExamined);
    }

    private void OnGetVerbs(Entity<CMUPhotoCameraComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Sizes.Length == 0)
            return;

        var user = args.User;
        var next = NextSize(ent.Comp);
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-photo-camera-verb-frame", ("size", next)),
            Act = () => SetSize(ent, next, user),
        });
    }

    private static int NextSize(CMUPhotoCameraComponent camera)
    {
        var index = Array.IndexOf(camera.Sizes, camera.Size);
        return camera.Sizes[(index + 1) % camera.Sizes.Length];
    }

    public void SetSize(Entity<CMUPhotoCameraComponent> ent, int size, EntityUid? user)
    {
        ent.Comp.Size = size;
        Dirty(ent);
        if (user != null)
            _popup.PopupClient(Loc.GetString("cmu-photo-camera-frame-set", ("size", size)), ent, user.Value);
    }

    private void OnCameraExamined(Entity<CMUPhotoCameraComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-photo-camera-examine", ("size", ent.Comp.Size)));
    }

    private void OnPhotoExamined(Entity<CMUPhotoComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-photo-examine",
            ("size", ent.Comp.Size),
            ("photographer", ent.Comp.Photographer)));
    }
}
