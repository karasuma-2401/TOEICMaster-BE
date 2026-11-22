using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge")]
public sealed class KnowledgeController(ISender sender, IKnowledgeReader reader) : ControllerBase
{
    [HttpGet("articles")]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? skill,
        [FromQuery] Guid? categoryId, [FromQuery] string? tag, [FromQuery] string? sort,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        var page = await sender.Send(new GetAdminKnowledgeArticlesQuery(new(q, skill, categoryId, tag, "Published",
            sort ?? "publishedAt:desc", pageNumber, pageSize)), ct);
        return Ok(page with { Items = page.Items.Select(PublicSummary).ToArray() });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) => Ok(await reader.CategoriesAsync(ct));

    [HttpGet("articles/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var id = await reader.FindPublishedAsync(slug, ct);
        if (id is null) return NotFound();
        var article = await sender.Send(new GetAdminKnowledgeArticleQuery(id.Value), ct);
        return article.Status == "Published" ? Ok(article with { CoverImageUrl = PublicImage(article.CoverImageAssetId) }) : NotFound();
    }

    [HttpGet("articles/{slug}/related")]
    public async Task<IActionResult> Related(string slug, CancellationToken ct)
    {
        var id = await reader.FindPublishedAsync(slug, ct);
        return id is null ? NotFound() : Ok((await reader.RelatedAsync(id.Value, ct)).Select(PublicSummary));
    }

    [HttpGet("media/{assetId:guid}")]
    public async Task<IActionResult> Image(Guid assetId, CancellationToken ct)
    {
        if (!await reader.IsPublicMediaAsync(assetId, ct)) return NotFound();
        var image = await sender.Send(new GetAdminKnowledgeImageQuery(assetId), ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Data, image.ContentType);
    }

    [Authorize, HttpGet("saved")]
    public async Task<IActionResult> Saved(CancellationToken ct) => Ok(await reader.SavedAsync(UserId(), ct));
    [Authorize, HttpPut("saved/{articleId:guid}")]
    public async Task<IActionResult> Save(Guid articleId, CancellationToken ct)
    { await reader.SaveAsync(UserId(), articleId, ct); return NoContent(); }
    [Authorize, HttpDelete("saved/{articleId:guid}")]
    public async Task<IActionResult> Remove(Guid articleId, CancellationToken ct)
    { await reader.RemoveAsync(UserId(), articleId, ct); return NoContent(); }
    private Guid UserId() => User.GetRequiredUserId();
    private static string? PublicImage(Guid? id) => id is null ? null : $"/api/v1/knowledge/media/{id}";
    private static KnowledgeArticleSummaryDto PublicSummary(KnowledgeArticleSummaryDto article) =>
        article with { CoverImageUrl = PublicImage(article.CoverImageAssetId) };
}
