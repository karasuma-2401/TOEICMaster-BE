using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TOEICMaster.Api.Configuration;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.Pronunciation;

namespace TOEICMaster.Api.Controllers;

/// <summary>IPA pronunciation practice for learners: scores one recording of an example word.</summary>
[ApiController]
[Authorize]
[Route("api/v1/pronunciation")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PronunciationController(ISender sender) : ControllerBase
{
    [HttpPost("assessments")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(PronunciationRateLimitPolicies.Assessment)]
    [RequestSizeLimit(PronunciationRules.MaxAudioBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PronunciationRules.MaxAudioBytes + 64 * 1024)]
    [ProducesResponseType<PronunciationAssessmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PronunciationAssessmentDto>> Assess(IFormFile? file,
        [FromForm] string? referenceText, [FromForm] string? locale, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Failure(StatusCodes.Status400BadRequest, "INVALID_AUDIO", "Recording is required.");
        if (file.Length > PronunciationRules.MaxAudioBytes)
            return Failure(StatusCodes.Status413PayloadTooLarge, "INVALID_AUDIO", "Recording exceeds 1 MiB.");

        byte[] audio;
        await using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream, cancellationToken);
            audio = stream.ToArray();
        }

        try
        {
            return Ok(await sender.Send(new AssessPronunciationCommand(audio, referenceText ?? string.Empty,
                string.IsNullOrWhiteSpace(locale) ? null : locale.Trim()), cancellationToken));
        }
        catch (PronunciationAssessmentException exception)
        {
            return Failure(exception.Kind switch
            {
                PronunciationFailureKind.InvalidAudio => StatusCodes.Status400BadRequest,
                PronunciationFailureKind.Rejected => StatusCodes.Status422UnprocessableEntity,
                PronunciationFailureKind.NotConfigured => StatusCodes.Status503ServiceUnavailable,
                PronunciationFailureKind.Timeout => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status502BadGateway
            }, exception.Code, exception.Message);
        }
    }

    private ObjectResult Failure(int status, string code, string title) =>
        this.ProblemResponse(status, title, code: code);
}
