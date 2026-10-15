using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TOEICMaster.Api.Configuration;
using TOEICMaster.Api.Contracts;
using TOEICMaster.Application.Authentication.Commands.Login;
using TOEICMaster.Application.Authentication.Commands.LoginWithGoogle;
using TOEICMaster.Application.Authentication.Commands.Logout;
using TOEICMaster.Application.Authentication.Commands.RefreshSession;
using TOEICMaster.Application.Authentication.Commands.Register;
using TOEICMaster.Application.Authentication.Commands.RequestPasswordReset;
using TOEICMaster.Application.Authentication.Commands.ResendEmailVerification;
using TOEICMaster.Application.Authentication.Commands.ResetPassword;
using TOEICMaster.Application.Authentication.Commands.VerifyEmail;
using TOEICMaster.Application.Authentication.Commands.VerifyPasswordReset;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.EmailDelivery)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return Accepted();
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting(AuthRateLimitPolicies.EmailDelivery)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerification(ResendEmailVerificationCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return Accepted();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(AuthRateLimitPolicies.EmailDelivery)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return Accepted();
    }

    [HttpPost("verify-password-reset")]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<PasswordResetVerificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PasswordResetVerificationResponse>> VerifyPasswordReset(
        VerifyPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        PreventTokenCaching();
        return Ok(PasswordResetVerificationResponse.From(result));
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        PreventTokenCaching();
        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        PreventTokenCaching();
        return Ok(AuthenticationResponse.From(result));
    }

    [HttpPost("google")]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticationResponse>> Google(LoginWithGoogleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        PreventTokenCaching();
        return Ok(AuthenticationResponse.From(result));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(AuthRateLimitPolicies.Sessions)]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        PreventTokenCaching();
        return Ok(AuthenticationResponse.From(result));
    }

    [HttpPost("logout")]
    [EnableRateLimiting(AuthRateLimitPolicies.Sessions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    private void PreventTokenCaching()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
