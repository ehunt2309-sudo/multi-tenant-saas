using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Responses;
using Platform.API.Shared.Utilities;

namespace Platform.API.Modules.Catalog;

/// <summary>Something customers can book: a massage, a haircut, a table, a tee time...</summary>
public sealed class ServiceOffering : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Something sold in venue: beauty products, merchandise, pro-shop items.</summary>
public sealed class Product : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record ServiceOfferingDto(Guid Id, string Name, string? Description, int DurationMinutes, decimal Price, bool IsActive);

public sealed record ServiceOfferingRequest(
    [property: Required, StringLength(150)] string Name,
    [property: StringLength(2000)] string? Description,
    [property: Range(5, 1440)] int DurationMinutes,
    [property: Range(0, 1000000)] decimal Price,
    bool IsActive = true);

public sealed record ProductDto(Guid Id, string Name, string? Description, decimal Price, int StockQuantity, string? ImageUrl, bool IsActive);

public sealed record ProductRequest(
    [property: Required, StringLength(150)] string Name,
    [property: StringLength(2000)] string? Description,
    [property: Range(0, 1000000)] decimal Price,
    [property: Range(0, 1000000)] int StockQuantity,
    [property: StringLength(500)] string? ImageUrl,
    bool IsActive = true);

public sealed record StockAdjustRequest([property: Range(-100000, 100000)] int Delta);

public sealed class ServiceOfferingService(AppDbContext db)
{
    private static ServiceOfferingDto ToDto(ServiceOffering s) => new(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.IsActive);

    public async Task<IReadOnlyList<ServiceOfferingDto>> ListAsync(bool includeInactive, CancellationToken ct) =>
        await db.ServiceOfferings.AsNoTracking().Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.Name).Take(500)
            .Select(s => new ServiceOfferingDto(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.IsActive))
            .ToListAsync(ct);

    public async Task<ServiceOfferingDto> CreateAsync(ServiceOfferingRequest r, CancellationToken ct)
    {
        var s = new ServiceOffering();
        Apply(s, r);
        db.ServiceOfferings.Add(s);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<ServiceOfferingDto> UpdateAsync(Guid id, ServiceOfferingRequest r, CancellationToken ct)
    {
        var s = await db.ServiceOfferings.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Service not found.");
        Apply(s, r);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    /// <summary>Soft delete: bookings keep pointing at the service they were made for.</summary>
    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var s = await db.ServiceOfferings.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Service not found.");
        s.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(ServiceOffering s, ServiceOfferingRequest r)
    {
        s.Name = r.Name.Trim();
        s.Description = r.Description?.Trim();
        s.DurationMinutes = r.DurationMinutes;
        s.Price = r.Price;
        s.IsActive = r.IsActive;
    }
}

public sealed class ProductService(AppDbContext db)
{
    private static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.ImageUrl, p.IsActive);

    public Task<PagedResult<ProductDto>> ListAsync(bool includeInactive, int page, int pageSize, CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => includeInactive || p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.ImageUrl, p.IsActive))
            .ToPagedAsync(page, pageSize, ct);

    public async Task<ProductDto> CreateAsync(ProductRequest r, CancellationToken ct)
    {
        var p = new Product();
        Apply(p, r);
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductRequest r, CancellationToken ct)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Product not found.");
        Apply(p, r);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    /// <summary>Atomic stock change in SQL so concurrent staff can't overwrite each other.</summary>
    public async Task<ProductDto> AdjustStockAsync(Guid id, int delta, CancellationToken ct)
    {
        var rows = await db.Products.Where(p => p.Id == id && p.StockQuantity + delta >= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + delta), ct);

        if (rows == 0)
        {
            if (!await db.Products.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("Product not found.");
            throw new BadRequestException("Stock cannot go below zero.");
        }
        return ToDto(await db.Products.AsNoTracking().FirstAsync(p => p.Id == id, ct));
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Product not found.");
        p.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Product p, ProductRequest r)
    {
        if (!string.IsNullOrWhiteSpace(r.ImageUrl) && !TextHelper.IsSafeHttpsUrl(r.ImageUrl))
            throw new BadRequestException("ImageUrl must be an https URL.");

        p.Name = r.Name.Trim();
        p.Description = r.Description?.Trim();
        p.Price = r.Price;
        p.StockQuantity = r.StockQuantity;
        p.ImageUrl = string.IsNullOrWhiteSpace(r.ImageUrl) ? null : r.ImageUrl.Trim();
        p.IsActive = r.IsActive;
    }
}
