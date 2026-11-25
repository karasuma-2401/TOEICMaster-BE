using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.ExamAttempts;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class ExamAttemptsController(ISender sender) : ControllerBase
{
    private Guid GetUserId() => User.GetRequiredUserId();

    /// <summary>Published Listening and Reading exams available to learners.</summary>
    [HttpGet("exams/lr")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PublishedLrExamDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishedLrExamDto>>> GetPublishedExams(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublishedLrExamsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Instructions screen: exam facts plus an in-progress attempt to resume, if any.</summary>
    [HttpGet("exams/{examId:guid}/lr-overview")]
    [ProducesResponseType<LrExamOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LrExamOverviewDto>> GetOverview(Guid examId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetLrExamOverviewQuery(GetUserId(), examId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Starts an attempt, or resumes the in-progress one. The time limit is fixed by the server.</summary>
    [HttpPost("exams/{examId:guid}/attempts")]
    [ProducesResponseType<ExamAttemptSheetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamAttemptSheetDto>> StartAttempt(Guid examId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartExamAttemptCommand(GetUserId(), examId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("exams/{examId:guid}/current-attempt")]
    [ProducesResponseType<ExamAttemptSheetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamAttemptSheetDto>> GetCurrentAttempt(Guid examId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentActiveAttemptQuery(GetUserId(), examId), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("exam-attempts/{attemptId:guid}")]
    [ProducesResponseType<ExamAttemptSheetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamAttemptSheetDto>> GetAttemptProgress(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExamAttemptProgressQuery(GetUserId(), attemptId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Moves from Listening to Reading. Listening answers are locked from this point on.</summary>
    [HttpPost("exam-attempts/{attemptId:guid}/start-reading")]
    [ProducesResponseType<ExamAttemptSheetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamAttemptSheetDto>> StartReading(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartReadingCommand(GetUserId(), attemptId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Autosave. A null or blank <c>selectedKey</c> clears the selection for that question.</summary>
    [HttpPut("exam-attempts/{attemptId:guid}/answers")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveAnswers(Guid attemptId, [FromBody] List<SaveAttemptAnswerInput> answers, CancellationToken cancellationToken)
    {
        await sender.Send(new SaveExamAttemptAnswersCommand(GetUserId(), attemptId, answers), cancellationToken);
        return NoContent();
    }

    [HttpPut("exam-attempts/{attemptId:guid}/questions/{questionId:guid}/flag")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ToggleQuestionFlag(
        Guid attemptId,
        Guid questionId,
        [FromBody] ToggleQuestionFlagInput input,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ToggleQuestionFlagCommand(GetUserId(), attemptId, questionId, input.IsFlagged), cancellationToken);
        return NoContent();
    }

    [HttpPost("exam-attempts/{attemptId:guid}/abandon")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AbandonAttempt(Guid attemptId, CancellationToken cancellationToken)
    {
        await sender.Send(new AbandonExamAttemptCommand(GetUserId(), attemptId), cancellationToken);
        return NoContent();
    }

    [HttpPost("exam-attempts/{attemptId:guid}/submit")]
    [ProducesResponseType<ExamAttemptResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamAttemptResultDto>> SubmitAttempt(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitExamAttemptCommand(GetUserId(), attemptId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("exam-attempts/{attemptId:guid}/result")]
    [ProducesResponseType<ExamAttemptResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamAttemptResultDto>> GetResult(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExamAttemptResultQuery(GetUserId(), attemptId), cancellationToken);
        return Ok(result);
    }

    /// <param name="filter"><c>All</c> (default), <c>Incorrect</c> (wrong or skipped) or <c>Flagged</c>.</param>
    [HttpGet("exam-attempts/{attemptId:guid}/review")]
    [ProducesResponseType<ExamAttemptReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamAttemptReviewDto>> ReviewAttempt(
        Guid attemptId,
        [FromQuery] ExamReviewFilter filter = ExamReviewFilter.All,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetExamAttemptReviewQuery(GetUserId(), attemptId, filter), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns ordered presigned audio URLs for all Part 1-4 Listening blocks of an active attempt.</summary>
    [HttpGet("exam-attempts/{attemptId:guid}/listening-audio-urls")]
    [ProducesResponseType<ExamAttemptListeningPlaylistDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamAttemptListeningPlaylistDto>> GetListeningAudioUrls(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExamAttemptListeningPlaylistQuery(GetUserId(), attemptId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a short-lived URL for a Listening audio block attached to this attempt.</summary>
    [HttpGet("exam-attempts/{attemptId:guid}/audio/{assetId:guid}")]
    [ProducesResponseType<ExamAttemptMediaUrlDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamAttemptMediaUrlDto>> GetAudioAsset(
        Guid attemptId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetExamAttemptMediaUrlQuery(GetUserId(), attemptId, TOEICMaster.Domain.Entities.ExamMediaKind.Audio, assetId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a short-lived URL for an image attached to this attempt.</summary>
    [HttpGet("exam-attempts/{attemptId:guid}/images/{assetId:guid}")]
    [ProducesResponseType<ExamAttemptMediaUrlDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamAttemptMediaUrlDto>> GetImageAsset(
        Guid attemptId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetExamAttemptMediaUrlQuery(GetUserId(), attemptId, TOEICMaster.Domain.Entities.ExamMediaKind.Image, assetId),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("exam-attempts/history")]
    [ProducesResponseType<ExamAttemptHistoryPagedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ExamAttemptHistoryPagedDto>> GetAttemptHistory(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? examId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetUserExamAttemptHistoryQuery(GetUserId(), pageIndex, pageSize, examId), cancellationToken);
        return Ok(result);
    }
}
