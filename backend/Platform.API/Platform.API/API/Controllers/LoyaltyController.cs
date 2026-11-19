using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Loyalty;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Responses;
using Platform.API.Shared.Utilities;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/loyalty")]
public sealed class LoyaltyController(LoyaltyService loyalty) : ControllerBase
{
    [HttpGet("me"), Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<LoyaltyAccountDto>> Me(CancellationToken ct) => Ok(await loyalty.GetMyAccountAsync(ct));

    [HttpGet("me/history"), Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<PagedResult<LoyaltyTransactionDto>>> MyHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await loyalty.MyHistoryAsync(page, pageSize, ct));

    [HttpGet("customers/{customerId:guid}"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<LoyaltyAccountDto>> Get(Guid customerId, CancellationToken ct) => Ok(await loyalty.GetAccountAsync(customerId, ct));

    [HttpGet("customers/{customerId:guid}/history"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<PagedResult<LoyaltyTransactionDto>>> History(Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await loyalty.HistoryAsync(customerId, page, pageSize, ct));

    [HttpPost("customers/{customerId:guid}/award"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<LoyaltyAccountDto>> Award(Guid customerId, PointsRequest request, CancellationToken ct) =>
        Ok(await loyalty.AwardAsync(customerId, request, HttpContext.ClientIp(), ct));

    [HttpPost("customers/{customerId:guid}/redeem"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<LoyaltyAccountDto>> Redeem(Guid customerId, PointsRequest request, CancellationToken ct) =>
        Ok(await loyalty.RedeemAsync(customerId, request, HttpContext.ClientIp(), ct));
}
