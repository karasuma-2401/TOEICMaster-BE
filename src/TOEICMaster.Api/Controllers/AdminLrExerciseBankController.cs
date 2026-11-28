using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.UploadExamImage;
using TOEICMaster.Application.ExerciseBank;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/lr-exercise-bank")]
[Authorize(Policy = "AccountAdministration")]
public sealed class AdminLrExerciseBankController(ISender sender, IWebHostEnvironment environment,
    IExamMediaStore mediaStore) : ControllerBase
{
    [HttpPost("media/{kind}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType<LrExerciseMediaUploadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LrExerciseMediaUploadDto>> UploadMedia(string kind, [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (kind is not ("audio" or "image") || file is null || file.Length is <= 0 or > 20 * 1024 * 1024)
            return this.BadRequestProblem("Invalid media file.");
        if (kind == "image" && file.Length > UploadExamImageCommandHandler.MaxBytes)
            return this.BadRequestProblem("Image must be a PNG, JPEG or WebP file up to 5 MB.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();
        string extension;
        string contentType;
        if (kind == "image")
        {
            var detected = ExamMediaInspector.DetectImage(bytes);
            if (detected is null)
                return this.BadRequestProblem("Image must be a PNG, JPEG or WebP file up to 5 MB.");
            (extension, contentType) = detected.Value;
        }
        else
        {
            var detected = ExamMediaInspector.DetectAudio(bytes);
            if (detected is null)
                return this.BadRequestProblem("Audio must be an MP3 or WAV file up to 20 MB.");
            (extension, contentType) = detected.Value;
        }
        var key = $"lr-exercise/{kind}/{Guid.NewGuid():N}.{extension}";
        await mediaStore.UploadAsync(key, bytes, contentType, cancellationToken);
        return Ok(new LrExerciseMediaUploadDto(key));
    }
    [HttpGet("summary")]
    [ProducesResponseType<LrExerciseBankSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LrExerciseBankSummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetLrExerciseBankSummaryQuery(), cancellationToken));

    [HttpGet("parts/{part:int}/items")]
    [ProducesResponseType<LrExerciseItemPageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LrExerciseItemPageDto>> GetItems(int part, [FromQuery] string? query,
        [FromQuery] string? difficulty, [FromQuery] string? status, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetLrExerciseItemPageQuery(new LrExerciseItemFilter(part, query, difficulty, status,
            pageNumber, pageSize)), cancellationToken));

    [HttpPost("parts/{part:int}/items")]
    [ProducesResponseType<LrExerciseItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LrExerciseItemDto>> CreateItem(int part, LrExerciseItemInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateLrExerciseItemCommand(CurrentUserId(), part, input), cancellationToken);
        return Created($"/api/v1/admin/lr-exercise-bank/parts/{part}/items/{created.Id}", created);
    }

    [HttpPatch("parts/{part:int}/items/{id:guid}")]
    [ProducesResponseType<LrExerciseItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LrExerciseItemDto>> UpdateMedia(int part, Guid id,
        LrExerciseItemMediaInput input, CancellationToken cancellationToken)
    {
        var updated = await sender.Send(new UpdateLrExerciseItemMediaCommand(CurrentUserId(), part, id, input),
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("parts/{part:int}/imports")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType<LrExerciseImportResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LrExerciseImportResultDto>> ImportItems(int part, [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > 10 * 1024 * 1024)
            return this.BadRequestProblem("Invalid import file.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var result = await sender.Send(new ImportLrExerciseItemsCommand(CurrentUserId(), part, file.FileName,
            stream.ToArray()), cancellationToken);
        return Created($"/api/v1/admin/lr-exercise-bank/parts/{part}/items", result);
    }

    [HttpGet("templates/{part:int}")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public IActionResult DownloadTemplate(int part)
    {
        if (part is < 1 or > 7)
            return NotFound();
        var filename = $"toeic-lr-part-{part}-import-template.xlsx";
        var path = Path.Combine(environment.ContentRootPath, "Resources", "ExerciseBankTemplates", filename);
        if (!System.IO.File.Exists(path))
            return NotFound();
        return PhysicalFile(path, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename,
            enableRangeProcessing: false);
    }

    private Guid CurrentUserId() => User.GetRequiredUserId();
}
