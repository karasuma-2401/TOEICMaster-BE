using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Knowledge;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class KnowledgeExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            KnowledgeNotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message),
            KnowledgeConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message),
            KnowledgeMediaTooLargeException tooLarge => (StatusCodes.Status413PayloadTooLarge, tooLarge.Message),
            InvalidKnowledgeMediaException invalidMedia => (StatusCodes.Status400BadRequest, invalidMedia.Message),
            InvalidKnowledgeContentException invalidContent => (StatusCodes.Status400BadRequest, invalidContent.Message),
            _ => (0, null)
        };
        if (title is null)
            return false;

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = status switch
            {
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                StatusCodes.Status413PayloadTooLarge => "https://tools.ietf.org/html/rfc9110#section-15.5.14",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            }
        };
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        }))
        {
            await httpContext.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }

        return true;
    }
}
