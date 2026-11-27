using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Commands.CreateAdminUser;
using TOEICMaster.Application.Accounts.Commands.DeleteAdminUser;
using TOEICMaster.Application.Accounts.Commands.LockAdminUser;
using TOEICMaster.Application.Accounts.Commands.ResetAdminUserPassword;
using TOEICMaster.Application.Accounts.Commands.UnlockAdminUser;
using TOEICMaster.Application.Accounts.Commands.UpdateAdminUser;
using TOEICMaster.Application.Accounts.Queries.GetAdminUser;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;
using TOEICMaster.Application.Profile;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Accounts;

public sealed class AdminUserCommandsTests
{
    [Fact]
    public async Task Unlock_rejects_self_and_allows_other_users()
    {
        var actorId = Guid.NewGuid();
        var handler = new UnlockAdminUserCommandHandler(new InMemoryAdminUserRepository());
        var error = await Assert.ThrowsAsync<AdminUserOperationConflictException>(() =>
            handler.Handle(new UnlockAdminUserCommand(actorId, actorId), CancellationToken.None));
        Assert.Equal("CANNOT_UNLOCK_SELF", error.Code);
        await handler.Handle(new UnlockAdminUserCommand(actorId, Guid.NewGuid()), CancellationToken.None);
    }

    [Fact]
    public void Create_validator_accepts_valid_payload()
    {
        var result = new CreateAdminUserCommandValidator().Validate(
            new CreateAdminUserCommand("Nguyen Van A", "user@example.com", "USER", true));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "user@example.com", "USER", nameof(CreateAdminUserCommand.FullName))]
    [InlineData("   ", "user@example.com", "USER", nameof(CreateAdminUserCommand.FullName))]
    [InlineData("Nguyen Van A", "", "USER", nameof(CreateAdminUserCommand.Email))]
    [InlineData("Nguyen Van A", "not-an-email", "USER", nameof(CreateAdminUserCommand.Email))]
    [InlineData("Nguyen Van A", "user@example.com", "OWNER", nameof(CreateAdminUserCommand.Role))]
    public void Create_validator_isolates_each_invalid_field(string fullName, string email, string role,
        string expectedProperty)
    {
        var result = new CreateAdminUserCommandValidator().Validate(
            new CreateAdminUserCommand(fullName, email, role, true));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedProperty, error.PropertyName);
    }

    [Fact]
    public void Update_validator_rejects_invalid_role_and_empty_ids()
    {
        var validator = new UpdateAdminUserCommandValidator();
        Assert.True(validator.Validate(new UpdateAdminUserCommand(Guid.NewGuid(), Guid.NewGuid(), "ADMIN")).IsValid);

        var badRole = validator.Validate(new UpdateAdminUserCommand(Guid.NewGuid(), Guid.NewGuid(), "SUPERADMIN"));
        Assert.False(badRole.IsValid);
        Assert.Equal(nameof(UpdateAdminUserCommand.Role), Assert.Single(badRole.Errors).PropertyName);

        var emptyTarget = validator.Validate(new UpdateAdminUserCommand(Guid.NewGuid(), Guid.Empty, "USER"));
        Assert.False(emptyTarget.IsValid);
        Assert.Equal(nameof(UpdateAdminUserCommand.TargetUserId), Assert.Single(emptyTarget.Errors).PropertyName);
    }

    [Fact]
    public void Action_command_validators_reject_empty_actor_or_target_ids()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        Assert.True(new DeleteAdminUserCommandValidator().Validate(new DeleteAdminUserCommand(actorId, targetId)).IsValid);
        Assert.False(new DeleteAdminUserCommandValidator().Validate(new DeleteAdminUserCommand(actorId, Guid.Empty)).IsValid);

        Assert.True(new LockAdminUserCommandValidator().Validate(new LockAdminUserCommand(actorId, targetId)).IsValid);
        Assert.False(new LockAdminUserCommandValidator().Validate(new LockAdminUserCommand(Guid.Empty, targetId)).IsValid);

        Assert.True(new UnlockAdminUserCommandValidator().Validate(new UnlockAdminUserCommand(actorId, targetId)).IsValid);
        Assert.False(new UnlockAdminUserCommandValidator().Validate(new UnlockAdminUserCommand(actorId, Guid.Empty)).IsValid);

        Assert.True(new ResetAdminUserPasswordCommandValidator().Validate(
            new ResetAdminUserPasswordCommand(actorId, targetId)).IsValid);
        Assert.False(new ResetAdminUserPasswordCommandValidator().Validate(
            new ResetAdminUserPasswordCommand(actorId, Guid.Empty)).IsValid);
    }

    [Fact]
    public async Task Create_with_activation_email_creates_pending_user_with_password_hash_and_releases_cooldown_on_mail_failure()
    {
        var now = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var repo = new InMemoryAdminUserRepository();
        var regRepo = new FakeUserRegistrationRepository();
        var mailSender = new RecordingMailSender();
        var handler = new CreateAdminUserCommandHandler(
            repo, regRepo, new FakePasswordHasher(), new FakeEmailVerificationCodeService(), mailSender, clock);

        var dto = await handler.Handle(
            new CreateAdminUserCommand("  Tran Thi B  ", "  b@example.com ", "ADMIN", true),
            CancellationToken.None);

        Assert.Equal("Tran Thi B", dto.FullName);
        Assert.Equal("b@example.com", dto.Email);
        Assert.Equal("ADMIN", dto.Role);
        Assert.Equal("PENDING", dto.Status);
        Assert.False(dto.EmailVerified);
        Assert.Equal("PASSWORD", dto.AuthProvider);
        Assert.NotNull(repo.CreatedUser);
        Assert.False(string.IsNullOrEmpty(repo.CreatedUser!.PasswordHash));
        Assert.NotNull(repo.CreatedChallenge);
        Assert.Single(mailSender.Messages);

        // Mail delivery failure releases verification cooldown and preserves exception
        var failingRepo = new InMemoryAdminUserRepository();
        var failingRegRepo = new FakeUserRegistrationRepository();
        var failingMail = new FailingMailSender();
        var failingHandler = new CreateAdminUserCommandHandler(
            failingRepo, failingRegRepo, new FakePasswordHasher(), new FakeEmailVerificationCodeService(),
            failingMail, clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => failingHandler.Handle(
            new CreateAdminUserCommand("User C", "c@example.com", "USER", true),
            CancellationToken.None));
        Assert.True(failingRegRepo.CooldownReleased);
    }

    [Fact]
    public async Task Create_without_activation_email_creates_active_verified_user_without_sending_mail()
    {
        var now = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        var repo = new InMemoryAdminUserRepository();
        var mailSender = new RecordingMailSender();
        var handler = new CreateAdminUserCommandHandler(
            repo, new FakeUserRegistrationRepository(), new FakePasswordHasher(),
            new FakeEmailVerificationCodeService(), mailSender, new MutableTimeProvider(now));

        var dto = await handler.Handle(
            new CreateAdminUserCommand("Active User", "active@example.com", "USER", false),
            CancellationToken.None);

        Assert.Equal("ACTIVE", dto.Status);
        Assert.True(dto.EmailVerified);
        Assert.Equal("USER", dto.Role);
        Assert.Null(repo.CreatedChallenge);
        Assert.Empty(mailSender.Messages);
    }

    [Fact]
    public async Task Create_duplicate_email_throws_DuplicateEmailException()
    {
        var repo = new InMemoryAdminUserRepository { ForceDuplicateEmail = true };
        var handler = new CreateAdminUserCommandHandler(
            repo, new FakeUserRegistrationRepository(), new FakePasswordHasher(),
            new FakeEmailVerificationCodeService(), new RecordingMailSender(), TimeProvider.System);

        await Assert.ThrowsAsync<DuplicateEmailException>(() => handler.Handle(
            new CreateAdminUserCommand("Dup", "dup@example.com", "USER", false),
            CancellationToken.None));
    }

    [Fact]
    public async Task Delete_and_lock_enforce_self_guard_and_best_effort_avatar_cleanup()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var repo = new InMemoryAdminUserRepository { DeletedAvatarKey = "avatars/test.png" };
        var avatarStore = new ThrowingOnDeleteAvatarStore();

        var deleteHandler = new DeleteAdminUserCommandHandler(repo, avatarStore);
        var selfDelete = await Assert.ThrowsAsync<AdminUserOperationConflictException>(() =>
            deleteHandler.Handle(new DeleteAdminUserCommand(actorId, actorId), CancellationToken.None));
        Assert.Equal("CANNOT_DELETE_SELF", selfDelete.Code);

        // Even if avatar store throws during best-effort cleanup, delete succeeds
        await deleteHandler.Handle(new DeleteAdminUserCommand(actorId, targetId), CancellationToken.None);
        Assert.Equal(targetId, repo.DeletedTargetId);

        var lockHandler = new LockAdminUserCommandHandler(repo, TimeProvider.System);
        var selfLock = await Assert.ThrowsAsync<AdminUserOperationConflictException>(() =>
            lockHandler.Handle(new LockAdminUserCommand(actorId, actorId), CancellationToken.None));
        Assert.Equal("CANNOT_LOCK_SELF", selfLock.Code);
    }

    [Fact]
    public async Task Reset_password_enforces_eligibility_cooldown_and_releases_cooldown_on_mail_failure()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

        // Google-only user -> PASSWORD_RESET_UNSUPPORTED_PROVIDER
        var googleRepo = new InMemoryAdminUserRepository
        {
            ResetTarget = new AdminPasswordResetTarget(targetId, "g@example.com", "G@EXAMPLE.COM", true, true, false)
        };
        var googleHandler = new ResetAdminUserPasswordCommandHandler(
            googleRepo, new FakePasswordResetRepository(), new FakeEmailVerificationCodeService(),
            new RecordingMailSender(), new MutableTimeProvider(now));
        var googleEx = await Assert.ThrowsAsync<AdminUserOperationConflictException>(() =>
            googleHandler.Handle(new ResetAdminUserPasswordCommand(actorId, targetId), CancellationToken.None));
        Assert.Equal("PASSWORD_RESET_UNSUPPORTED_PROVIDER", googleEx.Code);

        // Locked/pending user -> USER_INACTIVE_OR_UNVERIFIED
        var lockedRepo = new InMemoryAdminUserRepository
        {
            ResetTarget = new AdminPasswordResetTarget(targetId, "l@example.com", "L@EXAMPLE.COM", false, true, true)
        };
        var lockedHandler = new ResetAdminUserPasswordCommandHandler(
            lockedRepo, new FakePasswordResetRepository(), new FakeEmailVerificationCodeService(),
            new RecordingMailSender(), new MutableTimeProvider(now));
        var lockedEx = await Assert.ThrowsAsync<AdminUserOperationConflictException>(() =>
            lockedHandler.Handle(new ResetAdminUserPasswordCommand(actorId, targetId), CancellationToken.None));
        Assert.Equal("USER_INACTIVE_OR_UNVERIFIED", lockedEx.Code);

        // Cooldown active -> PASSWORD_RESET_COOLDOWN (429)
        var eligibleRepo = new InMemoryAdminUserRepository
        {
            ResetTarget = new AdminPasswordResetTarget(targetId, "ok@example.com", "OK@EXAMPLE.COM", true, true, true)
        };
        var cooldownResetRepo = new FakePasswordResetRepository { ReplaceSucceeds = false };
        var cooldownHandler = new ResetAdminUserPasswordCommandHandler(
            eligibleRepo, cooldownResetRepo, new FakeEmailVerificationCodeService(),
            new RecordingMailSender(), new MutableTimeProvider(now));
        var cooldownEx = await Assert.ThrowsAsync<AdminUserRateLimitException>(() =>
            cooldownHandler.Handle(new ResetAdminUserPasswordCommand(actorId, targetId), CancellationToken.None));
        Assert.Equal("PASSWORD_RESET_COOLDOWN", cooldownEx.Code);

        // Mail failure releases cooldown
        var releaseResetRepo = new FakePasswordResetRepository();
        var mailFailHandler = new ResetAdminUserPasswordCommandHandler(
            eligibleRepo, releaseResetRepo, new FakeEmailVerificationCodeService(),
            new FailingMailSender(), new MutableTimeProvider(now));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mailFailHandler.Handle(new ResetAdminUserPasswordCommand(actorId, targetId), CancellationToken.None));
        Assert.True(releaseResetRepo.CooldownReleased);
    }

    [Fact]
    public async Task GetAdminUser_throws_not_found_when_missing()
    {
        var handler = new GetAdminUserQueryHandler(new InMemoryAdminUserRepository());
        await Assert.ThrowsAsync<AdminUserNotFoundException>(() =>
            handler.Handle(new GetAdminUserQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private sealed class InMemoryAdminUserRepository : IAdminUserRepository
    {
        public bool ForceDuplicateEmail { get; init; }
        public User? CreatedUser { get; private set; }
        public EmailVerificationChallenge? CreatedChallenge { get; private set; }
        public string? DeletedAvatarKey { get; init; }
        public Guid? DeletedTargetId { get; private set; }
        public AdminPasswordResetTarget? ResetTarget { get; init; }

        public Task<AdminUserDto?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<AdminUserDto?>(null);

        public Task<bool> TryCreateAsync(User user, EmailVerificationChallenge? challenge,
            CancellationToken cancellationToken)
        {
            if (ForceDuplicateEmail)
                return Task.FromResult(false);
            CreatedUser = user;
            CreatedChallenge = challenge;
            return Task.FromResult(true);
        }

        public Task<AdminUserDto> UpdateRoleAsync(Guid actorUserId, Guid targetUserId, UserRole role,
            DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            throw new AdminUserNotFoundException();

        public Task<string?> DeleteAsync(Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken)
        {
            DeletedTargetId = targetUserId;
            return Task.FromResult(DeletedAvatarKey);
        }

        public Task LockAsync(Guid actorUserId, Guid targetUserId, DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UnlockAsync(Guid targetUserId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AdminPasswordResetTarget?> GetPasswordResetTargetAsync(Guid targetUserId,
            CancellationToken cancellationToken) => Task.FromResult(ResetTarget);
    }

    private sealed class FailingMailSender : IMailSender
    {
        public Task SendAsync(MailMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SMTP down");
    }

    private sealed class ThrowingOnDeleteAvatarStore : IAvatarStore
    {
        public Task UploadAsync(string objectKey, byte[] content, string contentType,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AvatarContent> DownloadAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("MinIO down");
    }
}
