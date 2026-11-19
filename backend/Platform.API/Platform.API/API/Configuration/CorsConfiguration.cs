using Microsoft.AspNetCore.Cors.Infrastructure;
using Platform.API.Infrastructure.Tenancy;

namespace Platform.API.Api.Configuration;

public static class CorsConfiguration
{
    public static IServiceCollection AddPlatformCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();
        return services;
    }
}

/// <summary>
/// The frontends live on each client's own domain, so allowed origins can't be a fixed list.
/// An origin is allowed if it is in Cors:AllowedOrigins (your admin portal) or is a registered
/// domain of an ACTIVE tenant. Everything else gets no CORS headers, so browsers block it.
/// </summary>
public sealed class DynamicCorsPolicyProvider(IConfiguration config, IHostEnvironment env) : ICorsPolicyProvider
{
    private readonly HashSet<string> _static = (config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .Select(o => o.Trim().TrimEnd('/'))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin) || origin.Length > 255) return null;

        if (!_static.Contains(origin) && !await IsTenantOriginAsync(context, origin)) return null;

        return new CorsPolicyBuilder()
            .WithOrigins(origin)                                           // echo exactly one origin, never "*"
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "X-Tenant-Host")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
            .Build();                                                      // no credentials/cookies are used
    }

    private async Task<bool> IsTenantOriginAsync(HttpContext context, string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && !(env.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp)) return false;

        var lookup = context.RequestServices.GetRequiredService<ITenantLookup>();
        return await lookup.ResolveByHostAsync(uri.Authority.ToLowerInvariant(), context.RequestAborted) is not null;
    }
}
