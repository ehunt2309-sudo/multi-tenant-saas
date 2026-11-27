using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Platform.API.Api.Configuration;

public static class RateLimitingConfiguration
{
    public const string AuthPolicy = "auth";

    private static string Client(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static IServiceCollection AddPlatformRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Everything: 300 requests/minute per client IP.
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(Client(ctx), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

            // Login / register / refresh: 10 per minute per IP (brute-force & credential-stuffing brake).
            o.AddPolicy(AuthPolicy, ctx =>
                RateLimitPartition.GetFixedWindowLimiter("auth:" + Client(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            o.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails { Status = 429, Title = "Too many requests", Detail = "Please slow down and try again shortly." }, ct);
            };
        });
        return services;
    }
}
