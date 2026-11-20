using System.Numerics;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws someone braced against a vehicle leaning in toward it, and puts them back upright when they
/// let go.
/// </summary>
public sealed class CMUVehiclePushVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, Vector2> _baseOffsets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehiclePushingComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CMUVehiclePushingComponent> ent, ref ComponentShutdown args)
    {
        if (_baseOffsets.Remove(ent, out var original) && TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetOffset((ent, sprite), original);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehiclePushingComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var pushing, out var sprite))
        {
            if (!_baseOffsets.TryGetValue(uid, out var original))
            {
                original = sprite.Offset;
                _baseOffsets[uid] = original;
            }

            _sprite.SetOffset((uid, sprite), original + pushing.Offset);
        }
    }
}
