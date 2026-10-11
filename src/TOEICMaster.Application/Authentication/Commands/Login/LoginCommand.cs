using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication.Commands.Login;

public sealed record LoginCommand(string Email, string Password, bool RememberMe = false)
    : IRequest<AuthenticationResult>;

public sealed class LoginCommandHandler(
    IAuthenticationRepository authenticationRepository,
    IPasswordHasher passwordHasher,
    IAuthenticationTokenService tokenService,
    TimeProvider timeProvider) : IRequestHandler<LoginCommand, AuthenticationResult>
{
    public async Task<AuthenticationResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await authenticationRepository.FindUserByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        // Verify against a dummy hash when the user has no local credential so response timing stays comparable.
        var passwordMatches = passwordHasher.Verify(request.Password, user?.PasswordHash);
        if (user is null || !user.IsActive || user.PasswordHash is null || !passwordMatches)
            throw new InvalidCredentialsException();

        var nowUtc = timeProvider.GetUtcNow();
        var tokens = tokenService.IssueForLogin(user, nowUtc, request.RememberMe);
        var session = UserSession.Create(user.Id, tokens.RefreshTokenHash, nowUtc,
            tokens.RefreshTokenExpiresAtUtc);
        await authenticationRepository.AddSessionAsync(session, cancellationToken);

        return AuthenticationResult.Create(user, tokens);
    }
}
