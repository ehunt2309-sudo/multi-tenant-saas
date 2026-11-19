using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Utilities;

namespace Platform.API.Modules.Cms;

/// <summary>Editable page content (home, about, location...). Content is JSON so each bespoke frontend can shape it.</summary>
public sealed class CmsPage : AuditableEntity
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string ContentJson { get; set; } = "{}";
    public bool IsPublished { get; set; }
}

public sealed record PageDto(string Slug, string Title, JsonElement Content, bool IsPublished, DateTime? UpdatedAt);

public sealed record PageSummaryDto(string Slug, string Title, bool IsPublished, DateTime? UpdatedAt);

public sealed record UpsertPageRequest(
    [property: Required, StringLength(150)] string Title,
    JsonElement Content,
    bool IsPublished);

public sealed class PageService(AppDbContext db)
{
    private const int MaxContentChars = 200_000;

    private static PageDto ToDto(CmsPage p) => new(p.Slug, p.Title, JsonDocument.Parse(p.ContentJson).RootElement.Clone(), p.IsPublished, p.UpdatedAt ?? p.CreatedAt);

    public async Task<PageDto> GetPublishedAsync(string slug, CancellationToken ct) =>
        ToDto(await db.CmsPages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct)
              ?? throw new NotFoundException("Page not found."));

    public async Task<PageDto> GetAsync(string slug, CancellationToken ct) =>
        ToDto(await db.CmsPages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct)
              ?? throw new NotFoundException("Page not found."));

    public async Task<IReadOnlyList<PageSummaryDto>> ListAsync(CancellationToken ct) =>
        await db.CmsPages.AsNoTracking().OrderBy(p => p.Slug).Take(500)
            .Select(p => new PageSummaryDto(p.Slug, p.Title, p.IsPublished, p.UpdatedAt ?? p.CreatedAt))
            .ToListAsync(ct);

    public async Task<PageDto> UpsertAsync(string slug, UpsertPageRequest r, CancellationToken ct)
    {
        slug = slug.Trim().ToLowerInvariant();
        if (slug.Length is 0 or > 100 || !TextHelper.SlugRegex().IsMatch(slug))
            throw new BadRequestException("Slug may only contain lowercase letters, digits and hyphens.");

        var json = r.Content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "{}" : r.Content.GetRawText();
        if (json.Length > MaxContentChars) throw new BadRequestException("Page content is too large.");

        var page = await db.CmsPages.FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (page is null)
        {
            page = new CmsPage { Slug = slug };
            db.CmsPages.Add(page);
        }
        page.Title = r.Title.Trim();
        page.ContentJson = json;
        page.IsPublished = r.IsPublished;

        await db.SaveChangesAsync(ct);
        return ToDto(page);
    }

    public async Task DeleteAsync(string slug, CancellationToken ct)
    {
        var page = await db.CmsPages.FirstOrDefaultAsync(p => p.Slug == slug, ct) ?? throw new NotFoundException("Page not found.");
        db.CmsPages.Remove(page);
        await db.SaveChangesAsync(ct);
    }
}
