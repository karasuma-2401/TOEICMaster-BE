using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Api.Configuration;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Attempts;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/speaking")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SpeakingAttemptsController(ISender sender) : ControllerBase
{
    [HttpGet("tests")]
    [ProducesResponseType<SpeakingPageDto<SpeakingPublicTestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakingPageDto<SpeakingPublicTestDto>>> GetTests([FromQuery] string? query,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPublishedSpeakingTestsQuery(query, pageNumber, pageSize), cancellationToken));

    [HttpGet("tests/{testId:guid}")]
    [ProducesResponseType<SpeakingPublicTestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpeakingPublicTestDto>> GetTest(Guid testId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPublishedSpeakingTestQuery(testId), cancellationToken));

    [HttpGet("media/{assetId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid assetId, CancellationToken cancellationToken) =>
        PrivateFile(await sender.Send(new GetPublishedSpeakingImageQuery(assetId), cancellationToken));

    [HttpPost("attempts")]
    [ProducesResponseType<SpeakingAttemptDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpeakingAttemptDto>> StartAttempt(StartSpeakingAttemptInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new StartSpeakingAttemptCommand(CurrentOwner(), input), cancellationToken);
        return CreatedAtAction(nameof(GetAttempt), new { attemptId = created.Id }, created);
    }

    [HttpGet("attempts")]
    [ProducesResponseType<SpeakingPageDto<SpeakingAttemptSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakingPageDto<SpeakingAttemptSummaryDto>>> GetAttempts(
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSpeakingAttemptsQuery(CurrentOwner()), cancellationToken));

    [HttpGet("attempts/{attemptId:guid}")]
    [ProducesResponseType<SpeakingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpeakingAttemptDto>> GetAttempt(Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSpeakingAttemptQuery(CurrentOwner(), attemptId), cancellationToken));

    [HttpPost("attempts/{attemptId:guid}/next")]
    [ProducesResponseType<SpeakingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpeakingAttemptDto>> Next(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new NextSpeakingPhaseCommand(CurrentOwner(), attemptId), cancellationToken));

    [HttpPost("attempts/{attemptId:guid}/submit")]
    [ProducesResponseType<SpeakingAttemptDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakingAttemptDto>> Submit(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SubmitSpeakingAttemptCommand(CurrentOwner(), attemptId), cancellationToken));

    [HttpPut("attempts/{attemptId:guid}/recordings/{position:int}")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(SpeakingRateLimitPolicies.RecordingUpload)]
    [RequestSizeLimit(SpeakingMediaRules.MaxRecordingBytes + 64 * 1024)]
    [ProducesResponseType<SpeakingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<SpeakingAttemptDto>> UploadRecording(Guid attemptId, int position,
        IFormFile? file, [FromForm] int? durationSeconds, CancellationToken cancellationToken)
    {
        var owner = CurrentOwner();
        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bản ghi trống, không thể tải lên.");
        if (file.Length > SpeakingMediaRules.MaxRecordingBytes)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Bản ghi vượt quá dung lượng cho phép.");
        if (durationSeconds is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["DurationSeconds"] = ["Thiếu thời lượng bản ghi."]
            }));
        return Ok(await sender.Send(new UploadSpeakingRecordingCommand(owner, attemptId, position,
            await AdminSpeakingController.ReadAsync(file, cancellationToken), file.ContentType,
            durationSeconds.Value), cancellationToken));
    }

    [HttpGet("attempts/{attemptId:guid}/recordings/{position:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecording(Guid attemptId, int position, CancellationToken cancellationToken) =>
        PrivateFile(await sender.Send(new GetSpeakingRecordingQuery(CurrentOwner(), attemptId, position),
            cancellationToken));

    private FileContentResult PrivateFile(SpeakingMediaContent media)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(media.Data, media.ContentType);
    }

    /// <summary>The signed-in user, otherwise the anonymous browser session sent by the frontend BFF.</summary>
    private SpeakingAttemptOwner CurrentOwner()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
                ? SpeakingAttemptOwner.ForUser(userId)
                : throw new UnauthorizedAccessException();
        var anonymousSession = Request.Headers[SpeakingRateLimitPolicies.AnonymousSessionHeader].ToString();
        return Guid.TryParse(anonymousSession, out _)
            ? SpeakingAttemptOwner.ForAnonymousSession(anonymousSession)
            : throw new UnauthorizedAccessException();
    }
}
