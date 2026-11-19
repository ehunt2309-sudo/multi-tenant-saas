namespace Platform.API.Shared.Utilities;

public static class HttpContextExtensions
{
    public static string? ClientIp(this HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString();
}
