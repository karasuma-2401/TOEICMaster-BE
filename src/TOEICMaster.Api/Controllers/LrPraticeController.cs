
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.ExerciseBank;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
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
        if (part is < 1 or > 7)
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Part phải nằm trong khoảng từ 1 đến 7."
            });

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

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost("items/{id:guid}/submit")]
    [Authorize]
    [ProducesResponseType<LrPracticeSubmitResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LrPracticeSubmitResultDto>> Submit(
    Guid id,
    [FromBody] LrPracticeSubmitRequest request,
    CancellationToken cancellationToken)
  {
    var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (!Guid.TryParse(subject, out var userId))
        return Unauthorized();

    var result = await sender.Send(
        new SubmitLrPracticeCommand(userId, id, request),
        cancellationToken);

    return Ok(result);
  }

[HttpGet("parts/{part:int}/statistics")]
[Authorize]
[ProducesResponseType<IReadOnlyList<LrPracticeItemStatisticsDto>>(
    StatusCodes.Status200OK)]
public async Task<ActionResult<IReadOnlyList<LrPracticeItemStatisticsDto>>>
    GetStatistics(
        int part,
        CancellationToken cancellationToken)
{
    if (part is < 1 or > 7)
        return BadRequest(new ProblemDetails
        {
            Status = 400,
            Title = "Part phải nằm trong khoảng từ 1 đến 7."
        });

    var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (!Guid.TryParse(subject, out var userId))
        return Unauthorized();

    var result = await sender.Send(
        new GetLrPracticeStatisticsQuery(userId, part),
        cancellationToken);

    return Ok(result);
}

}
