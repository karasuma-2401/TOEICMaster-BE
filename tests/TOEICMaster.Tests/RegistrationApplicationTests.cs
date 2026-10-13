using TOEICMaster.Application.Authentication.Commands.Register;
using TOEICMaster.Application.Common.Exceptions;

namespace TOEICMaster.Tests;

public sealed class RegistrationApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Registration_normalizes_input_hashes_password_and_persists_user()
    {
        var repository = new FakeUserRegistrationRepository();
        var passwordHasher = new FakePasswordHasher();
        var mail = new RecordingMailSender();
        var handler = CreateHandler(repository, passwordHasher, mail);

        await handler.Handle(new RegisterCommand("  Nguyen Van A  ", "  Learner@Example.com  ", " password "),
            CancellationToken.None);

        Assert.NotNull(repository.AddedUser);
        Assert.Equal("Nguyen Van A", repository.AddedUser.DisplayName);
        Assert.Equal("Learner@Example.com", repository.AddedUser.Email);
        Assert.Equal("LEARNER@EXAMPLE.COM", repository.AddedUser.NormalizedEmail);
        Assert.Equal("hashed-for-test", repository.AddedUser.PasswordHash);
        Assert.Equal(" password ", passwordHasher.Password);
        Assert.False(repository.AddedUser.IsActive);
        Assert.Equal("hash:LEARNER@EXAMPLE.COM:123456", repository.AddedChallenge?.CodeHash);
        Assert.Contains("123456", Assert.Single(mail.Messages).TextBody);
    }

    [Fact]
    public async Task Existing_email_is_rejected_before_hashing()
    {
        var repository = new FakeUserRegistrationRepository { ExistingEmail = true };
        var passwordHasher = new FakePasswordHasher();
        var handler = CreateHandler(repository, passwordHasher, new RecordingMailSender());

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
        var handler = CreateHandler(repository, new FakePasswordHasher(), new RecordingMailSender());

        await Assert.ThrowsAsync<DuplicateEmailException>(() => handler.Handle(
            new RegisterCommand("Learner", "learner@example.com", "password"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Pending_email_keeps_existing_credentials_and_resends_after_cooldown()
    {
        var repository = new FakeUserRegistrationRepository { ExistingPendingEmail = true };
        var passwordHasher = new FakePasswordHasher();
        var mail = new RecordingMailSender();
        var handler = CreateHandler(repository, passwordHasher, mail);

        await handler.Handle(new RegisterCommand("Replacement", "learner@example.com", "new-password"),
            CancellationToken.None);

        Assert.Null(passwordHasher.Password);
        Assert.Null(repository.AddedUser);
        Assert.Equal("hash:LEARNER@EXAMPLE.COM:123456", repository.ReplacementCodeHash);
        Assert.Single(mail.Messages);
    }

    [Fact]
    public async Task Pending_email_during_cooldown_returns_success_without_sending_again()
    {
        var repository = new FakeUserRegistrationRepository
        {
            ExistingPendingEmail = true,
            ReplaceSucceeds = false
        };
        var mail = new RecordingMailSender();
        var handler = CreateHandler(repository, new FakePasswordHasher(), mail);

        await handler.Handle(new RegisterCommand("Learner", "learner@example.com", "password"),
            CancellationToken.None);

        Assert.Empty(mail.Messages);
    }

    private static RegisterCommandHandler CreateHandler(FakeUserRegistrationRepository repository,
        FakePasswordHasher passwordHasher, RecordingMailSender mailSender) =>
        new(repository, passwordHasher, new FakeEmailVerificationCodeService(), mailSender,
            new FixedTimeProvider(Now));
}
