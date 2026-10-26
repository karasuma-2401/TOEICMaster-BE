using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Importing;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class ExamExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ExamNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy đề thi, revision hoặc barem yêu cầu."
            },
            ExamConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Mã đề thi đã tồn tại."
            },
            ExamPublicationException publication => new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["exam"] = publication.Errors.ToArray() })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Đề thi chưa đạt điều kiện xuất bản."
            },
            ExamWorkbookFormatException workbook => new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["workbook"] = workbook.Errors.ToArray() })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Workbook import không hợp lệ."
            },
            InvalidExamMediaException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Tệp media không hợp lệ hoặc vượt quá giới hạn cho phép."
            },
            _ => null
        };
        if (problem is null)
            return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
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
