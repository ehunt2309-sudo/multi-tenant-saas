using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Bookings;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Responses;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(BookingService bookings) : ControllerBase
{
    [HttpPost, Authorize(Policy = Policies.AnyTenantUser)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var created = await bookings.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet, Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<PagedResult<BookingDto>>> List(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] BookingStatus? status,
        [FromQuery] Guid? staffId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await bookings.ListAsync(from, to, status, staffId, page, pageSize, ct));

    [HttpGet("mine"), Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<PagedResult<BookingDto>>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await bookings.MineAsync(page, pageSize, ct));

    [HttpGet("{id:guid}"), Authorize(Policy = Policies.AnyTenantUser)]
    public async Task<ActionResult<BookingDto>> Get(Guid id, CancellationToken ct) => Ok(await bookings.GetAsync(id, ct));

    [HttpPatch("{id:guid}/status"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<BookingDto>> UpdateStatus(Guid id, UpdateBookingStatusRequest request, CancellationToken ct) =>
        Ok(await bookings.UpdateStatusAsync(id, request.Status, ct));

    [HttpPost("{id:guid}/cancel"), Authorize(Policy = Policies.AnyTenantUser)]
    public async Task<ActionResult<BookingDto>> Cancel(Guid id, CancellationToken ct) => Ok(await bookings.CancelAsync(id, ct));
}
