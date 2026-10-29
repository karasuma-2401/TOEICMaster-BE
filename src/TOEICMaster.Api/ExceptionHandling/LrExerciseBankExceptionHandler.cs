using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.ExerciseBank;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class LrExerciseBankExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            LrExerciseBankConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message),
            LrExerciseBankImportException import => (StatusCodes.Status400BadRequest, import.Message),
            InvalidLrExerciseBankItemException invalid => (StatusCodes.Status400BadRequest, invalid.Message),
            _ => (0, null)
        };
        if (title is null)
            return false;
        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title };
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        }))
            await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        return true;
    }
}
