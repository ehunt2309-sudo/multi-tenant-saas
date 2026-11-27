using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Platform.API.Infrastructure.Persistence;

namespace Platform.API.Infrastructure.Tenancy;

public interface ITenantLookup
{
    /// <summary>Maps a website host (e.g. "spa-luxe.com" or "localhost:5173") to an ACTIVE tenant id.</summary>
    Task<Guid?> ResolveByHostAsync(string host, CancellationToken ct);
    void Invalidate(string host);
}

public sealed class TenantLookup(AppDbContext db, IMemoryCache cache) : ITenantLookup
{
    private static string Key(string host) => $"tenant-host:{host}";

    public async Task<Guid?> ResolveByHostAsync(string host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 255) return null;
        host = host.ToLowerInvariant();

        return await cache.GetOrCreateAsync<Guid?>(Key(host), async entry =>
        {
            entry.Size = 1; // cache has a SizeLimit so random hostnames can't exhaust memory
            var id = await db.TenantDomains.AsNoTracking()
                .Where(d => d.Host == host && d.Tenant.IsActive)
                .Select(d => (Guid?)d.TenantId)
                .FirstOrDefaultAsync(ct);
            // Short negative cache, longer positive cache.
            entry.AbsoluteExpirationRelativeToNow = id is null ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5);
            return id;
        });
    }

    public void Invalidate(string host) => cache.Remove(Key(host.ToLowerInvariant()));
}

public static class TenantHost
{
    /// <summary>
    /// Browsers always send Origin and it can't be forged by page JavaScript. Server-side callers
    /// (e.g. a Next.js server) can send X-Tenant-Host instead. Either way this only selects PUBLIC tenant
    /// data; authenticated access is always bound to the tenant inside the signed JWT.
    /// </summary>
    public static string? FromRequest(HttpRequest request)
    {
        var raw = request.Headers["X-Tenant-Host"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            var origin = request.Headers.Origin.FirstOrDefault();
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri)) raw = uri.Authority;
        }
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToLowerInvariant();
    }
}
