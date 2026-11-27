using Platform.API.Core.Interfaces;
using Platform.API.Shared.Constants;

namespace Platform.API.Infrastructure.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ParseGuid(CustomClaims.Subject);
    public Guid? TenantId => ParseGuid(CustomClaims.TenantId);
    public string? Role => Principal?.FindFirst(CustomClaims.Role)?.Value;

    private Guid? ParseGuid(string claim) =>
        Guid.TryParse(Principal?.FindFirst(claim)?.Value, out var id) ? id : null;
}
