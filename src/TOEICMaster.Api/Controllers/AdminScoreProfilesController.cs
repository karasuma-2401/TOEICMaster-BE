using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.CreateScoreProfile;
using TOEICMaster.Application.Exams.Queries.GetScoreProfiles;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/score-profiles")]
[Authorize(Policy = "AccountAdministration")]
public sealed class AdminScoreProfilesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ScoreProfileResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScoreProfileResponse>>> Get(CancellationToken cancellationToken)
    {
        var profiles = await sender.Send(new GetScoreProfilesQuery(), cancellationToken);
        return Ok(profiles.Select(ScoreProfileResponse.From).ToArray());
    }

    [HttpPost]
    [ProducesResponseType<ScoreProfileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScoreProfileResponse>> Create(CreateScoreProfileRequest request,
        CancellationToken cancellationToken)
    {
        var conversions = request.Conversions.Select(conversion => new ScoreConversionInput(conversion.Skill,
            conversion.RawCorrectCount, conversion.ScaledScore)).ToArray();
        var profile = await sender.Send(new CreateScoreProfileCommand(request.Name, conversions), cancellationToken);
        var response = ScoreProfileResponse.From(profile);
        return Created($"/api/v1/admin/score-profiles/{profile.Id}", response);
    }
}

public sealed record CreateScoreProfileRequest(string Name, IReadOnlyList<ScoreConversionRequest> Conversions);
public sealed record ScoreConversionRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ExamSkill>))] ExamSkill Skill, int RawCorrectCount,
    int ScaledScore);
public sealed record ScoreProfileResponse(Guid Id, string Name, bool IsComplete, int MappingCount)
{
    public static ScoreProfileResponse From(ScoreProfileDto profile) => new(profile.Id, profile.Name, profile.IsComplete,
        profile.MappingCount);
}
