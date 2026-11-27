using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/knowledge")]
[Authorize(Policy = "KnowledgeAdministration")]
public sealed class AdminKnowledgeController(ISender sender) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType<KnowledgeCategoryPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeCategoryPageDto>> GetCategories(
        [FromQuery] string? query,
        [FromQuery] bool? hasArticles,
        [FromQuery] string? sort,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetAdminKnowledgeCategoriesQuery(
            new KnowledgeAdminCategoryFilter(query, hasArticles, sort, pageNumber, pageSize)), cancellationToken));

    [HttpPost("categories")]
    [ProducesResponseType<KnowledgeCategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeCategoryDto>> CreateCategory(KnowledgeCategoryInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateKnowledgeCategoryCommand(CurrentUserId(), input), cancellationToken);
        return Created($"/api/v1/admin/knowledge/categories/{created.Id}", created);
    }

    [HttpPut("categories/{categoryId:guid}")]
    [ProducesResponseType<KnowledgeCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeCategoryDto>> UpdateCategory(Guid categoryId,
        VersionedKnowledgeCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(
            new UpdateKnowledgeCategoryCommand(CurrentUserId(), categoryId, request.Version, request.Category),
            cancellationToken));

    [HttpDelete("categories/{categoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(Guid categoryId, [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKnowledgeCategoryCommand(categoryId, version), cancellationToken);
        return NoContent();
    }

    [HttpGet("tags")]
    [ProducesResponseType<KnowledgeTagPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeTagPageDto>> GetTags(
        [FromQuery] string? query,
        [FromQuery] string? sort,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetAdminKnowledgeTagsQuery(
            new KnowledgeAdminTagFilter(query, sort, pageNumber, pageSize)), cancellationToken));

    [HttpGet("media")]
    [ProducesResponseType<KnowledgeMediaAssetPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeMediaAssetPageDto>> GetMediaAssets(
        [FromQuery] string? query,
        [FromQuery] bool? unusedOnly,
        [FromQuery] string? sort,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetAdminKnowledgeMediaAssetsQuery(
            new KnowledgeAdminMediaFilter(query, unusedOnly, sort, pageNumber, pageSize)), cancellationToken));

    [HttpPost("media/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(KnowledgeImageSignature.MaxBytes + 64 * 1024)]
    [ProducesResponseType<KnowledgeMediaAssetDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<KnowledgeMediaAssetDto>> UploadImage([FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
        {
            ModelState.AddModelError(nameof(file), "Image file cannot be empty.");
            return ValidationProblem(ModelState);
        }
        if (file.Length > KnowledgeImageSignature.MaxBytes)
        {
            return this.ProblemResponse(StatusCodes.Status413PayloadTooLarge,
                "Image exceeds the maximum limit of 5 MiB.");
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var created = await sender.Send(
            new UploadKnowledgeImageCommand(CurrentUserId(), stream.ToArray(), file.FileName), cancellationToken);
        return CreatedAtAction(nameof(GetImage), new { assetId = created.Id }, created);
    }

    [HttpGet("media/{assetId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid assetId, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetAdminKnowledgeImageQuery(assetId), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Data, image.ContentType);
    }

    [HttpDelete("media/{assetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteImage(Guid assetId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKnowledgeMediaAssetCommand(assetId), cancellationToken);
        return NoContent();
    }

    [HttpGet("articles")]
    [ProducesResponseType<KnowledgeArticlePageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeArticlePageDto>> GetArticles(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? skill,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? tag,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetAdminKnowledgeArticlesQuery(
            new KnowledgeAdminArticleFilter(query, skill, categoryId, tag, status, sort, pageNumber, pageSize)),
            cancellationToken));

    [HttpGet("articles/{articleId:guid}")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> GetArticle(Guid articleId,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAdminKnowledgeArticleQuery(articleId), cancellationToken));

    [HttpPost("articles")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> CreateArticle(KnowledgeArticleInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateKnowledgeArticleCommand(CurrentUserId(), input), cancellationToken);
        return CreatedAtAction(nameof(GetArticle), new { articleId = created.Id }, created);
    }

    [HttpPut("articles/{articleId:guid}")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> UpdateArticle(Guid articleId,
        VersionedKnowledgeArticleRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(
            new UpdateKnowledgeArticleCommand(CurrentUserId(), articleId, request.Version, request.Article),
            cancellationToken));

    [HttpPost("articles/{articleId:guid}/publish")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> PublishArticle(Guid articleId,
        KnowledgeVersionRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new PublishKnowledgeArticleCommand(CurrentUserId(), articleId, request.Version),
            cancellationToken));

    [HttpPost("articles/{articleId:guid}/unpublish")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> UnpublishArticle(Guid articleId,
        KnowledgeVersionRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UnpublishKnowledgeArticleCommand(CurrentUserId(), articleId, request.Version),
            cancellationToken));

    [HttpPost("articles/{articleId:guid}/archive")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> ArchiveArticle(Guid articleId,
        KnowledgeVersionRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ArchiveKnowledgeArticleCommand(CurrentUserId(), articleId, request.Version),
            cancellationToken));

    [HttpDelete("articles/{articleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteArticle(Guid articleId, [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKnowledgeArticleCommand(articleId, version), cancellationToken);
        return NoContent();
    }

    private Guid CurrentUserId() => User.GetRequiredUserId();
}

public sealed record VersionedKnowledgeCategoryRequest(int Version, KnowledgeCategoryInput Category);

public sealed record VersionedKnowledgeArticleRequest(int Version, KnowledgeArticleInput Article);

public sealed record KnowledgeVersionRequest(int Version);
