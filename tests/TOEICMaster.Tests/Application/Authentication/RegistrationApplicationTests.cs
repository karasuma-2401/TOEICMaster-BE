using TOEICMaster.Application.Authentication.Commands.Register;
using TOEICMaster.Application.Authentication.Commands.ResendEmailVerification;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Authentication;

public sealed class RegistrationApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Registration_normalizes_input_binds_credentials_to_challenge_and_enqueues_outbox_mail()
    {
        var repository = new FakeUserRegistrationRepository();
        var passwordHasher = new FakePasswordHasher();
        var handler = CreateHandler(repository, passwordHasher);

        await handler.Handle(new RegisterCommand("  Nguyen Van A  ", "  Learner@Example.com  ", " password "),
            CancellationToken.None);

        Assert.NotNull(repository.AddedUser);
        Assert.Equal("Nguyen Van A", repository.AddedUser.DisplayName);
        Assert.Equal("Learner@Example.com", repository.AddedUser.Email);
        Assert.Equal("LEARNER@EXAMPLE.COM", repository.AddedUser.NormalizedEmail);
        Assert.Null(repository.AddedUser.PasswordHash);
        Assert.Equal(" password ", passwordHasher.Password);
        Assert.False(repository.AddedUser.IsActive);
        Assert.Equal("hash:LEARNER@EXAMPLE.COM:123456", repository.AddedChallenge?.CodeHash);
        Assert.Equal("Nguyen Van A", repository.AddedChallenge?.PendingDisplayName);
        Assert.Equal("hashed-for-test", repository.AddedChallenge?.PendingPasswordHash);
        Assert.Contains("123456", Assert.Single(repository.QueuedMessages).TextBody);
    }

    [Fact]
    public async Task Existing_email_is_rejected_before_hashing()
    {
        var repository = new FakeUserRegistrationRepository { ExistingEmail = true };
        var passwordHasher = new FakePasswordHasher();
        var handler = CreateHandler(repository, passwordHasher);

        await Assert.ThrowsAsync<DuplicateEmailException>(() => handler.Handle(
            new RegisterCommand("Learner", "learner@example.com", "password"),
            CancellationToken.None));

        Assert.Null(passwordHasher.Password);
        Assert.Null(repository.AddedUser);
    }

    [Fact]
    public async Task Concurrent_duplicate_is_reported_consistently()
    {
        var repository = new FakeUserRegistrationRepository { AddSucceeds = false };
        var handler = CreateHandler(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DuplicateEmailException>(() => handler.Handle(
            new RegisterCommand("Learner", "learner@example.com", "password"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Pending_email_re_registration_rotates_pending_credentials_and_enqueues_new_verification_mail()
    {
        var repository = new FakeUserRegistrationRepository { ExistingPendingEmail = true };
        var passwordHasher = new FakePasswordHasher();
        var handler = CreateHandler(repository, passwordHasher);

        await handler.Handle(new RegisterCommand("Victim Name", "learner@example.com", "victim-password"),
            CancellationToken.None);

        Assert.Equal("victim-password", passwordHasher.Password);
        Assert.Null(repository.AddedUser);
        Assert.Equal("hash:LEARNER@EXAMPLE.COM:123456", repository.ReplacementCodeHash);
        Assert.Equal("Victim Name", repository.ReplacementDisplayName);
        Assert.Equal("hashed-for-test", repository.ReplacementPasswordHash);
        Assert.Single(repository.QueuedMessages);
    }

    [Fact]
    public async Task Resend_during_cooldown_returns_success_without_enqueuing_mail_again()
    {
        var repository = new FakeUserRegistrationRepository
        {
            ExistingPendingEmail = true,
            ReplaceSucceeds = false
        };
        var handler = new ResendEmailVerificationCommandHandler(repository, new FakeEmailVerificationCodeService(),
            new FixedTimeProvider(Now));

        await handler.Handle(new ResendEmailVerificationCommand("learner@example.com"), CancellationToken.None);

        Assert.Empty(repository.QueuedMessages);
    }

    private static RegisterCommandHandler CreateHandler(FakeUserRegistrationRepository repository,
        FakePasswordHasher passwordHasher) =>
        new(repository, passwordHasher, new FakeEmailVerificationCodeService(), new FixedTimeProvider(Now));
}
