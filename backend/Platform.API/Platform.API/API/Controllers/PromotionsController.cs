using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Promotions;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Responses;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/promotions")]
public sealed class PromotionsController(PromotionService promotions) : ControllerBase
{
    [HttpGet("active"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> Active(CancellationToken ct) => Ok(await promotions.ActiveAsync(ct));

    [HttpGet, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<PagedResult<PromotionDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await promotions.ListAsync(page, pageSize, ct));

    [HttpPost, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<PromotionDto>> Create(PromotionRequest request, CancellationToken ct) => Ok(await promotions.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<PromotionDto>> Update(Guid id, PromotionRequest request, CancellationToken ct) => Ok(await promotions.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await promotions.DeleteAsync(id, ct);
        return NoContent();
    }
}
