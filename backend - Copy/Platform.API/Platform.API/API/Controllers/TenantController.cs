using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Api.Middleware;
using Platform.API.Modules.Tenants;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Responses;
using Platform.API.Shared.Utilities;

namespace Platform.API.Api.Controllers;

/// <summary>Branding/settings for the venue whose website is calling.</summary>
[ApiController]
[Route("api/tenant")]
public sealed class TenantController(TenantService tenants) : ControllerBase
{
    [HttpGet("public"), AllowAnonymous]
    public async Task<ActionResult<PublicTenantDto>> GetPublic(CancellationToken ct) => Ok(await tenants.GetPublicAsync(ct));

    [HttpPut("settings"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<PublicTenantDto>> UpdateSettings(UpdateTenantSettingsRequest request, CancellationToken ct) =>
        Ok(await tenants.UpdateSettingsAsync(request, ct));
}

/// <summary>Operator-only: onboard and manage client businesses.</summary>
[ApiController]
[Route("api/platform/tenants")]
[Authorize(Policy = Policies.PlatformAdminOnly)]
[AllowNoTenant]
public sealed class PlatformTenantsController(TenantService tenants) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TenantSummaryDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await tenants.ListAsync(page, pageSize, ct));

    [HttpPost]
    public async Task<ActionResult<TenantSummaryDto>> Create(CreateTenantRequest request, CancellationToken ct) =>
        Ok(await tenants.CreateAsync(request, HttpContext.ClientIp(), ct));

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, SetTenantActiveRequest request, CancellationToken ct)
    {
        await tenants.SetActiveAsync(id, request.IsActive, HttpContext.ClientIp(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/domains")]
    public async Task<IActionResult> AddDomain(Guid id, AddDomainRequest request, CancellationToken ct)
    {
        await tenants.AddDomainAsync(id, request.Host, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/domains/{host}")]
    public async Task<IActionResult> RemoveDomain(Guid id, string host, CancellationToken ct)
    {
        await tenants.RemoveDomainAsync(id, host, ct);
        return NoContent();
    }
}
