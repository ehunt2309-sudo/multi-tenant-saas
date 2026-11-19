using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Analytics;
using Platform.API.Shared.Constants;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = Policies.ManagerOnly)]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    private static DateTimeOffset DefaultFrom(DateTimeOffset? from) => from ?? DateTimeOffset.UtcNow.AddDays(-30);
    private static DateTimeOffset DefaultTo(DateTimeOffset? to) => to ?? DateTimeOffset.UtcNow;

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryDto>> Summary([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        Ok(await analytics.SummaryAsync(DefaultFrom(from), DefaultTo(to), ct));

    [HttpGet("revenue-by-day")]
    public async Task<ActionResult<IReadOnlyList<DailyRevenueDto>>> RevenueByDay([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        Ok(await analytics.RevenueByDayAsync(DefaultFrom(from), DefaultTo(to), ct));

    [HttpGet("top-customers")]
    public async Task<ActionResult<IReadOnlyList<TopCustomerDto>>> TopCustomers([FromQuery] int take = 10, CancellationToken ct = default) =>
        Ok(await analytics.TopCustomersAsync(take, ct));

    [HttpGet("demographics")]
    public async Task<ActionResult<IReadOnlyList<DemographicRowDto>>> Demographics(CancellationToken ct) =>
        Ok(await analytics.DemographicsAsync(ct));
}
