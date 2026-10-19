using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// A computer that boots an OS and browses the ExtraNet. Pages ship with the client, so browsing is client side.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPersonalComputerComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<CMUComputerOsPrototype> Os = "CMUOsSeegson";

    /// <summary>Overrides the OS networks when set, e.g. a colony PC wired into a corporate network.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<CMUNetworkPrototype>>? Networks;

    /// <summary>Overrides the OS home page when set.</summary>
    [DataField, AutoNetworkedField]
    public string? HomePage;

    /// <summary>
    /// Folder of this computer's colony pages: a <see cref="CMUNetColonyPrototype"/> id, or the round's game map
    /// id when no colony lists the map. Set by the server from the round's map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Colony;

    /// <summary>What <c>{colony}</c> becomes in pages: the round's map name.</summary>
    [DataField, AutoNetworkedField]
    public string? ColonyName;

    /// <summary>Documents on this computer, in addition to the OS ones. Mappers use it for notes left on a PC.</summary>
    [DataField, AutoNetworkedField]
    public List<CMUComputerDocument> Documents = new();
}

/// <summary>A read only document in the Files app, written in the same format as ExtraNet pages.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUComputerDocument
{
    /// <summary>File name shown in Files, e.g. <c>README.TXT</c>. Not localized: it is a file name.</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public ResPath Path;
}

[Serializable, NetSerializable]
public enum CMUPersonalComputerUiKey : byte
{
    Key,
}
