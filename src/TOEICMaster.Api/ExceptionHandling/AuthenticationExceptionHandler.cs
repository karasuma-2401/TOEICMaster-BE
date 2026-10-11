using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Common.Exceptions;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class AuthenticationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var title = exception switch
        {
            InvalidCredentialsException => "Email hoặc mật khẩu không chính xác.",
            InvalidSessionException => "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
            _ => null
        };
        if (title is null)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = title,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
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
