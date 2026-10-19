using System.Linq;
using System.Text;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.CriminalRecords;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers.DeepWeb;

/// <summary>
/// Unknown hackers posting to the deep web's leak boards at random times: ATM dumps of the colony's richest
/// accounts, and leaked wanted lists. A dump only ever shows two digits of a PIN and favours digits no earlier
/// dump showed, and an account turns up on a board it has not been on before when it can, so the whole PIN is
/// only found by reading every board.
/// </summary>
public sealed partial class CMUDeepLeakSystem : EntitySystem
{
    private static readonly ProtoId<DatasetPrototype> Handles = "CMUDeepHackerHandles";
    private static readonly TimeSpan FirstLeak = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan MaxInterval = TimeSpan.FromMinutes(9);
    private const int MinBalance = 300;
    private const int Richest = 8;
    private const int PinDigits = 4;
    private const int DigitsPerLeak = 2;
    private const int WantedPerLeak = 8;
    private const float WantedChance = 0.3f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CMUNetBoardSystem _boards = default!;
    [Dependency] private CMUUniversalRecordsSystem _universalRecords = default!;
    [Dependency] private StationRecordsSystem _records = default!;

    private TimeSpan _nextLeak;
    private readonly Dictionary<int, bool[]> _leakedDigits = new();
    private readonly Dictionary<int, HashSet<ProtoId<CMUNetSitePrototype>>> _leakedOn = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _nextLeak = TimeSpan.Zero;
        _leakedDigits.Clear();
        _leakedOn.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Leaks) || !_cfg.GetCVar(CMUPersonalComputerCVars.DeepWeb))
            return;

        var now = _timing.CurTime;
        var fast = _cfg.GetCVar(CMUPersonalComputerDevCVars.DevFastLeaks);
        if (_nextLeak == TimeSpan.Zero)
        {
            _nextLeak = now + (fast ? TimeSpan.FromSeconds(30) : FirstLeak + NextInterval());
            return;
        }

        if (now < _nextLeak)
            return;

        _nextLeak = now + (fast ? TimeSpan.FromMinutes(1) : NextInterval());
        Leak();
    }

    private TimeSpan NextInterval()
    {
        return MinInterval + (MaxInterval - MinInterval) * _random.NextDouble();
    }

    /// <summary>Posts one leak to a random leak board, a wanted list now and then, else an ATM dump.</summary>
    public void Leak()
    {
        var boards = LeakBoards();
        if (boards.Count == 0)
            return;

        if (_random.Prob(WantedChance) && TryLeakWanted(boards))
            return;

        if (!TryLeakAccounts(boards))
            TryLeakWanted(boards);
    }

    private List<CMUNetSitePrototype> LeakBoards()
    {
        var boards = new List<CMUNetSitePrototype>();
        foreach (var site in _prototype.EnumeratePrototypes<CMUNetSitePrototype>())
        {
            if (site.Board is { Leaks: true })
                boards.Add(site);
        }

        return boards;
    }

    /// <summary>A dump of one to three of the richest accounts, each with a name, number, rough balance and half a PIN.</summary>
    public bool TryLeakAccounts(List<CMUNetSitePrototype> boards)
    {
        var accounts = new List<IdCardComponent>();
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out _, out var card))
        {
            if (card.AccountNumber != 0 && card.OriginalOwner != null && card.AccountBalance >= MinBalance)
                accounts.Add(card);
        }

        if (accounts.Count == 0)
            return false;

        var board = _random.Pick(boards);
        accounts.Sort((a, b) =>
        {
            int left = a.AccountBalance, right = b.AccountBalance;
            return right.CompareTo(left);
        });
        var richest = accounts.Take(Richest).ToList();
        _random.Shuffle(richest);

        // Accounts this board has not carried yet go first.
        var picked = richest
            .OrderBy(card => _leakedOn.TryGetValue(card.AccountNumber, out var on) && on.Contains(board.ID))
            .Take(_random.Next(1, 4))
            .ToList();

        var lines = new StringBuilder();
        foreach (var card in picked)
        {
            var name = card.FullName ?? (card.OriginalOwner is { } owner ? Name(owner) : Loc.GetString("cmu-deep-leak-unknown"));
            var balance = card.AccountBalance / 100 * 100;
            int account = card.AccountNumber;
            lines.AppendLine(Loc.GetString("cmu-deep-leak-atm-line",
                ("name", name),
                ("account", account.ToString()),
                ("balance", balance.ToString()),
                ("pin", MaskPin(card))));
            Remember(_leakedOn, card.AccountNumber).Add(board.ID);
        }

        Post(board, Loc.GetString("cmu-deep-leak-atm-title", ("dump", _random.Next(1000, 10000).ToString())),
            Loc.GetString("cmu-deep-leak-atm-body", ("lines", lines.ToString().TrimEnd())));
        return true;
    }

    /// <summary>Two digits of the PIN, the rest blanked. Digits no dump has shown yet are picked first.</summary>
    public string MaskPin(IdCardComponent card)
    {
        int code = card.AtmPin;
        var pin = code.ToString("D4");
        if (!_leakedDigits.TryGetValue(card.AccountNumber, out var known))
            _leakedDigits[card.AccountNumber] = known = new bool[PinDigits];

        var positions = Enumerable.Range(0, PinDigits).ToList();
        _random.Shuffle(positions);
        positions = positions.OrderBy(i => known[i]).Take(DigitsPerLeak).ToList();

        var mask = new StringBuilder();
        for (var i = 0; i < PinDigits; i++)
        {
            if (i > 0)
                mask.Append(' ');

            var shown = positions.Contains(i);
            known[i] |= shown;
            mask.Append(shown ? pin[i] : '_');
        }

        return mask.ToString();
    }

    /// <summary>The colony's wanted, hostile and suspected records.</summary>
    public bool TryLeakWanted(List<CMUNetSitePrototype> boards)
    {
        if (_universalRecords.GetRecords() is not { } station)
            return false;

        var lines = new StringBuilder();
        var count = 0;
        foreach (var (id, record) in _records.GetRecordsOfType<CriminalRecord>(station))
        {
            if (record.Status is not (SecurityStatus.Wanted or SecurityStatus.Hostile or SecurityStatus.Suspected))
                continue;

            var key = new StationRecordKey(id, station);
            var name = _records.TryGetRecord<GeneralStationRecord>(key, out var general)
                ? general.Name
                : Loc.GetString("cmu-deep-leak-unknown");
            lines.AppendLine(Loc.GetString("cmu-deep-leak-wanted-line",
                ("name", name),
                ("status", record.Status.ToString().ToUpperInvariant()),
                ("reason", string.IsNullOrWhiteSpace(record.Reason) ? "-" : record.Reason),
                ("bounty", record.Bounty.ToString())));

            if (++count >= WantedPerLeak)
                break;
        }

        if (count == 0)
            return false;

        Post(_random.Pick(boards), Loc.GetString("cmu-deep-leak-wanted-title"),
            Loc.GetString("cmu-deep-leak-wanted-body", ("lines", lines.ToString().TrimEnd())));
        return true;
    }

    private void Post(CMUNetSitePrototype board, string title, string body)
    {
        var handle = _random.Pick(_prototype.Index(Handles).Values);
        _boards.PostAs(board.ID, handle, Loc.GetString("cmu-deep-leak-role"), title, body);
    }

    private static HashSet<ProtoId<CMUNetSitePrototype>> Remember(
        Dictionary<int, HashSet<ProtoId<CMUNetSitePrototype>>> seen,
        int account)
    {
        if (!seen.TryGetValue(account, out var boards))
            seen[account] = boards = new HashSet<ProtoId<CMUNetSitePrototype>>();

        return boards;
    }
}
