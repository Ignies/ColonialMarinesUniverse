using Content.Shared.Access.Components;
using Content.Shared.AU14.ColonyEconomy;
using Robust.Shared.GameTiming;
using Robust.Shared.Random;

namespace Content.Server.AU14.ColonyEconomy;

/// <summary>
///     Central banking service. Handles account/PIN assignment,
///     the transaction log, and account lookups.
///     All ATM/admin operations go through this system.
/// </summary>
public sealed partial class ColonyBankSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const int PinMin = 1000;
    private const int PinMax = 9999;
    private const int AcctMin = 10000;
    private const int AcctMax = 99999;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Global transaction log, most-recent-last.
    ///     Kept in memory for the duration of the round.
    /// </summary>
    private readonly List<BankTransaction> _transactions = new();

    /// <summary>
    ///     Ensures the ID card has a valid account number and PIN, generating them if needed.
    /// </summary>
    public void EnsureAccountCredentials(EntityUid cardUid, IdCardComponent card)
    {
        var dirty = false;

        if (card.AccountNumber == 0)
        {
            card.AccountNumber = GenerateUniqueAccountNumber();
            dirty = true;
        }

        if (card.AtmPin == 0)
        {
            card.AtmPin = _random.Next(PinMin, PinMax + 1);
            dirty = true;
        }

        if (dirty)
            Dirty(cardUid, card);
    }

    /// <summary>
    ///     Looks up an ID card by account number. Returns null if not found.
    /// </summary>
    public (EntityUid uid, IdCardComponent card)? FindAccount(int accountNumber)
    {
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var card))
        {
            if (card.AccountNumber == accountNumber)
                return (uid, card);
        }
        return null;
    }

    /// <summary>
    ///     Returns a read-only snapshot of all known accounts for admin display.
    /// </summary>
    public List<BankAccountInfo> GetAllAccounts()
    {
        var result = new List<BankAccountInfo>();
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out _, out var card))
        {
            if (card.AccountNumber == 0) continue;
            result.Add(new BankAccountInfo
            {
                AccountNumber = card.AccountNumber,
                Name = card.FullName ?? "Unknown",
                JobTitle = card.LocalizedJobTitle ?? "Unknown",
                Balance = card.AccountBalance,
            });
        }
        return result;
    }

    /// <summary>
    ///     Returns all recorded transactions (newest-last).
    /// </summary>
    public List<BankTransaction> GetTransactions() => new(_transactions);

    /// <summary>
    ///     Records a transaction in the log.
    /// </summary>
    public void LogTransaction(int accountNumber, string accountName, BankTransactionType type,
        int amount, int? recipientAccount = null, string? note = null)
    {
        _transactions.Add(new BankTransaction
        {
            Timestamp = _timing.CurTime,
            AccountNumber = accountNumber,
            AccountName = accountName,
            Type = type,
            Amount = amount,
            RecipientAccount = recipientAccount,
            Note = note,
        });
    }

    /// <summary>
    ///     Verifies PIN and applies lockout on failure.
    ///     Returns true if authenticated, false if wrong/locked.
    ///     Out parameter <paramref name="locked"/> is true when the card just got locked.
    /// </summary>
    public bool TryAuthenticatePin(EntityUid cardUid, IdCardComponent card, int enteredPin, out bool locked)
    {
        locked = false;

        // Check lockout
        if (card.PinLockedUntil.HasValue && _timing.CurTime < card.PinLockedUntil.Value)
            return false;

        if (card.PinLockedUntil.HasValue && _timing.CurTime >= card.PinLockedUntil.Value)
        {
            // Lock expired — reset
            card.PinLockedUntil = null;
            card.PinAttempts = 0;
            Dirty(cardUid, card);
        }

        if (card.AtmPin == enteredPin)
        {
            card.PinAttempts = 0;
            Dirty(cardUid, card);
            return true;
        }

        card.PinAttempts++;
        if (card.PinAttempts >= 3)
        {
            card.PinLockedUntil = _timing.CurTime + LockDuration;
            locked = true;
        }

        Dirty(cardUid, card);
        return false;
    }

    /// <summary>
    ///     Whether a card is currently locked out.
    /// </summary>
    public bool IsLocked(IdCardComponent card, out TimeSpan? unlockAt)
    {
        if (card.PinLockedUntil.HasValue && _timing.CurTime < card.PinLockedUntil.Value)
        {
            unlockAt = card.PinLockedUntil;
            return true;
        }
        unlockAt = null;
        return false;
    }

    // ── Internal helpers ─────────────────────────────────────────────────

    private int GenerateUniqueAccountNumber()
    {
        // Try to generate a unique number (collision extremely rare but guard anyway)
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = _random.Next(AcctMin, AcctMax + 1);
            if (FindAccount(candidate) == null)
                return candidate;
        }
        // Fallback: just return a random one (edge case)
        return _random.Next(AcctMin, AcctMax + 1);
    }
}
