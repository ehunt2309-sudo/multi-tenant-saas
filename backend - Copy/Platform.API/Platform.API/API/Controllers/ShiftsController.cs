using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Scheduling;
using Platform.API.Shared.Constants;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/shifts")]
public sealed class ShiftsController(ShiftService shifts) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<IReadOnlyList<ShiftDto>>> List([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] Guid? staffId, CancellationToken ct) =>
        Ok(await shifts.ListAsync(from, to, staffId, ct));

    [HttpGet("mine"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<IReadOnlyList<ShiftDto>>> Mine([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct) =>
        Ok(await shifts.MineAsync(from, to, ct));

    [HttpPost, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<ShiftDto>> Create(CreateShiftRequest request, CancellationToken ct) => Ok(await shifts.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<ShiftDto>> Update(Guid id, UpdateShiftRequest request, CancellationToken ct) => Ok(await shifts.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await shifts.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/clock-in"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<ShiftDto>> ClockIn(Guid id, CancellationToken ct) => Ok(await shifts.ClockInAsync(id, ct));

    [HttpPost("{id:guid}/clock-out"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<ShiftDto>> ClockOut(Guid id, CancellationToken ct) => Ok(await shifts.ClockOutAsync(id, ct));

    /// <summary>Hours worked and pay earned per staff member (manager: everyone).</summary>
    [HttpGet("hours"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<IReadOnlyList<HoursReportRow>>> Hours([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] Guid? staffId, CancellationToken ct) =>
        Ok(await shifts.HoursAsync(from, to, staffId, ct));

    [HttpGet("hours/mine"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<IReadOnlyList<HoursReportRow>>> MyHours([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct) =>
        Ok(await shifts.MyHoursAsync(from, to, ct));
}
