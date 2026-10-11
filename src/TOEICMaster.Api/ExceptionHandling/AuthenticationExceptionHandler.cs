using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Common.Exceptions;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class AuthenticationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, type) = exception switch
        {
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized,
                "Email hoặc mật khẩu không chính xác.", "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            InvalidSessionException => (StatusCodes.Status401Unauthorized,
                "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            InvalidGoogleCredentialException => (StatusCodes.Status401Unauthorized,
                "Google credential không hợp lệ hoặc đã hết hạn.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            ExternalAccountLinkRequiredException => (StatusCodes.Status409Conflict,
                "Email này đã thuộc một tài khoản khác. Hãy đăng nhập bằng mật khẩu để liên kết Google.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            _ => (0, null, null)
        };
        if (title is null)
            return false;

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type
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
