using TOEICMaster.Application.Authentication.Commands.RequestPasswordReset;
using TOEICMaster.Application.Authentication.Commands.ResetPassword;
using TOEICMaster.Application.Authentication.Commands.VerifyPasswordReset;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Authentication;

public sealed class PasswordResetApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Request_normalizes_email_and_sends_code_for_eligible_local_account()
    {
        var repository = new FakePasswordResetRepository();
        var mail = new RecordingMailSender();
        var handler = new RequestPasswordResetCommandHandler(repository,
            new FakeEmailVerificationCodeService(), mail, new FixedTimeProvider(Now));

        await handler.Handle(new RequestPasswordResetCommand(" Learner@Example.com "), CancellationToken.None);

        Assert.Equal("hash:LEARNER@EXAMPLE.COM:123456", repository.IssuedCodeHash);
        var message = Assert.Single(mail.Messages);
        Assert.Equal("learner@example.com", message.To);
        Assert.Contains("123456", message.TextBody);
        Assert.Contains("đặt lại mật khẩu", message.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Request_does_not_reveal_missing_or_ineligible_accounts()
    {
        var repository = new FakePasswordResetRepository { HasEligibleAccount = false };
        var mail = new RecordingMailSender();
        var handler = new RequestPasswordResetCommandHandler(repository,
            new FakeEmailVerificationCodeService(), mail, new FixedTimeProvider(Now));

        await handler.Handle(new RequestPasswordResetCommand("missing@example.com"), CancellationToken.None);

        Assert.Empty(mail.Messages);
        Assert.Null(repository.IssuedCodeHash);
    }

    [Fact]
    public async Task Request_during_cooldown_returns_success_without_sending_again()
    {
        var repository = new FakePasswordResetRepository { ReplaceSucceeds = false };
        var mail = new RecordingMailSender();
        var handler = new RequestPasswordResetCommandHandler(repository,
            new FakeEmailVerificationCodeService(), mail, new FixedTimeProvider(Now));

        await handler.Handle(new RequestPasswordResetCommand("learner@example.com"), CancellationToken.None);

        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task Verification_exchanges_the_code_for_a_short_lived_token()
    {
        var repository = new FakePasswordResetRepository();
        var handler = new VerifyPasswordResetCommandHandler(repository,
            new FakeEmailVerificationCodeService(), new FakePasswordResetTokenService(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new VerifyPasswordResetCommand(" Learner@Example.com ", "123456"), CancellationToken.None);

        Assert.Equal("reset-token", result.ResetToken);
        Assert.Equal(Now.AddMinutes(10), result.ExpiresAtUtc);
        Assert.Equal("LEARNER@EXAMPLE.COM", repository.VerifiedEmail);
        Assert.Equal("hash:LEARNER@EXAMPLE.COM:123456", repository.VerifiedCodeHash);
        Assert.Equal("hash:reset-token", repository.SavedResetTokenHash);
    }

    [Fact]
    public async Task Invalid_verification_code_returns_only_a_generic_error()
    {
        var repository = new FakePasswordResetRepository
        {
            VerificationResult = PasswordResetCodeAttempt.Invalid
        };
        var handler = new VerifyPasswordResetCommandHandler(repository,
            new FakeEmailVerificationCodeService(), new FakePasswordResetTokenService(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidPasswordResetCodeException>(() => handler.Handle(
            new VerifyPasswordResetCommand("learner@example.com", "000000"), CancellationToken.None));
    }

    [Fact]
    public async Task Reset_hashes_the_new_password_and_consumes_the_token()
    {
        var repository = new FakePasswordResetRepository();
        var passwordHasher = new FakePasswordHasher();
        var handler = new ResetPasswordCommandHandler(repository, new FakePasswordResetTokenService(),
            passwordHasher, new FixedTimeProvider(Now));

        await handler.Handle(new ResetPasswordCommand("reset-token", " new-password "),
            CancellationToken.None);

        Assert.Equal(" new-password ", passwordHasher.Password);
        Assert.Equal("hash:reset-token", repository.ConsumedResetTokenHash);
        Assert.Equal("hashed-for-test", repository.NewPasswordHash);
        Assert.Equal(Now, repository.ResetAtUtc);
    }

    [Fact]
    public async Task Invalid_or_consumed_reset_token_is_rejected()
    {
        var repository = new FakePasswordResetRepository { ResetSucceeds = false };
        var handler = new ResetPasswordCommandHandler(repository, new FakePasswordResetTokenService(),
            new FakePasswordHasher(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidPasswordResetTokenException>(() => handler.Handle(
            new ResetPasswordCommand("used-token", "new-password"), CancellationToken.None));
    }
}
