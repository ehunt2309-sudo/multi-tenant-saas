using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Tenancy;
using Platform.API.Shared.Constants;

namespace Platform.API.Api.Middleware;

/// <summary>
/// Decides which tenant the request belongs to.
///  - Authenticated: the tenant inside the signed JWT is authoritative. A different venue's origin is rejected.
///  - Anonymous: the venue's website host (Origin / X-Tenant-Host) selects the tenant for public data.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ITenantProvider tenant, ITenantLookup lookup, ICurrentUser user)
    {
        Guid? hostTenant = null;
        var host = TenantHost.FromRequest(ctx.Request);
        if (host is not null)
            hostTenant = await lookup.ResolveByHostAsync(host, ctx.RequestAborted);

        if (user.IsAuthenticated)
        {
            if (user.TenantId is not { } claimTenant)
            {
                await Results.Problem(statusCode: 403, title: "Forbidden").ExecuteAsync(ctx);
                return;
            }

            if (user.Role != Roles.PlatformAdmin && hostTenant is not null && hostTenant != claimTenant)
            {
                await Results.Problem(statusCode: 403, title: "Forbidden",
                    detail: "This account does not belong to this venue.").ExecuteAsync(ctx);
                return;
            }

            tenant.SetTenant(claimTenant);
        }
        else if (hostTenant is not null)
        {
            tenant.SetTenant(hostTenant.Value);
        }

        await next(ctx);
    }
}
