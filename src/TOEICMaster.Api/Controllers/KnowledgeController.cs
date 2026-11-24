using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge")]
public sealed class KnowledgeController(ISender sender) : ControllerBase
{
    [HttpGet("articles")]
    [ProducesResponseType<KnowledgeArticlePageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? skill,
        [FromQuery] Guid? categoryId, [FromQuery] string? tag, [FromQuery] string? sort,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetPublicKnowledgeArticlesQuery(new(q, skill, categoryId, tag, "Published",
            sort, pageNumber, pageSize)), ct));

    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<KnowledgeCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(await sender.Send(new GetPublicKnowledgeCategoriesQuery(), ct));

    [HttpGet("articles/{slug}")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct) =>
        Ok(await sender.Send(new GetPublicKnowledgeArticleQuery(slug), ct));

    [HttpGet("articles/{slug}/related")]
    [ProducesResponseType<IReadOnlyList<KnowledgeArticleSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Related(string slug, CancellationToken ct) =>
        Ok(await sender.Send(new GetRelatedKnowledgeArticlesQuery(slug), ct));

    [HttpGet("media/{assetId:guid}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Image(Guid assetId, CancellationToken ct)
    {
        var image = await sender.Send(new GetPublicKnowledgeImageQuery(assetId), ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Data, image.ContentType);
    }

    [Authorize, HttpGet("saved")]
    [ProducesResponseType<SavedKnowledgeArticlePage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Saved([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12,
        [FromQuery] Guid? articleId = null, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetSavedKnowledgeArticlesQuery(UserId(), pageNumber, pageSize, articleId), ct));

    [Authorize, HttpPut("saved/{articleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Save(Guid articleId, CancellationToken ct)
    {
        await sender.Send(new SaveKnowledgeArticleCommand(UserId(), articleId), ct);
        return NoContent();
    }

    [Authorize, HttpDelete("saved/{articleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove(Guid articleId, CancellationToken ct)
    {
        await sender.Send(new RemoveSavedKnowledgeArticleCommand(UserId(), articleId), ct);
        return NoContent();
    }

    private Guid UserId() => User.GetRequiredUserId();
}
