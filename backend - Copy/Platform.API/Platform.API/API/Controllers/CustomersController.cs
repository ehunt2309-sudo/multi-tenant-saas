using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Customers;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Responses;
using Platform.API.Shared.Utilities;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await customers.ListAsync(search, page, pageSize, ct));

    [HttpGet("me"), Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<CustomerDto>> Me(CancellationToken ct) => Ok(await customers.GetMeAsync(ct));

    [HttpPut("me"), Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<CustomerDto>> UpdateMe(UpdateCustomerRequest request, CancellationToken ct) => Ok(await customers.UpdateMeAsync(request, ct));

    [HttpGet("{id:guid}"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken ct) => Ok(await customers.GetAsync(id, ct));

    [HttpPost, Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, UpdateCustomerRequest request, CancellationToken ct) => Ok(await customers.UpdateAsync(id, request, ct));

    /// <summary>POPIA erasure request: removes personal data, keeps anonymous totals.</summary>
    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<IActionResult> Erase(Guid id, CancellationToken ct)
    {
        await customers.AnonymizeAsync(id, HttpContext.ClientIp(), ct);
        return NoContent();
    }
}
