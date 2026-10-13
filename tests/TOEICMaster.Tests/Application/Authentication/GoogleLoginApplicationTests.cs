using TOEICMaster.Application.Authentication.Commands.LoginWithGoogle;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Authentication;

public sealed class GoogleLoginApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Verified_google_account_issues_the_same_application_session_as_local_login()
    {
        var user = User.CreateExternal("Google Learner", "learner@gmail.com", "LEARNER@GMAIL.COM", Now);
        var verifier = new FakeGoogleIdentityVerifier();
        var externalRepository = new FakeExternalLoginRepository
        {
            Resolution = ExternalLoginResolution.Resolved(user)
        };
        var authenticationRepository = new FakeAuthenticationRepository();
        var tokenService = new FakeAuthenticationTokenService();
        var handler = new LoginWithGoogleCommandHandler(verifier, externalRepository, authenticationRepository,
            tokenService, new FixedTimeProvider(Now));

        var result = await handler.Handle(new LoginWithGoogleCommand("signed-google-id-token", true),
            CancellationToken.None);

        Assert.Equal("signed-google-id-token", verifier.Credential);
        Assert.Equal(verifier.Account, externalRepository.Account);
        Assert.True(tokenService.RememberMe);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("hash:login-refresh-token", authenticationRepository.AddedSession?.RefreshTokenHash);
    }

    [Fact]
    public async Task Existing_non_authoritative_email_requires_explicit_account_linking()
    {
        var authenticationRepository = new FakeAuthenticationRepository();
        var handler = new LoginWithGoogleCommandHandler(new FakeGoogleIdentityVerifier(),
            new FakeExternalLoginRepository { Resolution = ExternalLoginResolution.LinkRequired() },
            authenticationRepository, new FakeAuthenticationTokenService(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ExternalAccountLinkRequiredException>(() => handler.Handle(
            new LoginWithGoogleCommand("signed-google-id-token"), CancellationToken.None));

        Assert.Null(authenticationRepository.AddedSession);
    }

    [Fact]
    public async Task Invalid_google_credential_never_reaches_the_database()
    {
        var verifier = new FakeGoogleIdentityVerifier { Exception = new InvalidGoogleCredentialException() };
        var externalRepository = new FakeExternalLoginRepository();
        var handler = new LoginWithGoogleCommandHandler(verifier, externalRepository,
            new FakeAuthenticationRepository(), new FakeAuthenticationTokenService(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidGoogleCredentialException>(() => handler.Handle(
            new LoginWithGoogleCommand("invalid-token"), CancellationToken.None));

        Assert.Null(externalRepository.Account);
    }
}
