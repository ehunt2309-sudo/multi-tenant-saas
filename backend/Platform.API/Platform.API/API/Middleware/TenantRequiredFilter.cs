using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Platform.API.Core.Interfaces;

namespace Platform.API.Api.Middleware;

/// <summary>Marks the few endpoints that may run without a resolved tenant (platform admin, login, refresh).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowNoTenantAttribute : Attribute { }

/// <summary>Global filter: every endpoint needs a tenant unless it opts out.</summary>
public sealed class TenantRequiredFilter(ITenantProvider tenant) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var optedOut = context.ActionDescriptor.EndpointMetadata.OfType<AllowNoTenantAttribute>().Any();
        if (!optedOut && tenant.TenantId is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 400,
                Title = "Tenant not resolved",
                Detail = "Send requests from a registered venue website, or include the X-Tenant-Host header."
            })
            { StatusCode = 400 };
            return;
        }
        await next();
    }
}
