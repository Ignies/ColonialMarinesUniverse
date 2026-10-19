using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Camera;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.SurveillanceCamera;
using Content.Shared.Camera;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CMU14.PersonalComputers.DeepWeb;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// The deep web's working side. Insurgents sell contraband on a market paid from colony accounts, with the
/// dead drop revealed only to the buyer; any hacked terminal can sniff another terminal's last pages, or loop
/// a camera on a tapped grid for a minute.
/// </summary>
public sealed partial class CMUDeepEconomySystem : EntitySystem
{
    private static readonly ProtoId<DatasetPrototype> Handles = "CMUDeepHackerHandles";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CameraNetworkSystem _cameraNetworks = default!;
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private CMUMailSystem _mail = default!;
    [Dependency] private SurveillanceCameraSystem _cameras = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private sealed record Listing(CMUMarketListing Shown, int SellerAccount, string Drop);

    private readonly List<Listing> _listings = new();
    private readonly Dictionary<int, List<CMUMarketPurchase>> _purchases = new();
    private readonly Dictionary<EntityUid, string> _sellerHandles = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextSniff = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextLoop = new();
    private readonly List<(EntityUid Camera, TimeSpan Ends)> _looped = new();
    private int _nextListing = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _listings.Clear();
            _purchases.Clear();
            _sellerHandles.Clear();
            _nextSniff.Clear();
            _nextLoop.Clear();
            _looped.Clear();
            _nextListing = 1;
        });
        Subs.BuiEvents<CMUDeepWebAccessComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUMarketRequestMessage>(OnMarketRequest);
            subs.Event<CMUMarketListMessage>(OnList);
            subs.Event<CMUMarketBuyMessage>(OnBuy);
            subs.Event<CMUDeepSniffMessage>(OnSniff);
            subs.Event<CMUDeepLoopMessage>(OnLoop);
        });
    }

    private bool On(CVarDef<bool> feature)
    {
        return _cfg.GetCVar(CMUPersonalComputerCVars.Enabled)
            && _cfg.GetCVar(CMUPersonalComputerCVars.DeepWeb)
            && _cfg.GetCVar(feature);
    }

    /// <summary>What is for sale right now.</summary>
    public IEnumerable<CMUMarketListing> Listings => _listings.Select(l => l.Shown);

    private int Account(EntityUid computer)
    {
        return TryComp(computer, out CMUPersonalComputerComponent? pc) ? pc.SessionAccount : 0;
    }

    private void OnMarketRequest(Entity<CMUDeepWebAccessComponent> ent, ref CMUMarketRequestMessage args)
    {
        if (On(CMUPersonalComputerCVars.Market))
            SendMarket(ent, args.Actor);
    }

    /// <summary>Insurgents list goods under a market handle; the money goes to the account they are logged in as.</summary>
    private void OnList(Entity<CMUDeepWebAccessComponent> ent, ref CMUMarketListMessage args)
    {
        var account = Account(ent);
        if (!On(CMUPersonalComputerCVars.Market)
            || !HasComp<CLFMemberComponent>(args.Actor)
            || account == 0
            || args.Price <= 0
            || args.Price > CMUMarketLimits.MaxPrice
            || _listings.Count >= CMUMarketLimits.Listings)
            return;

        var title = Clean(args.Title, CMUMarketLimits.TitleLength);
        var description = Clean(args.Description, CMUMarketLimits.DescriptionLength);
        var drop = Clean(args.Drop, CMUMarketLimits.DropLength);
        if (title.Length == 0 || drop.Length == 0)
            return;

        if (!_sellerHandles.TryGetValue(args.Actor, out var handle))
            _sellerHandles[args.Actor] = handle = _random.Pick(_prototype.Index(Handles).Values);

        _listings.Add(new Listing(new CMUMarketListing(_nextListing++, handle, title, description, args.Price), account, drop));
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} listed {title} for {args.Price} on the deep market, drop: {drop}");
        SendMarket(ent, args.Actor);
    }

    /// <summary>The buyer pays the seller, gets the drop point on screen and by mail, and the listing goes.</summary>
    private void OnBuy(Entity<CMUDeepWebAccessComponent> ent, ref CMUMarketBuyMessage args)
    {
        var account = Account(ent);
        var id = args.Listing;
        if (!On(CMUPersonalComputerCVars.Market)
            || account == 0
            || _listings.FirstOrDefault(l => l.Shown.Id == id) is not { } listing
            || listing.SellerAccount == account
            || _bank.FindAccount(account) is not { } buyer
            || buyer.card.AccountBalance < listing.Shown.Price
            || _bank.FindAccount(listing.SellerAccount) is not { } seller)
            return;

        buyer.card.AccountBalance -= listing.Shown.Price;
        seller.card.AccountBalance += listing.Shown.Price;
        Dirty(buyer.uid, buyer.card);
        Dirty(seller.uid, seller.card);
        _bank.RecordTransaction(buyer.uid, AtmHistoryKind.TransferOut, listing.Shown.Price, listing.SellerAccount);
        _bank.RecordTransaction(seller.uid, AtmHistoryKind.TransferIn, listing.Shown.Price, account);
        _listings.Remove(listing);

        if (!_purchases.TryGetValue(account, out var bought))
            _purchases[account] = bought = new List<CMUMarketPurchase>();
        bought.Add(new CMUMarketPurchase(listing.Shown.Title, listing.Drop));

        if (TryComp(ent, out CMUPersonalComputerComponent? pc) && pc.SessionUser is { } user)
            _mail.Deliver(listing.Shown.Seller, user, Loc.GetString("cmu-market-mail-subject", ("title", listing.Shown.Title)),
                Loc.GetString("cmu-market-mail-body", ("drop", listing.Drop)));

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} bought {listing.Shown.Title} for {listing.Shown.Price} on the deep market");
        SendMarket(ent, args.Actor);
    }

    private void SendMarket(EntityUid computer, EntityUid actor)
    {
        var account = Account(computer);
        var purchases = account != 0 && _purchases.TryGetValue(account, out var bought) ? new List<CMUMarketPurchase>(bought) : new List<CMUMarketPurchase>();
        _ui.ServerSendUiMessage(computer, CMUPersonalComputerUiKey.Key,
            new CMUMarketStateMessage(_listings.Select(l => l.Shown).ToList(), purchases, HasComp<CLFMemberComponent>(actor)), actor);
    }

    /// <summary>The last pages another terminal opened, read off the wire.</summary>
    private void OnSniff(Entity<CMUDeepWebAccessComponent> ent, ref CMUDeepSniffMessage args)
    {
        var now = _timing.CurTime;
        if (!On(CMUPersonalComputerCVars.HackerTools)
            || _nextSniff.TryGetValue(ent, out var ready) && now < ready)
            return;

        _nextSniff[ent] = now + CMUDeepToolLimits.SniffCooldown;
        var lines = new List<string>();
        var query = EntityQueryEnumerator<CMUPersonalComputerComponent>();
        while (query.MoveNext(out _, out var terminal))
        {
            if (terminal.TerminalId != args.Terminal)
                continue;

            lines.AddRange(terminal.Log.TakeLast(CMUDeepToolLimits.SniffLines).Select(l => l.Text));
            break;
        }

        if (lines.Count == 0)
            lines.Add(Loc.GetString("cmu-deep-sniff-nothing"));

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key,
            new CMUDeepToolResultMessage(Loc.GetString("cmu-deep-sniff-title", ("terminal", args.Terminal)), lines), args.Actor);
    }

    /// <summary>Freezes one camera of a tapped grid for a minute. Needs the camera tap found in the maze.</summary>
    private void OnLoop(Entity<CMUDeepWebAccessComponent> ent, ref CMUDeepLoopMessage args)
    {
        var now = _timing.CurTime;
        ProtoId<CameraNetworkPrototype> network = args.Network;
        if (!On(CMUPersonalComputerCVars.HackerTools)
            || !ent.Comp.Unlocked.Contains(ent.Comp.CameraSite)
            || !ent.Comp.CameraNetworks.Contains(network)
            || _nextLoop.TryGetValue(ent, out var ready) && now < ready)
            return;

        var cameras = _cameraNetworks.GetNetworkMembers(network)
            .Where(c => TryComp(c, out SurveillanceCameraComponent? camera) && camera.Active)
            .ToList();
        var lines = new List<string>();
        if (cameras.Count == 0)
        {
            lines.Add(Loc.GetString("cmu-deep-loop-nothing"));
        }
        else
        {
            _nextLoop[ent] = now + CMUDeepToolLimits.LoopCooldown;
            var camera = _random.Pick(cameras);
            _cameras.SetActive(camera, false);
            _looped.Add((camera, now + CMUDeepToolLimits.LoopLength));
            lines.Add(Loc.GetString("cmu-deep-loop-done", ("camera", Name(camera))));
            _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.Actor):player} looped camera {ToPrettyString(camera)} from the deep web");
        }

        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key,
            new CMUDeepToolResultMessage(Loc.GetString("cmu-deep-loop-title"), lines), args.Actor);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        for (var i = _looped.Count - 1; i >= 0; i--)
        {
            var (camera, ends) = _looped[i];
            if (now < ends)
                continue;

            _looped.RemoveAt(i);
            if (Exists(camera))
                _cameras.SetActive(camera, true);
        }
    }

    private static string Clean(string text, int max)
    {
        text = text.Trim().Replace('\n', ' ');
        return text.Length > max ? text[..max] : text;
    }
}
