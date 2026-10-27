using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Writing.Attempts;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class WritingAttemptExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            WritingAttemptNotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy lượt làm Writing."),
            WritingAttemptConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message),
            _ => (0, null)
        };
        if (title is null)
            return false;

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = status == StatusCodes.Status404NotFound
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.5"
                : "https://tools.ietf.org/html/rfc9110#section-15.5.10"
        };
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
