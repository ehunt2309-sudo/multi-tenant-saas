using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.API.Modules.Cms;
using Platform.API.Shared.Constants;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/pages")]
public sealed class PagesController(PageService pages) : ControllerBase
{
    /// <summary>Public: published page content for the venue's website.</summary>
    [HttpGet("{slug}"), AllowAnonymous]
    public async Task<ActionResult<PageDto>> GetPublished(string slug, CancellationToken ct) => Ok(await pages.GetPublishedAsync(slug, ct));

    [HttpGet, Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<IReadOnlyList<PageSummaryDto>>> List(CancellationToken ct) => Ok(await pages.ListAsync(ct));

    [HttpGet("{slug}/edit"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<PageDto>> GetForEdit(string slug, CancellationToken ct) => Ok(await pages.GetAsync(slug, ct));

    [HttpPut("{slug}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<ActionResult<PageDto>> Upsert(string slug, UpsertPageRequest request, CancellationToken ct) => Ok(await pages.UpsertAsync(slug, request, ct));

    [HttpDelete("{slug}"), Authorize(Policy = Policies.ManagerOnly)]
    public async Task<IActionResult> Delete(string slug, CancellationToken ct)
    {
        await pages.DeleteAsync(slug, ct);
        return NoContent();
    }
}
