using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Profile;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Profile;

public sealed class ProfileApplicationTests
{
    [Fact]
    public async Task Profile_returns_only_the_authenticated_users_public_details()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository { User = user };
        var handler = new GetProfileQueryHandler(repository);

        var profile = await handler.Handle(new GetProfileQuery(user.Id), CancellationToken.None);

        Assert.Equal(user.Id, repository.SearchedId);
        Assert.Equal(user.Email, profile.Email);
        Assert.Equal("Learner", profile.DisplayName);
        Assert.Null(profile.AvatarUrl);
    }

    [Fact]
    public async Task Change_password_requires_the_current_password_and_revokes_sessions()
    {
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", "hash:old", DateTimeOffset.UtcNow);
        user.VerifyEmail(DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository { User = user };
        var handler = new ChangeProfilePasswordCommandHandler(repository, new FakeHasher(), TimeProvider.System);

        await Assert.ThrowsAsync<InvalidProfilePasswordException>(() => handler.Handle(
            new ChangeProfilePasswordCommand(user.Id, "wrong", "new-password"), CancellationToken.None));
        Assert.Null(repository.NewPasswordHash);

        await handler.Handle(new ChangeProfilePasswordCommand(user.Id, "old", "new-password"), CancellationToken.None);
        Assert.Equal("hash:new-password", repository.NewPasswordHash);
        Assert.True(repository.SessionsRevoked);
    }

    [Fact]
    public async Task Google_only_account_cannot_change_a_nonexistent_local_password()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var handler = new ChangeProfilePasswordCommandHandler(
            new FakeProfileRepository { User = user }, new FakeHasher(), TimeProvider.System);

        await Assert.ThrowsAsync<InvalidProfilePasswordException>(() => handler.Handle(
            new ChangeProfilePasswordCommand(user.Id, "anything", "new-password"), CancellationToken.None));
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string? passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class FakeProfileRepository : IProfileRepository
    {
        public User? User { get; init; }
        public Guid SearchedId { get; private set; }
        public string? NewPasswordHash { get; private set; }
        public bool SessionsRevoked { get; private set; }

        public Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken)
        {
            SearchedId = userId;
            return Task.FromResult(User?.Id == userId ? User : null);
        }

        public Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken) =>
            Task.FromResult(User?.Id == userId);

        public Task<bool> UpdateAvatarAsync(Guid userId, string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(User?.Id == userId);

        public Task<bool> ChangePasswordAsync(Guid userId, string currentHash, string newHash,
            DateTimeOffset changedAtUtc, CancellationToken cancellationToken)
        {
            if (User?.Id != userId || User.PasswordHash != currentHash)
                return Task.FromResult(false);
            NewPasswordHash = newHash;
            SessionsRevoked = true;
            return Task.FromResult(true);
        }
    }
}
