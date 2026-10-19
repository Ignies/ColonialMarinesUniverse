using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersonalComputers.DeepWeb;

/// <summary>A contraband listing. The drop point is only sent to whoever buys it.</summary>
[Serializable, NetSerializable]
public sealed class CMUMarketListing(int id, string seller, string title, string description, int price)
{
    public readonly int Id = id;

    /// <summary>The seller's market handle, never their name.</summary>
    public readonly string Seller = seller;
    public readonly string Title = title;
    public readonly string Description = description;
    public readonly int Price = price;
}

/// <summary>Something this account bought, with where to pick it up.</summary>
[Serializable, NetSerializable]
public sealed class CMUMarketPurchase(string title, string drop)
{
    public readonly string Title = title;
    public readonly string Drop = drop;
}

[Serializable, NetSerializable]
public sealed class CMUMarketRequestMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUMarketListMessage(string title, string description, int price, string drop) : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly string Description = description;
    public readonly int Price = price;
    public readonly string Drop = drop;
}

[Serializable, NetSerializable]
public sealed class CMUMarketBuyMessage(int listing) : BoundUserInterfaceMessage
{
    public readonly int Listing = listing;
}

[Serializable, NetSerializable]
public sealed class CMUMarketStateMessage(List<CMUMarketListing> listings, List<CMUMarketPurchase> purchases, bool canSell)
    : BoundUserInterfaceMessage
{
    public readonly List<CMUMarketListing> Listings = listings;
    public readonly List<CMUMarketPurchase> Purchases = purchases;

    /// <summary>Only insurgents may list.</summary>
    public readonly bool CanSell = canSell;
}

public static class CMUMarketLimits
{
    public const int TitleLength = 60;
    public const int DescriptionLength = 300;
    public const int DropLength = 80;
    public const int MaxPrice = 50000;
    public const int Listings = 30;
}

/// <summary>Packet sniffer: the last pages another terminal opened.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepSniffMessage(int terminal) : BoundUserInterfaceMessage
{
    public readonly int Terminal = terminal;
}

[Serializable, NetSerializable]
public sealed class CMUDeepToolResultMessage(string title, List<string> lines) : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly List<string> Lines = lines;
}

/// <summary>Camera loop: a camera on the tapped network shows nothing for a minute.</summary>
[Serializable, NetSerializable]
public sealed class CMUDeepLoopMessage(string network) : BoundUserInterfaceMessage
{
    public readonly string Network = network;
}

public static class CMUDeepToolLimits
{
    public const int SniffLines = 6;
    public static readonly TimeSpan SniffCooldown = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan LoopLength = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan LoopCooldown = TimeSpan.FromMinutes(3);
}
