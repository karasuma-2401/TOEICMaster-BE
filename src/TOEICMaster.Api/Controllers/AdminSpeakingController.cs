using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/speaking")]
[Authorize(Policy = "SpeakingAdministration")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSpeakingController(ISender sender) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<SpeakingAdminSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakingAdminSummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSpeakingAdminSummaryQuery(), cancellationToken));

    [HttpGet("exercises")]
    [ProducesResponseType<SpeakingPageDto<SpeakingExerciseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakingPageDto<SpeakingExerciseDto>>> GetExercises([FromQuery] int? part,
        [FromQuery] string? query, [FromQuery] string? difficulty, [FromQuery] string? status,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetSpeakingExercisesQuery(new SpeakingExerciseFilter(part, query, difficulty, status,
            pageNumber, pageSize)), cancellationToken));

    [HttpGet("exercises/{exerciseId:guid}")]
    [ProducesResponseType<SpeakingExerciseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpeakingExerciseDto>> GetExercise(Guid exerciseId,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSpeakingExerciseQuery(exerciseId), cancellationToken));

    [HttpPost("exercises")]
    [ProducesResponseType<SpeakingExerciseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpeakingExerciseDto>> CreateExercise(SpeakingExerciseInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateSpeakingExerciseCommand(CurrentUserId(), input), cancellationToken);
        return CreatedAtAction(nameof(GetExercise), new { exerciseId = created.Id }, created);
    }

    [HttpPut("exercises/{exerciseId:guid}")]
    [ProducesResponseType<SpeakingExerciseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpeakingExerciseDto>> UpdateExercise(Guid exerciseId,
        VersionedSpeakingExerciseRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateSpeakingExerciseCommand(CurrentUserId(), exerciseId, request.Version,
            request.Exercise ?? MissingExercise), cancellationToken));

    [HttpDelete("exercises/{exerciseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveExercise(Guid exerciseId, [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ArchiveSpeakingExerciseCommand(CurrentUserId(), exerciseId, version), cancellationToken);
        return NoContent();
    }

    [HttpPost("media/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SpeakingMediaRules.MaxImageBytes + 64 * 1024)]
    [ProducesResponseType<SpeakingImageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<SpeakingImageDto>> UploadImage(IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Cần chọn một ảnh JPEG hoặc PNG.");
        if (file.Length > SpeakingMediaRules.MaxImageBytes)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Ảnh không được vượt quá 5 MiB.");
        var created = await sender.Send(new UploadSpeakingImageCommand(CurrentUserId(),
            await ReadAsync(file, cancellationToken), file.ContentType), cancellationToken);
        return CreatedAtAction(nameof(GetImage), new { assetId = created.Id }, created);
    }

    [HttpGet("media/{assetId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid assetId, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetSpeakingAdminImageQuery(assetId), cancellationToken);
        return PrivateFile(image);
    }

    [HttpGet("tests")]
    [ProducesResponseType<SpeakingPageDto<SpeakingTestListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakingPageDto<SpeakingTestListItemDto>>> GetTests([FromQuery] string? query,
        [FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetSpeakingTestsQuery(new SpeakingTestFilter(query, status, pageNumber, pageSize)),
            cancellationToken));

    [HttpGet("tests/{testId:guid}")]
    [ProducesResponseType<SpeakingTestDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpeakingTestDetailDto>> GetTest(Guid testId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSpeakingTestQuery(testId), cancellationToken));

    [HttpPost("tests")]
    [ProducesResponseType<SpeakingTestDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpeakingTestDetailDto>> CreateTest(SpeakingTestInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateSpeakingTestCommand(CurrentUserId(), input), cancellationToken);
        return CreatedAtAction(nameof(GetTest), new { testId = created.Id }, created);
    }

    [HttpPut("tests/{testId:guid}")]
    [ProducesResponseType<SpeakingTestDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpeakingTestDetailDto>> UpdateTest(Guid testId,
        VersionedSpeakingTestRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateSpeakingTestCommand(CurrentUserId(), testId, request.Version,
            request.Test ?? new SpeakingTestInput(null, null, null, null)), cancellationToken));

    [HttpPost("tests/{testId:guid}/publish")]
    [ProducesResponseType<SpeakingTestDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SpeakingTestDetailDto>> PublishTest(Guid testId,
        VersionedSpeakingPublishRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new PublishSpeakingTestCommand(CurrentUserId(), testId, request.Version),
            cancellationToken));

    private static readonly SpeakingExerciseInput MissingExercise = new(0, null, null, null, null, null, null);

    internal static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
    }

    internal FileContentResult PrivateFile(SpeakingMediaContent media)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(media.Data, media.ContentType);
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
        ? userId
        : throw new UnauthorizedAccessException();
}

public sealed record VersionedSpeakingExerciseRequest(int Version, SpeakingExerciseInput? Exercise);
public sealed record VersionedSpeakingTestRequest(int Version, SpeakingTestInput? Test);
public sealed record VersionedSpeakingPublishRequest(int Version);
