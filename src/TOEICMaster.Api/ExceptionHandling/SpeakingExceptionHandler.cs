using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Speaking;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class SpeakingExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, type) = exception switch
        {
            SpeakingNotFoundException => (StatusCodes.Status404NotFound,
                "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            SpeakingConflictException or SpeakingConcurrencyException => (StatusCodes.Status409Conflict,
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            SpeakingPublishRequirementsException => (StatusCodes.Status422UnprocessableEntity,
                "https://tools.ietf.org/html/rfc9110#section-15.5.21"),
            SpeakingMediaTooLargeException => (StatusCodes.Status413PayloadTooLarge,
                "https://tools.ietf.org/html/rfc9110#section-15.5.14"),
            InvalidSpeakingMediaException => (StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            _ => (0, string.Empty)
        };
        if (status == 0)
            return false;

        var problem = exception is SpeakingPublishRequirementsException requirements
            ? new ValidationProblemDetails(requirements.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = exception.Message;
        problem.Type = type;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        }))
            await httpContext.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
