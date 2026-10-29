using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Application.ExerciseBank;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/lr-exercise-bank")]
[Authorize(Policy = "AccountAdministration")]
public sealed class AdminLrExerciseBankController(ISender sender, IWebHostEnvironment environment) : ControllerBase
{
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

    [HttpPost("parts/{part:int}/imports")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType<LrExerciseImportResultDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LrExerciseImportResultDto>> ImportItems(int part, [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > 10 * 1024 * 1024)
            return BadRequest(new ProblemDetails { Status = 400, Title = "File import không hợp lệ." });
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

    private Guid CurrentUserId()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAccessException();
        return userId;
    }
}
