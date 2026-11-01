using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TOEICMaster.Application.ExamAttempts;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class ExamAttemptsController(ISender sender) : ControllerBase
{
    private Guid GetUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User identifier not found in claims.");
        return Guid.Parse(idClaim);
    }

    [HttpPost("exams/{examId:guid}/attempts")]
    public async Task<ActionResult<ExamAttemptSheetDto>> StartAttempt(
        Guid examId,
        [FromBody] StartExamAttemptInput? input,
        CancellationToken cancellationToken)
    {
        var timeLimit = input?.TimeLimitMinutes ?? 120;
        var result = await sender.Send(new StartExamAttemptCommand(GetUserId(), examId, timeLimit), cancellationToken);
        return Ok(result);
    }

    [HttpGet("exams/{examId:guid}/current-attempt")]
    public async Task<ActionResult<ExamAttemptSheetDto>> GetCurrentAttempt(Guid examId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentActiveAttemptQuery(GetUserId(), examId), cancellationToken);
        if (result is null) return NotFound("No active attempt found for this exam.");
        return Ok(result);
    }

    [HttpGet("exam-attempts/{attemptId:guid}")]
    public async Task<ActionResult<ExamAttemptSheetDto>> GetAttemptProgress(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExamAttemptProgressQuery(GetUserId(), attemptId), cancellationToken);
        return Ok(result);
    }

    [HttpPut("exam-attempts/{attemptId:guid}/answers")]
    public async Task<IActionResult> SaveAnswers(Guid attemptId, [FromBody] List<SaveAttemptAnswerInput> answers, CancellationToken cancellationToken)
    {
        await sender.Send(new SaveExamAttemptAnswersCommand(GetUserId(), attemptId, answers), cancellationToken);
        return NoContent();
    }

    [HttpPut("exam-attempts/{attemptId:guid}/questions/{questionId:guid}/flag")]
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
    public async Task<IActionResult> AbandonAttempt(Guid attemptId, CancellationToken cancellationToken)
    {
        await sender.Send(new AbandonExamAttemptCommand(GetUserId(), attemptId), cancellationToken);
        return NoContent();
    }

    [HttpPost("exam-attempts/{attemptId:guid}/submit")]
    public async Task<ActionResult<ExamAttemptResultDto>> SubmitAttempt(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitExamAttemptCommand(GetUserId(), attemptId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("exam-attempts/{attemptId:guid}/review")]
    public async Task<ActionResult<ExamAttemptReviewDto>> ReviewAttempt(
        Guid attemptId,
        [FromQuery] bool onlyIncorrect = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetExamAttemptReviewQuery(GetUserId(), attemptId, onlyIncorrect), cancellationToken);
        return Ok(result);
    }

    [HttpGet("exam-attempts/history")]
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
