using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers;

/// <summary>
/// What an ExtraNet shop sells: real items, each with a price taken from the card in the reader and how long it
/// takes to come in on the next freight run. WeyBay's is <c>CMUWeyBayCatalog</c>.
/// </summary>
[Prototype("cmuShopCatalog")]
public sealed partial class CMUShopCatalogPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<CMUShopEntry> Entries = new();
}

[DataDefinition]
public sealed partial class CMUShopEntry
{
    [DataField(required: true)]
    public EntProtoId Item;

    [DataField(required: true)]
    public int Price;

    /// <summary>How long from the order to the parcel at the pickup, in minutes.</summary>
    [DataField]
    public float Minutes = 5;

    /// <summary>The heading it is listed under.</summary>
    [DataField]
    public LocId Category = "cmu-shop-category-misc";

    /// <summary>The seller's line about it. Unset, the item's own description.</summary>
    [DataField]
    public LocId? Blurb;
}

/// <summary>
/// Where shop parcels are left: a pallet mappers put in the colony's logistics area. Without one, a parcel is left
/// at the terminal it was ordered from.
/// </summary>
[RegisterComponent]
public sealed partial class CMUShopPickupComponent : Component;

[Serializable, NetSerializable]
public enum CMUShopOrderState : byte
{
    /// <summary>Paid for, being picked off the shelf.</summary>
    Processing,

    /// <summary>On the freight run.</summary>
    InTransit,

    /// <summary>Waiting at the pickup.</summary>
    Arrived,
}

/// <summary>An order, as its buyer sees it on the shop's page.</summary>
[Serializable, NetSerializable]
public sealed class CMUShopOrderView(int id, string item, CMUShopOrderState state, TimeSpan due, string where)
{
    public readonly int Id = id;

    /// <summary>The item's prototype, for its picture and name.</summary>
    public readonly string Item = item;
    public readonly CMUShopOrderState State = state;
    public readonly TimeSpan Due = due;

    /// <summary>Where it is waiting, once it has arrived.</summary>
    public readonly string Where = where;
}

[Serializable, NetSerializable]
public sealed class CMUShopRequestMessage : BoundUserInterfaceMessage;

/// <summary>The logged in account's orders and balance; balance -1 with nobody logged in.</summary>
[Serializable, NetSerializable]
public sealed class CMUShopStateMessage(List<CMUShopOrderView> orders, int balance) : BoundUserInterfaceMessage
{
    public readonly List<CMUShopOrderView> Orders = orders;
    public readonly int Balance = balance;
}

[Serializable, NetSerializable]
public sealed class CMUShopBuyMessage(string catalog, int entry) : BoundUserInterfaceMessage
{
    public readonly string Catalog = catalog;
    public readonly int Entry = entry;
}

/// <summary>Asks for an order's receipt: saved to Files, or printed when <see cref="Print"/>.</summary>
[Serializable, NetSerializable]
public sealed class CMUShopReceiptMessage(int order, bool print) : BoundUserInterfaceMessage
{
    public readonly int Order = order;
    public readonly bool Print = print;
}

public static class CMUShopLimits
{
    /// <summary>Orders an account can have on their way at once.</summary>
    public const int OpenOrders = 5;
}
