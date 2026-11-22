using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.ExerciseBank;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/lr-practice")]
public sealed class LrPracticeController(ISender sender) : ControllerBase
{
    [HttpGet("parts/{part:int}/items")]
    [AllowAnonymous]
    [ProducesResponseType<LrPracticeItemPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LrPracticeItemPageDto>> GetItems(
        int part,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetLrPracticeItemsQuery(part, pageNumber, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("items/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<LrPracticeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LrPracticeDetailDto>> GetItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetLrPracticeItemDetailQuery(id),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("items/{id:guid}/submit")]
    [Authorize]
    [ProducesResponseType<LrPracticeSubmitResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LrPracticeSubmitResultDto>> Submit(
        Guid id,
        [FromBody] LrPracticeSubmitRequest request,
        CancellationToken cancellationToken)
    {
        if (User.TryGetUserId() is not { } userId)
            return Unauthorized();

        var result = await sender.Send(
            new SubmitLrPracticeCommand(userId, id, request),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("parts/{part:int}/statistics")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<LrPracticeItemStatisticsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LrPracticeItemStatisticsDto>>> GetStatistics(
        int part,
        CancellationToken cancellationToken)
    {
        if (User.TryGetUserId() is not { } userId)
            return Unauthorized();

        var result = await sender.Send(
            new GetLrPracticeStatisticsQuery(userId, part),
            cancellationToken);

        return Ok(result);
    }
}
