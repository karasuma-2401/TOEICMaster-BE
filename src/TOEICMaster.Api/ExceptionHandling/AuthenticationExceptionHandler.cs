using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Profile;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class AuthenticationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, type) = exception switch
        {
            InvalidProfilePasswordException => (StatusCodes.Status400BadRequest,
                "Mật khẩu hiện tại không đúng hoặc tài khoản không có mật khẩu.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            InvalidAvatarException => (StatusCodes.Status400BadRequest,
                "Ảnh đại diện phải là PNG hoặc JPEG hợp lệ, tối đa 2 MiB.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,
                "Phiên đăng nhập không hợp lệ.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized,
                "Email hoặc mật khẩu không chính xác.", "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            InvalidSessionException => (StatusCodes.Status401Unauthorized,
                "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            InvalidGoogleCredentialException => (StatusCodes.Status401Unauthorized,
                "Google credential không hợp lệ hoặc đã hết hạn.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            ExternalAccountLinkRequiredException => (StatusCodes.Status409Conflict,
                "Email này đã thuộc một tài khoản khác. Hãy xác minh email, đăng nhập bằng mật khẩu rồi liên kết Google.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            InvalidEmailVerificationException => (StatusCodes.Status400BadRequest,
                "Mã xác minh không chính xác, đã hết hạn hoặc đã vượt quá số lần thử.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            InvalidPasswordResetCodeException => (StatusCodes.Status400BadRequest,
                "Mã đặt lại mật khẩu không chính xác, đã hết hạn hoặc đã vượt quá số lần thử.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            InvalidPasswordResetTokenException => (StatusCodes.Status400BadRequest,
                "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
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
