using TOEICMaster.Application.Authentication.Commands.Register;
using TOEICMaster.Application.Common.Exceptions;

namespace TOEICMaster.Tests;

public sealed class RegistrationApplicationTests
{
    [Fact]
    public async Task Registration_normalizes_input_hashes_password_and_persists_user()
    {
        var repository = new FakeUserRegistrationRepository();
        var passwordHasher = new FakePasswordHasher();
        var handler = new RegisterCommandHandler(repository, passwordHasher);

        await handler.Handle(new RegisterCommand("  Nguyen Van A  ", "  Learner@Example.com  ", " password "),
            CancellationToken.None);

        Assert.NotNull(repository.AddedUser);
        Assert.Equal("Nguyen Van A", repository.AddedUser.DisplayName);
        Assert.Equal("Learner@Example.com", repository.AddedUser.Email);
        Assert.Equal("LEARNER@EXAMPLE.COM", repository.AddedUser.NormalizedEmail);
        Assert.Equal("hashed-for-test", repository.AddedUser.PasswordHash);
        Assert.Equal(" password ", passwordHasher.Password);
    }

    [Fact]
    public async Task Existing_email_is_rejected_before_hashing()
    {
        var repository = new FakeUserRegistrationRepository { ExistingEmail = true };
        var passwordHasher = new FakePasswordHasher();
        var handler = new RegisterCommandHandler(repository, passwordHasher);

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
        var handler = new RegisterCommandHandler(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DuplicateEmailException>(() => handler.Handle(
            new RegisterCommand("Learner", "learner@example.com", "password"),
            CancellationToken.None));
    }
}
