using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Staffing;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Utilities;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/staff")]
public sealed class StaffController(StaffService staff) : ControllerBase
{
    /// <summary>Public: who can I book with? (names and titles only)</summary>
    [HttpGet("public"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PublicStaffDto>>> Public(CancellationToken ct) => Ok(await staff.ListPublicAsync(ct));

    [HttpGet, Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<IReadOnlyList<StaffDto>>> List(CancellationToken ct) => Ok(await staff.ListAsync(ct));

    [HttpPost, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<StaffDto>> Create(CreateStaffRequest request, CancellationToken ct) =>
        Ok(await staff.CreateAsync(request, HttpContext.ClientIp(), ct));

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<StaffDto>> Update(Guid id, UpdateStaffRequest request, CancellationToken ct) =>
        Ok(await staff.UpdateAsync(id, request, HttpContext.ClientIp(), ct));
}
