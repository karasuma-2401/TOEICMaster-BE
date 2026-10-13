using TOEICMaster.Application.Authentication.Commands.Login;
using TOEICMaster.Application.Authentication.Commands.Logout;
using TOEICMaster.Application.Authentication.Commands.RefreshSession;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests;

public sealed class LoginApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Login_normalizes_email_verifies_password_and_persists_hashed_session()
    {
        var passwordHasher = new FakePasswordHasher();
        var hash = passwordHasher.Hash("correct-password");
        var user = User.CreateLocal("Learner", "Learner@example.com", "LEARNER@EXAMPLE.COM", hash, Now);
        user.VerifyEmail(Now);
        var repository = new FakeAuthenticationRepository { User = user };
        var tokenService = new FakeAuthenticationTokenService();
        var handler = new LoginCommandHandler(repository, passwordHasher, tokenService, new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new LoginCommand("  learner@Example.com ", "correct-password", true), CancellationToken.None);

        Assert.Equal("LEARNER@EXAMPLE.COM", repository.SearchedEmail);
        Assert.Equal("correct-password", passwordHasher.VerificationPassword);
        Assert.True(tokenService.RememberMe);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("hash:login-refresh-token", repository.AddedSession?.RefreshTokenHash);
        Assert.Equal(Now.AddDays(30), repository.AddedSession?.ExpiresAtUtc);
    }

    [Fact]
    public async Task Unknown_user_uses_dummy_verification_and_returns_the_generic_credentials_error()
    {
        var passwordHasher = new FakePasswordHasher();
        var handler = new LoginCommandHandler(new FakeAuthenticationRepository(), passwordHasher,
            new FakeAuthenticationTokenService(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.Handle(
            new LoginCommand("missing@example.com", "attempted-password"), CancellationToken.None));

        Assert.Equal("attempted-password", passwordHasher.VerificationPassword);
        Assert.Null(passwordHasher.VerificationHash);
    }

    [Fact]
    public async Task Wrong_password_does_not_create_a_session()
    {
        var passwordHasher = new FakePasswordHasher();
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM",
            passwordHasher.Hash("correct-password"), Now);
        user.VerifyEmail(Now);
        var repository = new FakeAuthenticationRepository { User = user };
        var handler = new LoginCommandHandler(repository, passwordHasher, new FakeAuthenticationTokenService(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.Handle(
            new LoginCommand(user.Email, "wrong-password"), CancellationToken.None));

        Assert.Null(repository.AddedSession);
    }

    [Fact]
    public async Task Unverified_local_account_cannot_log_in()
    {
        var passwordHasher = new FakePasswordHasher();
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM",
            passwordHasher.Hash("correct-password"), Now);
        var repository = new FakeAuthenticationRepository { User = user };
        var handler = new LoginCommandHandler(repository, passwordHasher, new FakeAuthenticationTokenService(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.Handle(
            new LoginCommand(user.Email, "correct-password"), CancellationToken.None));

        Assert.Null(repository.AddedSession);
    }

    [Fact]
    public async Task Refresh_rotates_the_session_without_extending_its_absolute_expiry()
    {
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", "hash", Now);
        user.VerifyEmail(Now);
        var original = UserSession.Create(user.Id, "old-hash", Now, Now.AddDays(30));
        var repository = new FakeAuthenticationRepository
        {
            ActiveSession = new AuthenticationSession(original, user)
        };
        var handler = new RefreshSessionCommandHandler(repository, new FakeAuthenticationTokenService(),
            new FixedTimeProvider(Now.AddMinutes(10)));

        var result = await handler.Handle(new RefreshSessionCommand("old-token"), CancellationToken.None);

        Assert.Equal("rotated-refresh-token", result.RefreshToken);
        Assert.Equal(original.ExpiresAtUtc, result.RefreshTokenExpiresAtUtc);
        Assert.Equal("hash:rotated-refresh-token", repository.ReplacementSession?.RefreshTokenHash);
    }

    [Fact]
    public async Task Concurrent_refresh_of_the_same_session_is_rejected()
    {
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", "hash", Now);
        user.VerifyEmail(Now);
        var original = UserSession.Create(user.Id, "old-hash", Now, Now.AddDays(1));
        var repository = new FakeAuthenticationRepository
        {
            ActiveSession = new AuthenticationSession(original, user),
            RotateSucceeds = false
        };
        var handler = new RefreshSessionCommandHandler(repository, new FakeAuthenticationTokenService(),
            new FixedTimeProvider(Now.AddMinutes(1)));

        await Assert.ThrowsAsync<InvalidSessionException>(() => handler.Handle(
            new RefreshSessionCommand("old-token"), CancellationToken.None));
    }

    [Fact]
    public async Task Logout_revokes_the_hashed_refresh_token_and_is_idempotent()
    {
        var repository = new FakeAuthenticationRepository();
        var handler = new LogoutCommandHandler(repository, new FakeAuthenticationTokenService(),
            new FixedTimeProvider(Now));

        await handler.Handle(new LogoutCommand("refresh-token"), CancellationToken.None);

        Assert.Equal("hash:refresh-token", repository.RevokedTokenHash);
    }
}
