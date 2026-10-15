using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication.Commands.LoginWithGoogle;

public sealed record LoginWithGoogleCommand(string Credential, bool RememberMe = false)
    : IRequest<AuthenticationResult>;

public sealed class LoginWithGoogleCommandHandler(
    IGoogleIdentityVerifier googleIdentityVerifier,
    IExternalLoginRepository externalLoginRepository,
    IAuthenticationRepository authenticationRepository,
    IAuthenticationTokenService tokenService,
    TimeProvider timeProvider) : IRequestHandler<LoginWithGoogleCommand, AuthenticationResult>
{
    public async Task<AuthenticationResult> Handle(LoginWithGoogleCommand request,
        CancellationToken cancellationToken)
    {
        var googleAccount = await googleIdentityVerifier.VerifyAsync(request.Credential, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        var resolution = await externalLoginRepository.ResolveGoogleAccountAsync(googleAccount, nowUtc,
            cancellationToken);
        if (resolution.RequiresExplicitLink)
            throw new ExternalAccountLinkRequiredException();
        if (resolution.User is not { IsActive: true } user)
            throw new InvalidGoogleCredentialException();

        var tokens = tokenService.IssueForLogin(user, nowUtc, request.RememberMe);
        var session = UserSession.Create(user.Id, tokens.RefreshTokenHash, nowUtc,
            tokens.RefreshTokenExpiresAtUtc);
        await authenticationRepository.AddSessionAsync(session, cancellationToken);
        return AuthenticationResult.Create(user, tokens);
    }
}
