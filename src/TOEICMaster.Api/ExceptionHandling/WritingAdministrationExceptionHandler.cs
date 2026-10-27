using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Writing.Admin;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class WritingAdministrationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            WritingResourceNotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy tài nguyên Writing."),
            WritingConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message),
            InvalidWritingImageException => (StatusCodes.Status400BadRequest,
                "Ảnh đề bài phải là PNG hoặc JPEG hợp lệ, tối đa 5 MiB."),
            InvalidWritingImportException import => (StatusCodes.Status400BadRequest, import.Message),
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
                : status == StatusCodes.Status409Conflict
                    ? "https://tools.ietf.org/html/rfc9110#section-15.5.10"
                    : "https://tools.ietf.org/html/rfc9110#section-15.5.1"
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
