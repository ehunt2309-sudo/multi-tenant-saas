using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.API.Shared.Exceptions;

namespace Platform.API.Api.Middleware;

/// <summary>Single place where exceptions become RFC 7807 responses. Unexpected errors never leak details.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        int status;
        string title;
        string? detail = null;

        switch (ex)
        {
            case AppException app:
                status = app.Status; title = app.Title; detail = app.Message;
                break;
            case DbUpdateConcurrencyException:
                status = 409; title = "Conflict";
                detail = "This record was changed by someone else. Reload and try again.";
                break;
            case DbUpdateException:
                status = 409; title = "Conflict";
                detail = "The change conflicts with existing data.";
                logger.LogWarning(ex, "Database update conflict");
                break;
            case BadHttpRequestException bad:
                status = bad.StatusCode; title = "Bad request";
                break;
            default:
                status = 500; title = "An unexpected error occurred.";
                logger.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
                break;
        }

        ctx.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            Exception = ex,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
