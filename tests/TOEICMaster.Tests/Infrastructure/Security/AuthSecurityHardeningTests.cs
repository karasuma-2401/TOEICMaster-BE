using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Authentication.Commands.RefreshSession;
using TOEICMaster.Application.Authentication.Commands.Register;
using TOEICMaster.Application.Authentication.Commands.VerifyEmail;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Email;
using TOEICMaster.Infrastructure.Health;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Repositories;
using TOEICMaster.Infrastructure.Security;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Infrastructure.Security;

public sealed class AuthSecurityHardeningTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void User_rotates_security_stamp_on_lock_unlock_role_change_and_verify()
    {
        var user = User.CreateLocal("Admin", "admin@example.com", "ADMIN@EXAMPLE.COM", null, Now);
        var initial = user.SecurityStamp;

        user.VerifyEmail(Now, "Verified Admin", "hashed-password");
        Assert.NotEqual(initial, user.SecurityStamp);
        Assert.Equal("Verified Admin", user.DisplayName);
        Assert.Equal("hashed-password", user.PasswordHash);

        var afterVerify = user.SecurityStamp;
        user.PromoteToAdmin();
        Assert.NotEqual(afterVerify, user.SecurityStamp);

        var afterPromote = user.SecurityStamp;
        user.ChangeRole(UserRole.User);
        Assert.NotEqual(afterPromote, user.SecurityStamp);

        var afterDemote = user.SecurityStamp;
        user.Lock();
        Assert.NotEqual(afterDemote, user.SecurityStamp);
    }

    [Fact]
    public void Email_outbox_message_transitions_through_transient_retries_and_permanent_failure()
    {
        var message = EmailOutboxMessage.Create("student@example.com", "Verify", "Code: 123456", Now);
        Assert.Equal(EmailOutboxStatus.Pending, message.Status);
        Assert.Equal(0, message.AttemptCount);

        message.RecordFailure("Temporary 421", Now.AddSeconds(10), EmailOutboxProcessor.MaxAttempts, isTransient: true);
        Assert.Equal(EmailOutboxStatus.Pending, message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal(Now.AddSeconds(10), message.NextAttemptAtUtc);

        message.RecordFailure("Permanent 550", Now.AddSeconds(20), EmailOutboxProcessor.MaxAttempts, isTransient: false);
        Assert.Equal(EmailOutboxStatus.Failed, message.Status);
        Assert.Equal(2, message.AttemptCount);

        Assert.True(EmailOutboxProcessor.IsTransientFailure(new SocketException()));
        Assert.True(EmailOutboxProcessor.IsTransientFailure(new TimeoutException()));
        Assert.True(EmailOutboxProcessor.IsTransientFailure(
            new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.ServiceNotAvailable, "421")));
        Assert.False(EmailOutboxProcessor.IsTransientFailure(
            new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "550")));
    }

    [Fact]
    public async Task Development_mail_sink_writes_messages_and_health_check_reports_healthy()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"toeicmaster-mail-sink-{Guid.NewGuid():N}");
        try
        {
            var options = Options.Create(new SmtpOptions
            {
                UseDevelopmentSink = true,
                DevelopmentSinkDirectory = tempDir
            });
            var sink = new DevelopmentMailSink(options, NullLogger<DevelopmentMailSink>.Instance,
                new FixedTimeProvider(Now));
            await sink.SendAsync(new MailMessage("learner@example.com", "Verify email", "Code: 654321"),
                CancellationToken.None);

            var files = Directory.GetFiles(tempDir, "*.txt");
            var file = Assert.Single(files);
            var text = await File.ReadAllTextAsync(file);
            Assert.Contains("learner@example.com", text);
            Assert.Contains("654321", text);

            var health = await new SmtpHealthCheck(options).CheckHealthAsync(new HealthCheckContext(),
                CancellationToken.None);
            Assert.Equal(HealthStatus.Healthy, health.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Stale_admin_jwt_is_immediately_rejected_after_demotion_or_locking_in_postgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        using var factory = new DatabaseJwtApiFactory();
        var adminEmail = $"stale-admin-{Guid.NewGuid():N}@toeicmaster.test";
        var admin = User.CreateLocal("Admin User", adminEmail, adminEmail.ToUpperInvariant(), "hash", Now);
        admin.VerifyEmail(Now);
        admin.PromoteToAdmin();

        await using (var setupDb = CreateDbContext(connectionString))
        {
            setupDb.Users.Add(admin);
            await setupDb.SaveChangesAsync();
        }

        try
        {
            var tokenService = factory.Services.GetRequiredService<IAuthenticationTokenService>();
            var token = tokenService.IssueForLogin(admin, DateTimeOffset.UtcNow, false).AccessToken;
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using (var beforeDemote = await client.GetAsync("/api/v1/admin/users"))
                Assert.Equal(HttpStatusCode.OK, beforeDemote.StatusCode);

            await using (var demoteDb = CreateDbContext(connectionString))
            {
                var tracked = await demoteDb.Users.SingleAsync(u => u.Id == admin.Id);
                tracked.ChangeRole(UserRole.User);
                await demoteDb.SaveChangesAsync();
            }

            using (var afterDemote = await client.GetAsync("/api/v1/admin/users"))
                Assert.Equal(HttpStatusCode.Unauthorized, afterDemote.StatusCode);

            await using (var rePromoteAndLockDb = CreateDbContext(connectionString))
            {
                var tracked = await rePromoteAndLockDb.Users.SingleAsync(u => u.Id == admin.Id);
                tracked.PromoteToAdmin();
                await rePromoteAndLockDb.SaveChangesAsync();

                var freshAdminToken = tokenService.IssueForLogin(tracked, DateTimeOffset.UtcNow, false).AccessToken;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", freshAdminToken);

                tracked.Lock();
                await rePromoteAndLockDb.SaveChangesAsync();
            }

            using (var afterLock = await client.GetAsync("/api/v1/admin/users"))
                Assert.Equal(HttpStatusCode.Unauthorized, afterLock.StatusCode);
        }
        finally
        {
            await using var cleanupDb = CreateDbContext(connectionString);
            await cleanupDb.Users.Where(u => u.Id == admin.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Pending_registration_replaces_pre_registered_attacker_password_and_queues_outbox_in_postgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var email = $"hijack-{Guid.NewGuid():N}@toeicmaster.test";
        var normalized = email.ToUpperInvariant();
        var hasher = new BcryptPasswordHasher();
        var codeService = new EmailVerificationCodeService(Options.Create(new EmailVerificationOptions
        {
            HashKey = "test-email-verification-key-32-characters",
            LifetimeMinutes = 10,
            ResendCooldownSeconds = 60,
            MaxFailedAttempts = 5
        }));
        var time = new MutableTimeProvider(Now);

        try
        {
            await using var db = CreateDbContext(connectionString);
            var repo = new EfUserRegistrationRepository(db);
            var registerHandler = new RegisterCommandHandler(repo, hasher, codeService, time);
            var verifyHandler = new VerifyEmailCommandHandler(repo, codeService, time);

            // Attacker pre-registers victim's email with attacker password.
            await registerHandler.Handle(new RegisterCommand("Attacker", email, "AttackerPass123!"),
                CancellationToken.None);

            var attackerOutbox = await db.EmailOutboxMessages.AsNoTracking()
                .Where(m => m.To == email)
                .OrderBy(m => m.CreatedAtUtc)
                .SingleAsync();
            var attackerCode = ExtractSixDigitCode(attackerOutbox.TextBody);

            // Victim registers same email 5 seconds later (within cooldown) with their own password.
            time.Advance(5);
            await registerHandler.Handle(new RegisterCommand("Real Victim", email, "VictimPass456!"),
                CancellationToken.None);

            // Attacker's old OTP must no longer work.
            await Assert.ThrowsAsync<InvalidEmailVerificationException>(() =>
                verifyHandler.Handle(new VerifyEmailCommand(email, attackerCode), CancellationToken.None));

            var latestOutbox = await db.EmailOutboxMessages.AsNoTracking()
                .Where(m => m.To == email)
                .OrderByDescending(m => m.CreatedAtUtc)
                .FirstAsync();
            var victimCode = ExtractSixDigitCode(latestOutbox.TextBody);

            await verifyHandler.Handle(new VerifyEmailCommand(email, victimCode), CancellationToken.None);

            var verifiedUser = await db.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == normalized);
            Assert.True(verifiedUser.IsActive);
            Assert.Equal("Real Victim", verifiedUser.DisplayName);
            Assert.False(hasher.Verify("AttackerPass123!", verifiedUser.PasswordHash));
            Assert.True(hasher.Verify("VictimPass456!", verifiedUser.PasswordHash));

            // Outbox processor delivers pending messages via IMailSender.
            var sender = new RecordingMailSender();
            var processor = new EmailOutboxProcessor(db, sender, time, NullLogger<EmailOutboxProcessor>.Instance);
            var delivered = await processor.ProcessDueAsync(20, CancellationToken.None);
            Assert.True(delivered >= 2);
            Assert.Contains(sender.Messages, m => m.To == email && m.TextBody.Contains(victimCode));
        }
        finally
        {
            await using var cleanupDb = CreateDbContext(connectionString);
            await cleanupDb.Users.Where(u => u.NormalizedEmail == normalized).ExecuteDeleteAsync();
            await cleanupDb.EmailOutboxMessages.Where(m => m.To == email).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Replaying_rotated_refresh_token_revokes_entire_family_in_postgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var email = $"refresh-family-{Guid.NewGuid():N}@toeicmaster.test";
        var user = User.CreateLocal("Learner", email, email.ToUpperInvariant(), "hash", Now);
        user.VerifyEmail(Now);
        var tokenService = new JwtAuthenticationTokenService(Options.Create(new JwtOptions
        {
            Issuer = "TOEICMaster.Tests",
            Audience = "TOEICMaster.Tests.Client",
            SigningKey = "test-only-signing-key-with-32-characters",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 1,
            RememberMeRefreshTokenDays = 30
        }));
        var time = new MutableTimeProvider(Now);

        try
        {
            await using var db = CreateDbContext(connectionString);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var repo = new EfAuthenticationRepository(db);
            var initialTokens = tokenService.IssueForLogin(user, Now, rememberMe: false);
            var initialSession = UserSession.Create(user.Id, initialTokens.RefreshTokenHash, Now,
                initialTokens.RefreshTokenExpiresAtUtc);
            await repo.AddSessionAsync(initialSession, CancellationToken.None);

            var handler = new RefreshSessionCommandHandler(repo, tokenService, time);
            time.Advance(60);
            var rotated = await handler.Handle(new RefreshSessionCommand(initialTokens.RefreshToken),
                CancellationToken.None);

            // Replay of initial token T1 must fail AND revoke successor T2 in the same family.
            time.Advance(60);
            await Assert.ThrowsAsync<InvalidSessionException>(() =>
                handler.Handle(new RefreshSessionCommand(initialTokens.RefreshToken), CancellationToken.None));

            // Subsequent use of T2 must also fail because the entire family was revoked.
            await Assert.ThrowsAsync<InvalidSessionException>(() =>
                handler.Handle(new RefreshSessionCommand(rotated.RefreshToken), CancellationToken.None));
        }
        finally
        {
            await using var cleanupDb = CreateDbContext(connectionString);
            await cleanupDb.UserSessions.Where(s => s.UserId == user.Id && s.ReplacedBySessionId != null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplacedBySessionId, (Guid?)null));
            await cleanupDb.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    private sealed class DatabaseJwtApiFactory : ApiFactory
    {
        protected override bool UseDatabaseAccessTokenValidation => true;
    }

    private static ToeicDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
            .Options);

    private static string ExtractSixDigitCode(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\b\d{6}\b");
        Assert.True(match.Success, $"Could not find 6-digit OTP in email body: {text}");
        return match.Value;
    }
}
