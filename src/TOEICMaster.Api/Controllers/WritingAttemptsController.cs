using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Application.Writing.Attempts;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/writing")]
public sealed class WritingAttemptsController(ISender sender) : ControllerBase
{
    private const string AnonymousSessionHeader = "X-Writing-Anonymous-Session";

    [HttpGet("tests")]
    [ProducesResponseType<WritingPublicTestPageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingPublicTestPageDto>> GetTests([FromQuery] string? query,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPublishedWritingTestsQuery(query, pageNumber, pageSize), cancellationToken));

    [HttpGet("tests/{testId:guid}")]
    [ProducesResponseType<WritingPublicTestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WritingPublicTestDto>> GetTest(Guid testId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPublishedWritingTestQuery(testId), cancellationToken));

    [HttpGet("media/{assetId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid assetId, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetPublishedWritingImageQuery(assetId), cancellationToken);
        Response.Headers.CacheControl = "public, max-age=3600";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Data, image.ContentType);
    }

    [HttpPost("attempts")]
    [ProducesResponseType<WritingAttemptDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WritingAttemptDto>> StartAttempt(StartWritingAttemptInput input,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new StartWritingAttemptCommand(CurrentOwner(), input), cancellationToken);
        return CreatedAtAction(nameof(GetAttempt), new { attemptId = created.Id }, created);
    }

    [HttpGet("attempts")]
    [ProducesResponseType<WritingAttemptPageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingAttemptPageDto>> GetHistory(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetWritingAttemptHistoryQuery(CurrentOwner()), cancellationToken));

    [HttpGet("attempts/{attemptId:guid}")]
    [ProducesResponseType<WritingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WritingAttemptDto>> GetAttempt(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetWritingAttemptQuery(CurrentOwner(), attemptId), cancellationToken));

    [HttpPut("attempts/{attemptId:guid}/answers/{position:int}")]
    [ProducesResponseType<WritingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WritingAttemptDto>> SaveAnswer(Guid attemptId, int position,
        SaveWritingAnswerInput input, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SaveWritingAnswerCommand(CurrentOwner(), attemptId, position, input),
            cancellationToken));

    [HttpPost("attempts/{attemptId:guid}/next")]
    [ProducesResponseType<WritingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WritingAttemptDto>> Next(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new NextWritingAttemptQuestionCommand(CurrentOwner(), attemptId), cancellationToken));

    [HttpPost("attempts/{attemptId:guid}/submit")]
    [ProducesResponseType<WritingAttemptDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WritingAttemptDto>> Submit(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SubmitWritingAttemptCommand(CurrentOwner(), attemptId), cancellationToken));

    [HttpPost("attempts/{attemptId:guid}/evaluation-consent")]
    [ProducesResponseType<WritingAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WritingAttemptDto>> GrantEvaluationConsent(Guid attemptId,
        GrantWritingEvaluationConsentInput input, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GrantWritingEvaluationConsentCommand(CurrentOwner(), attemptId, input),
            cancellationToken));

    private WritingAttemptOwner CurrentOwner()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(subject, out var userId))
                throw new UnauthorizedAccessException();
            return WritingAttemptOwner.ForUser(userId);
        }

        var rawAnonymousSession = Request.Headers[AnonymousSessionHeader].ToString();
        if (!Guid.TryParse(rawAnonymousSession, out _))
            throw new UnauthorizedAccessException();
        return WritingAttemptOwner.ForAnonymousSession(rawAnonymousSession);
    }
}
