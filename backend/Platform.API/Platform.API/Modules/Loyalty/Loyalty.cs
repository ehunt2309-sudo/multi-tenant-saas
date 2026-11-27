using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Modules.Accounts;
using Platform.API.Modules.Customers;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Responses;

namespace Platform.API.Modules.Loyalty;

public enum LoyaltyTier { Bronze, Silver, Gold, Platinum }
public enum LoyaltyTransactionType { Earned, Redeemed }

public sealed class LoyaltyAccount : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public int PointsBalance { get; set; }
    public int LifetimePoints { get; set; }
    public LoyaltyTier Tier { get; set; } = LoyaltyTier.Bronze;
    public byte[] RowVersion { get; set; } = [];   // optimistic concurrency: no lost/duplicated point updates
}

/// <summary>Append-only ledger. The account balance is a cached sum of these rows.</summary>
public sealed class LoyaltyTransaction : AuditableEntity
{
    public Guid LoyaltyAccountId { get; set; }
    public int Points { get; set; }                 // positive = earned, negative = redeemed
    public int BalanceAfter { get; set; }
    public LoyaltyTransactionType Type { get; set; }
    public string Reason { get; set; } = "";
    public Guid? BookingId { get; set; }
}

public sealed record LoyaltyAccountDto(Guid CustomerId, int PointsBalance, int LifetimePoints, LoyaltyTier Tier,
    LoyaltyTier? NextTier, int? PointsToNextTier);

public sealed record LoyaltyTransactionDto(Guid Id, int Points, int BalanceAfter, LoyaltyTransactionType Type,
    string Reason, DateTime CreatedAt);

public sealed record PointsRequest(
    [Range(1, 100000)] int Points,
    [Required, StringLength(200)] string Reason);

public sealed class LoyaltyService(AppDbContext db, ICurrentUser user, AuditService audit)
{
    private static readonly (LoyaltyTier Tier, int From)[] Tiers =
        [(LoyaltyTier.Bronze, 0), (LoyaltyTier.Silver, 1000), (LoyaltyTier.Gold, 5000), (LoyaltyTier.Platinum, 15000)];

    public static LoyaltyTier TierFor(int lifetime) => Tiers.Last(t => lifetime >= t.From).Tier;

    private static LoyaltyAccountDto ToDto(LoyaltyAccount a)
    {
        var next = Tiers.FirstOrDefault(t => t.From > a.LifetimePoints);
        var hasNext = next.From > 0;
        return new LoyaltyAccountDto(a.CustomerId, a.PointsBalance, a.LifetimePoints, a.Tier,
            hasNext ? next.Tier : null, hasNext ? next.From - a.LifetimePoints : null);
    }

    /// <summary>Stages a ledger entry + balance change. The CALLER saves, so it joins their transaction.</summary>
    public async Task ApplyAsync(Guid customerId, int signedPoints, LoyaltyTransactionType type, string reason,
        Guid? bookingId, CancellationToken ct)
    {
        var account = await db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.CustomerId == customerId, ct)
                      ?? throw new NotFoundException("Loyalty account not found.");

        var newBalance = account.PointsBalance + signedPoints;
        if (newBalance < 0) throw new BadRequestException("Customer does not have enough points.");

        account.PointsBalance = newBalance;
        if (signedPoints > 0) account.LifetimePoints += signedPoints;
        account.Tier = TierFor(account.LifetimePoints);

        db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyAccountId = account.Id,
            Points = signedPoints,
            BalanceAfter = newBalance,
            Type = type,
            Reason = reason,
            BookingId = bookingId
        });
    }

    public async Task<LoyaltyAccountDto> GetAccountAsync(Guid customerId, CancellationToken ct) =>
        ToDto(await db.LoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.CustomerId == customerId, ct)
              ?? throw new NotFoundException("Loyalty account not found."));

    public async Task<LoyaltyAccountDto> GetMyAccountAsync(CancellationToken ct) =>
        await GetAccountAsync(await db.CurrentCustomerIdAsync(user, ct), ct);

    public Task<PagedResult<LoyaltyTransactionDto>> HistoryAsync(Guid customerId, int page, int pageSize, CancellationToken ct) =>
        db.LoyaltyTransactions.AsNoTracking()
            .Where(t => db.LoyaltyAccounts.Any(a => a.Id == t.LoyaltyAccountId && a.CustomerId == customerId))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new LoyaltyTransactionDto(t.Id, t.Points, t.BalanceAfter, t.Type, t.Reason, t.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);

    public async Task<PagedResult<LoyaltyTransactionDto>> MyHistoryAsync(int page, int pageSize, CancellationToken ct) =>
        await HistoryAsync(await db.CurrentCustomerIdAsync(user, ct), page, pageSize, ct);

    public async Task<LoyaltyAccountDto> AwardAsync(Guid customerId, PointsRequest r, string? ip, CancellationToken ct)
    {
        if (r.Points > 10000) throw new BadRequestException("Manual awards are limited to 10,000 points.");
        await EnsureCustomerAsync(customerId, ct);
        await ApplyAsync(customerId, r.Points, LoyaltyTransactionType.Earned, r.Reason.Trim(), null, ct);
        audit.Record(AuditActions.LoyaltyAward, user.UserId, user.TenantId, ip, $"customer {customerId}: +{r.Points}");
        await db.SaveChangesAsync(ct);
        return await GetAccountAsync(customerId, ct);
    }

    public async Task<LoyaltyAccountDto> RedeemAsync(Guid customerId, PointsRequest r, string? ip, CancellationToken ct)
    {
        await EnsureCustomerAsync(customerId, ct);
        await ApplyAsync(customerId, -r.Points, LoyaltyTransactionType.Redeemed, r.Reason.Trim(), null, ct);
        audit.Record(AuditActions.LoyaltyRedeem, user.UserId, user.TenantId, ip, $"customer {customerId}: -{r.Points}");
        await db.SaveChangesAsync(ct);
        return await GetAccountAsync(customerId, ct);
    }

    private async Task EnsureCustomerAsync(Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId && !c.IsAnonymized, ct))
            throw new NotFoundException("Customer not found.");
    }
}

