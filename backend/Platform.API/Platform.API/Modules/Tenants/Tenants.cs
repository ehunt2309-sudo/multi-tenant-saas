using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Infrastructure.Security;
using Platform.API.Infrastructure.Tenancy;
using Platform.API.Modules.Accounts;
using Platform.API.Modules.Staffing;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Responses;
using Platform.API.Shared.Utilities;

namespace Platform.API.Modules.Tenants;

public enum Industry { Restaurant, Spa, Hairdresser, Hotel, GolfCourse, Other }

/// <summary>One client business (a spa, a restaurant...). Not tenant-scoped itself: it IS the tenant.</summary>
public sealed class Tenant : BaseEntity
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public Industry Industry { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? PrimaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string TimeZone { get; set; } = "Africa/Johannesburg";
    public decimal LoyaltyPointsPerCurrencyUnit { get; set; } = 1m;
    public List<TenantDomain> Domains { get; set; } = [];
}

/// <summary>A website host (e.g. "spa-luxe.com") that belongs to a tenant. Drives tenant resolution and CORS.</summary>
public sealed class TenantDomain : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Host { get; set; } = "";
    public bool IsPrimary { get; set; }
}

public sealed record PublicTenantDto(string Name, string Slug, Industry Industry, string? PrimaryColor, string? LogoUrl, string Currency, string TimeZone, decimal LoyaltyPointsPerCurrencyUnit);

public sealed record TenantSummaryDto(Guid Id, string Name, string Slug, Industry Industry, bool IsActive, DateTime CreatedAt, IReadOnlyList<string> Domains);

public sealed record CreateTenantRequest(
    [Required, StringLength(150)] string Name,
    [Required, StringLength(60)] string Slug,
    Industry Industry,
    [Required, StringLength(253)] string PrimaryDomain,
    [Required, EmailAddress, StringLength(256)] string ManagerEmail,
    [Required, StringLength(100)] string ManagerFirstName,
    [Required, StringLength(100)] string ManagerLastName,
    [Required, StringLength(128, MinimumLength = 12)] string ManagerPassword);

public sealed record UpdateTenantSettingsRequest(
    [StringLength(7)] string? PrimaryColor,
    [StringLength(500)] string? LogoUrl,
    [Range(0, 100)] decimal LoyaltyPointsPerCurrencyUnit);

public sealed record AddDomainRequest([Required, StringLength(253)] string Host);
public sealed record SetTenantActiveRequest(bool IsActive);

public sealed class TenantService(
    AppDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    PasswordService passwords,
    AuditService audit,
    ITenantLookup lookup)
{
    // ----- public / tenant-scoped -----
    public async Task<PublicTenantDto> GetPublicAsync(CancellationToken ct)
    {
        var id = tenant.TenantId ?? throw new BadRequestException("Tenant could not be resolved.");
        return await db.Tenants.AsNoTracking().Where(t => t.Id == id && t.IsActive)
                   .Select(t => new PublicTenantDto(t.Name, t.Slug, t.Industry, t.PrimaryColor, t.LogoUrl, t.Currency, t.TimeZone, t.LoyaltyPointsPerCurrencyUnit))
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Tenant not found.");
    }

    public async Task<PublicTenantDto> UpdateSettingsAsync(UpdateTenantSettingsRequest r, CancellationToken ct)
    {
        var id = tenant.TenantId ?? throw new BadRequestException("Tenant could not be resolved.");
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Tenant not found.");

        if (!string.IsNullOrWhiteSpace(r.PrimaryColor) && !TextHelper.HexColorRegex().IsMatch(r.PrimaryColor))
            throw new BadRequestException("PrimaryColor must look like #RRGGBB.");
        if (!string.IsNullOrWhiteSpace(r.LogoUrl) && !TextHelper.IsSafeHttpsUrl(r.LogoUrl))
            throw new BadRequestException("LogoUrl must be an https URL.");

        t.PrimaryColor = string.IsNullOrWhiteSpace(r.PrimaryColor) ? null : r.PrimaryColor;
        t.LogoUrl = string.IsNullOrWhiteSpace(r.LogoUrl) ? null : r.LogoUrl.Trim();
        t.LoyaltyPointsPerCurrencyUnit = r.LoyaltyPointsPerCurrencyUnit;
        await db.SaveChangesAsync(ct);
        return await GetPublicAsync(ct);
    }

    // ----- platform admin only -----
    public Task<PagedResult<TenantSummaryDto>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        db.Tenants.AsNoTracking().Where(t => t.Id != PlatformConstants.PlatformTenantId)
            .OrderBy(t => t.Name)
            .Select(t => new TenantSummaryDto(t.Id, t.Name, t.Slug, t.Industry, t.IsActive, t.CreatedAt,
                t.Domains.Select(d => d.Host).ToList()))
            .ToPagedAsync(page, pageSize, ct);

    /// <summary>Creates tenant + primary domain + first Manager login in ONE atomic save.</summary>
    public async Task<TenantSummaryDto> CreateAsync(CreateTenantRequest r, string? ip, CancellationToken ct)
    {
        var slug = r.Slug.Trim().ToLowerInvariant();
        if (!TextHelper.SlugRegex().IsMatch(slug) || slug == "platform")
            throw new BadRequestException("Slug is invalid or reserved.");

        var host = TextHelper.NormalizeHost(r.PrimaryDomain);
        if (!TextHelper.IsValidHost(host)) throw new BadRequestException("PrimaryDomain is not a valid host.");

        var email = r.ManagerEmail.Trim();
        passwords.EnforcePolicy(r.ManagerPassword, email);

        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct)) throw new ConflictException("Slug already in use.");
        if (await db.TenantDomains.AnyAsync(d => d.Host == host, ct)) throw new ConflictException("Domain already registered.");

        var t = new Tenant { Name = r.Name.Trim(), Slug = slug, Industry = r.Industry };
        t.Domains.Add(new TenantDomain { Host = host, IsPrimary = true });

        var manager = new User
        {
            TenantId = t.Id,
            Email = email,
            NormalizedEmail = TextHelper.NormalizeEmail(email),
            FirstName = r.ManagerFirstName.Trim(),
            LastName = r.ManagerLastName.Trim(),
            Role = Roles.Manager
        };
        manager.PasswordHash = passwords.Hash(manager, r.ManagerPassword);

        var staff = new StaffMember { TenantId = t.Id, UserId = manager.Id, JobTitle = "Manager" };

        // Switch the request's tenant context to the new tenant so the write guard accepts its rows.
        tenant.SetTenant(t.Id);
        db.AddRange(t, manager, staff);
        audit.Record(AuditActions.TenantCreated, currentUser.UserId, t.Id, ip, $"{slug} ({host})");
        await db.SaveChangesAsync(ct);

        return new TenantSummaryDto(t.Id, t.Name, t.Slug, t.Industry, t.IsActive, t.CreatedAt, [host]);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, string? ip, CancellationToken ct)
    {
        if (id == PlatformConstants.PlatformTenantId) throw new NotFoundException("Tenant not found.");
        var t = await db.Tenants.Include(x => x.Domains).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Tenant not found.");
        t.IsActive = isActive;
        audit.Record(AuditActions.TenantStatusChanged, currentUser.UserId, id, ip, isActive ? "activated" : "suspended");
        await db.SaveChangesAsync(ct);
        foreach (var d in t.Domains) lookup.Invalidate(d.Host);
    }

    public async Task AddDomainAsync(Guid id, string rawHost, CancellationToken ct)
    {
        var host = TextHelper.NormalizeHost(rawHost);
        if (!TextHelper.IsValidHost(host)) throw new BadRequestException("Host is not valid.");
        if (!await db.Tenants.AnyAsync(t => t.Id == id && t.Id != PlatformConstants.PlatformTenantId, ct))
            throw new NotFoundException("Tenant not found.");
        if (await db.TenantDomains.AnyAsync(d => d.Host == host, ct)) throw new ConflictException("Domain already registered.");

        db.TenantDomains.Add(new TenantDomain { TenantId = id, Host = host });
        await db.SaveChangesAsync(ct);
        lookup.Invalidate(host);
    }

    public async Task RemoveDomainAsync(Guid id, string rawHost, CancellationToken ct)
    {
        var host = TextHelper.NormalizeHost(rawHost);
        var d = await db.TenantDomains.FirstOrDefaultAsync(x => x.TenantId == id && x.Host == host, ct)
                ?? throw new NotFoundException("Domain not found.");
        if (d.IsPrimary) throw new BadRequestException("The primary domain cannot be removed.");
        db.TenantDomains.Remove(d);
        await db.SaveChangesAsync(ct);
        lookup.Invalidate(host);
    }
}
