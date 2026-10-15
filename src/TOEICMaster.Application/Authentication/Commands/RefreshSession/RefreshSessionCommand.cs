using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication.Commands.RefreshSession;

public sealed record RefreshSessionCommand(string RefreshToken) : IRequest<AuthenticationResult>;

public sealed class RefreshSessionCommandHandler(
    IAuthenticationRepository authenticationRepository,
    IAuthenticationTokenService tokenService,
    TimeProvider timeProvider) : IRequestHandler<RefreshSessionCommand, AuthenticationResult>
{
    public async Task<AuthenticationResult> Handle(RefreshSessionCommand request,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var authenticationSession = await authenticationRepository.FindActiveSessionAsync(refreshTokenHash, nowUtc,
            cancellationToken);
        if (authenticationSession is null || !authenticationSession.User.IsActive)
            throw new InvalidSessionException();

        var tokens = tokenService.Rotate(authenticationSession.User, nowUtc,
            authenticationSession.Session.ExpiresAtUtc);
        var replacement = UserSession.Create(authenticationSession.User.Id, tokens.RefreshTokenHash, nowUtc,
            tokens.RefreshTokenExpiresAtUtc);
        if (!await authenticationRepository.TryRotateSessionAsync(authenticationSession.Session.Id, replacement,
                nowUtc, cancellationToken))
            throw new InvalidSessionException();

        return AuthenticationResult.Create(authenticationSession.User, tokens);
    }
}
