using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// ExtraNet shops, WeyBay first: buy a real item from a terminal and it is charged to the card in the reader, then
/// comes in on the freight run as a crate addressed to the buyer, left on the colony's pickup pallet (or at the
/// terminal it was ordered from, on a colony without one). The buyer is mailed when it is paid for and when it is
/// waiting, and the shop's page tracks it.
/// </summary>
public sealed class CMUShopSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private CMUComputerPrintSystem _print = default!;
    [Dependency] private CMUMailSystem _mail = default!;
    [Dependency] private CMUPersonalComputerSystem _computers = default!;
    [Dependency] private CMUSecurityNetSystem _security = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>The crate a parcel comes in.</summary>
    public static readonly EntProtoId Parcel = "CrateGenericSteel";

    private readonly List<Order> _orders = new();
    private int _nextId = 1;
    private float _check;

    private sealed class Order
    {
        public int Id;
        public int Account;
        public string Buyer = string.Empty;
        public EntProtoId Item;
        public int Price;
        public TimeSpan Ordered;
        public TimeSpan Due;
        public EntityUid Terminal;
        public MapCoordinates TerminalAt;
        public CMUShopOrderState State;
        public string Where = string.Empty;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _orders.Clear();
            _nextId = 1;
        });
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUShopRequestMessage>(OnRequest);
            subs.Event<CMUShopBuyMessage>(OnBuy);
            subs.Event<CMUShopReceiptMessage>(OnReceipt);
        });
    }

    private bool Enabled => _cfg.GetCVar(CMUPersonalComputerCVars.Shop);

    private void OnRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUShopRequestMessage args)
    {
        SendState(ent, args.Actor);
    }

    private void OnBuy(Entity<CMUPersonalComputerComponent> ent, ref CMUShopBuyMessage args)
    {
        if (!Enabled
            || !_prototype.TryIndex<CMUShopCatalogPrototype>(args.Catalog, out var catalog)
            || args.Entry < 0
            || args.Entry >= catalog.Entries.Count)
            return;

        var entry = catalog.Entries[args.Entry];
        if (ent.Comp.SessionAccount == 0 || ent.Comp.SessionUser is not { } buyer
            || _bank.FindAccount(ent.Comp.SessionAccount) is not { } account)
        {
            _popup.PopupEntity(Loc.GetString("cmu-shop-no-card"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        var number = ent.Comp.SessionAccount;
        if (_orders.Count(o => o.Account == number && o.State != CMUShopOrderState.Arrived) >= CMUShopLimits.OpenOrders)
        {
            _popup.PopupEntity(Loc.GetString("cmu-shop-too-many", ("count", CMUShopLimits.OpenOrders)), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (account.card.AccountBalance < entry.Price)
        {
            _popup.PopupEntity(Loc.GetString("cmu-shop-no-funds"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        account.card.AccountBalance -= entry.Price;
        Dirty(account.uid, account.card);
        _bank.RecordTransaction(account.uid, AtmHistoryKind.Purchase, entry.Price);

        var now = _timing.CurTime;
        var order = new Order
        {
            Id = _nextId++,
            Account = number,
            Buyer = buyer,
            Item = entry.Item,
            Price = entry.Price,
            Ordered = now,
            Due = now + TimeSpan.FromMinutes(entry.Minutes),
            Terminal = ent,
            TerminalAt = _transform.GetMapCoordinates(ent),
        };
        _orders.Add(order);

        var name = ItemName(entry.Item);
        _mail.Deliver(Loc.GetString("cmu-shop-mail-from"), buyer, Loc.GetString("cmu-shop-mail-paid-subject", ("item", name)),
            Loc.GetString("cmu-shop-mail-paid-body", ("item", name), ("price", entry.Price), ("minutes", (int) Math.Ceiling(entry.Minutes))));
        _popup.PopupEntity(Loc.GetString("cmu-shop-bought", ("item", name)), ent, args.Actor);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} bought {entry.Item} for {entry.Price} as {buyer} on terminal {ToPrettyString(ent)}");
        SendState(ent, args.Actor);
    }

    /// <summary>
    /// An order's receipt, for the card that paid for it: saved to the terminal's Files, or printed on its printer.
    /// </summary>
    private void OnReceipt(Entity<CMUPersonalComputerComponent> ent, ref CMUShopReceiptMessage args)
    {
        var id = args.Order;
        var account = ent.Comp.SessionAccount;
        if (!Enabled || account == 0 || _orders.FirstOrDefault(o => o.Id == id && o.Account == account) is not { } order)
            return;

        var name = Loc.GetString("cmu-shop-receipt-name", ("id", order.Id.ToString("D4")));
        var text = Receipt(order);
        var from = Loc.GetString("cmu-shop-mail-from");
        if (!args.Print)
        {
            _computers.AddNote(ent, name, text, from);
            _popup.PopupEntity(Loc.GetString("cmu-shop-receipt-saved", ("name", name)), ent, args.Actor);
            return;
        }

        if (!_print.CanPrint(ent.Comp))
            _popup.PopupEntity(Loc.GetString("cmu-print-none"), ent, args.Actor, PopupType.SmallCaution);
        else if (!_print.TryPrint(ent, args.Actor, $"{name}.TXT", text, from))
            _popup.PopupEntity(Loc.GetString("cmu-print-busy"), ent, args.Actor, PopupType.SmallCaution);
    }

    /// <summary>The receipt's text: what was bought, for how much, by whom and when, the card number masked.</summary>
    private string Receipt(Order order)
    {
        var card = order.Account.ToString();
        var masked = card.Length > 4 ? new string('*', card.Length - 4) + card[^4..] : card;
        return Loc.GetString("cmu-shop-receipt-body",
            ("id", order.Id.ToString("D4")),
            ("item", ItemName(order.Item)),
            ("price", order.Price),
            ("buyer", order.Buyer),
            ("card", masked),
            ("time", order.Ordered.ToString(@"hh\:mm")));
    }

    private void SendState(Entity<CMUPersonalComputerComponent> ent, EntityUid actor)
    {
        var account = ent.Comp.SessionAccount;
        var balance = account != 0 && _bank.FindAccount(account) is { } found ? found.card.AccountBalance : -1;
        var orders = new List<CMUShopOrderView>();
        if (account != 0)
        {
            foreach (var order in _orders.Where(o => o.Account == account).OrderByDescending(o => o.Id).Take(10))
                orders.Add(new CMUShopOrderView(order.Id, order.Item.Id, order.State, order.Due, order.Where));
        }

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key, new CMUShopStateMessage(orders, balance), actor);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _check -= frameTime;
        if (_check > 0 || _orders.Count == 0)
            return;

        _check = 1;
        var now = _timing.CurTime;
        foreach (var order in _orders)
        {
            if (order.State == CMUShopOrderState.Arrived)
                continue;

            if (now >= order.Due)
                Deliver(order);
            else if (now >= order.Ordered + (order.Due - order.Ordered) / 2)
                order.State = CMUShopOrderState.InTransit;
        }
    }

    /// <summary>Leaves the parcel at the pickup and tells the buyer where.</summary>
    private void Deliver(Order order)
    {
        var at = Pickup(order, out var where);
        var crate = Spawn(Parcel, at);
        _metaData.SetEntityName(crate, Loc.GetString("cmu-shop-parcel-name", ("buyer", order.Buyer)));
        _metaData.SetEntityDescription(crate, Loc.GetString("cmu-shop-parcel-desc", ("buyer", order.Buyer), ("item", ItemName(order.Item))));
        var item = Spawn(order.Item, at);
        _storage.Insert(item, crate);

        order.State = CMUShopOrderState.Arrived;
        order.Where = _security.Location(crate);
        _mail.Deliver(Loc.GetString("cmu-shop-mail-from"), order.Buyer,
            Loc.GetString("cmu-shop-mail-arrived-subject", ("item", ItemName(order.Item))),
            Loc.GetString(where ? "cmu-shop-mail-arrived-body" : "cmu-shop-mail-arrived-terminal-body",
                ("item", ItemName(order.Item)), ("where", order.Where)));
        _adminLog.Add(LogType.Action, LogImpact.Low, $"WeyBay delivered {order.Item} for {order.Buyer} at {ToPrettyString(crate)}");
    }

    /// <summary>A pickup pallet on the terminal's map, the nearest one; or the terminal itself if there is none.</summary>
    private MapCoordinates Pickup(Order order, out bool pallet)
    {
        var best = default(MapCoordinates?);
        var bestDistance = float.MaxValue;
        var query = EntityQueryEnumerator<CMUShopPickupComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var coords = _transform.GetMapCoordinates(uid, xform);
            if (coords.MapId != order.TerminalAt.MapId)
                continue;

            var distance = (coords.Position - order.TerminalAt.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = coords;
            bestDistance = distance;
        }

        pallet = best != null;
        if (best is { } found)
            return found;

        return !TerminatingOrDeleted(order.Terminal) ? _transform.GetMapCoordinates(order.Terminal) : order.TerminalAt;
    }

    private string ItemName(EntProtoId item)
    {
        return _prototype.TryIndex(item, out var proto) ? proto.Name : item.Id;
    }
}
