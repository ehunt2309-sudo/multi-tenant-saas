using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Catalog;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Responses;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/services")]
public sealed class ServicesController(ServiceOfferingService services) : ControllerBase
{
    /// <summary>Public: what can be booked at this venue.</summary>
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ServiceOfferingDto>>> List(CancellationToken ct) => Ok(await services.ListAsync(false, ct));

    [HttpGet("all"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<IReadOnlyList<ServiceOfferingDto>>> ListAll(CancellationToken ct) => Ok(await services.ListAsync(true, ct));

    [HttpPost, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<ServiceOfferingDto>> Create(ServiceOfferingRequest request, CancellationToken ct) => Ok(await services.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<ServiceOfferingDto>> Update(Guid id, ServiceOfferingRequest request, CancellationToken ct) => Ok(await services.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await services.DeactivateAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await products.ListAsync(false, page, pageSize, ct));

    [HttpGet("all"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<PagedResult<ProductDto>>> ListAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await products.ListAsync(true, page, pageSize, ct));

    [HttpPost, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request, CancellationToken ct) => Ok(await products.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, ProductRequest request, CancellationToken ct) => Ok(await products.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/stock"), Authorize(Policy = Policies.StaffOrManager)]
    public async Task<ActionResult<ProductDto>> AdjustStock(Guid id, StockAdjustRequest request, CancellationToken ct) => Ok(await products.AdjustStockAsync(id, request.Delta, ct));

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await products.DeactivateAsync(id, ct);
        return NoContent();
    }
}
