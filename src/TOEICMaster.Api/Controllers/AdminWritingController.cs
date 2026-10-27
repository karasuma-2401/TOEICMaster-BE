using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Attempts;
using TOEICMaster.Application.Writing.Evaluations;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/writing")]
[Authorize(Policy = "WritingAdministration")]
public sealed class AdminWritingController(ISender sender) : ControllerBase
{
    [HttpGet("exercises")]
    [ProducesResponseType<WritingExercisePageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingExercisePageDto>> GetExercises([FromQuery] int? part,
        [FromQuery] string? query, [FromQuery] string? difficulty, [FromQuery] string? status,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetWritingExercisesQuery(new WritingExerciseFilter(part, query, difficulty, status,
            pageNumber, pageSize)), cancellationToken));

    [HttpGet("exercises/{exerciseId:guid}")]
    [ProducesResponseType<WritingExerciseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WritingExerciseDto>> GetExercise(Guid exerciseId,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetWritingExerciseQuery(exerciseId), cancellationToken));

    [HttpPost("exercises")]
    [ProducesResponseType<WritingExerciseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WritingExerciseDto>> CreateExercise(WritingExerciseInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateWritingExerciseCommand(CurrentUserId(), input), cancellationToken);
        return CreatedAtAction(nameof(GetExercise), new { exerciseId = created.Id }, created);
    }

    [HttpPost("exercises/imports/validate")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType<WritingExerciseImportPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingExerciseImportPreviewDto>> PreviewImport([FromQuery] int part,
        [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > 10 * 1024 * 1024)
            return BadRequest(new ProblemDetails { Status = 400, Title = "File import không hợp lệ." });
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return Ok(await sender.Send(new PreviewWritingExerciseImportCommand(part, file.FileName, stream.ToArray()),
            cancellationToken));
    }

    [HttpPut("exercises/{exerciseId:guid}")]
    [ProducesResponseType<WritingExerciseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingExerciseDto>> UpdateExercise(Guid exerciseId,
        VersionedWritingExerciseRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateWritingExerciseCommand(CurrentUserId(), exerciseId, request.Version,
            request.Exercise), cancellationToken));

    [HttpDelete("exercises/{exerciseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ArchiveExercise(Guid exerciseId, [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ArchiveWritingExerciseCommand(CurrentUserId(), exerciseId, version), cancellationToken);
        return NoContent();
    }

    [HttpPost("media/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadWritingImageCommandHandler.MaxBytes + 64 * 1024)]
    [ProducesResponseType<WritingMediaAssetDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WritingMediaAssetDto>> UploadImage([FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > UploadWritingImageCommandHandler.MaxBytes)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Ảnh đề bài không hợp lệ." });
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var created = await sender.Send(new UploadWritingImageCommand(CurrentUserId(), stream.ToArray(),
            file.ContentType, file.FileName), cancellationToken);
        return CreatedAtAction(nameof(GetImage), new { assetId = created.Id }, created);
    }

    [HttpGet("media/{assetId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImage(Guid assetId, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetWritingImageQuery(assetId), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Data, image.ContentType);
    }

    [HttpGet("tests")]
    [ProducesResponseType<WritingTestPageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingTestPageDto>> GetTests([FromQuery] string? query,
        [FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetWritingTestsQuery(new WritingTestFilter(query, status, pageNumber, pageSize)),
            cancellationToken));

    [HttpGet("tests/{testId:guid}")]
    [ProducesResponseType<WritingTestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingTestDto>> GetTest(Guid testId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetWritingTestQuery(testId), cancellationToken));

    [HttpPost("tests")]
    [ProducesResponseType<WritingTestDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WritingTestDto>> CreateTest(WritingTestInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateWritingTestCommand(CurrentUserId(), input), cancellationToken);
        return CreatedAtAction(nameof(GetTest), new { testId = created.Id }, created);
    }

    [HttpPut("tests/{testId:guid}")]
    [ProducesResponseType<WritingTestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingTestDto>> UpdateTest(Guid testId, VersionedWritingTestRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateWritingTestDraftCommand(CurrentUserId(), testId, request.Version,
            request.Test), cancellationToken));

    [HttpPost("tests/{testId:guid}/publish")]
    [ProducesResponseType<WritingTestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingTestDto>> PublishTest(Guid testId, VersionedRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new PublishWritingTestCommand(CurrentUserId(), testId, request.Version),
            cancellationToken));

    [HttpGet("evaluations/{attemptId:guid}")]
    [ProducesResponseType<WritingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WritingAttemptDto>> GetEvaluation(Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAdminWritingEvaluationQuery(attemptId), cancellationToken));

    private Guid CurrentUserId()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAccessException();
        return userId;
    }
}

public sealed record VersionedRequest(int Version);
public sealed record VersionedWritingExerciseRequest(int Version, WritingExerciseInput Exercise);
public sealed record VersionedWritingTestRequest(int Version, WritingTestInput Test);
