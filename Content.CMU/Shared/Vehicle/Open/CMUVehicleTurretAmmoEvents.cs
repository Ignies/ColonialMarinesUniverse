using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Swapping a mounted gun's belt for one of another round from the box in hand, raised on the box.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUVehicleBeltSwapDoAfterEvent : SimpleDoAfterEvent;
