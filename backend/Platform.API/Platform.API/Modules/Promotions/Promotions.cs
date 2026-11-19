using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Responses;

namespace Platform.API.Modules.Promotions;

public enum DiscountType { None, Percentage, FixedAmount }

public sealed class Promotion : AuditableEntity
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record PromotionDto(Guid Id, string Title, string? Description, DateTime StartsAt, DateTime EndsAt,
    DiscountType DiscountType, decimal DiscountValue, bool IsActive);

public sealed record PromotionRequest(
    [property: Required, StringLength(150)] string Title,
    [property: StringLength(2000)] string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DiscountType DiscountType,
    [property: Range(0, 1000000)] decimal DiscountValue,
    bool IsActive = true);

public sealed class PromotionService(AppDbContext db)
{
    private static PromotionDto ToDto(Promotion p) => new(p.Id, p.Title, p.Description, p.StartsAt, p.EndsAt, p.DiscountType, p.DiscountValue, p.IsActive);

    public async Task<IReadOnlyList<PromotionDto>> ActiveAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.Promotions.AsNoTracking()
            .Where(p => p.IsActive && p.StartsAt <= now && p.EndsAt > now)
            .OrderBy(p => p.EndsAt).Take(100)
            .Select(p => new PromotionDto(p.Id, p.Title, p.Description, p.StartsAt, p.EndsAt, p.DiscountType, p.DiscountValue, p.IsActive))
            .ToListAsync(ct);
    }

    public Task<PagedResult<PromotionDto>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        db.Promotions.AsNoTracking().OrderByDescending(p => p.StartsAt)
            .Select(p => new PromotionDto(p.Id, p.Title, p.Description, p.StartsAt, p.EndsAt, p.DiscountType, p.DiscountValue, p.IsActive))
            .ToPagedAsync(page, pageSize, ct);

    public async Task<PromotionDto> CreateAsync(PromotionRequest r, CancellationToken ct)
    {
        var p = new Promotion();
        Apply(p, r);
        db.Promotions.Add(p);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<PromotionDto> UpdateAsync(Guid id, PromotionRequest r, CancellationToken ct)
    {
        var p = await db.Promotions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion not found.");
        Apply(p, r);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Promotions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion not found.");
        db.Promotions.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Promotion p, PromotionRequest r)
    {
        var start = r.StartsAt.UtcDateTime; var end = r.EndsAt.UtcDateTime;
        if (end <= start) throw new BadRequestException("EndsAt must be after StartsAt.");
        if (r.DiscountType == DiscountType.Percentage && r.DiscountValue > 100)
            throw new BadRequestException("A percentage discount cannot exceed 100.");

        p.Title = r.Title.Trim();
        p.Description = r.Description?.Trim();
        p.StartsAt = start;
        p.EndsAt = end;
        p.DiscountType = r.DiscountType;
        p.DiscountValue = r.DiscountType == DiscountType.None ? 0 : r.DiscountValue;
        p.IsActive = r.IsActive;
    }
}
