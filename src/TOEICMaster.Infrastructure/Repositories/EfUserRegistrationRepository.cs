using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Email;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Repositories;

public sealed class EfUserRegistrationRepository(ToeicDbContext dbContext) : IUserRegistrationRepository
{
    private const string NormalizedEmailIndex = "IX_users_NormalizedEmail";

    public Task<RegistrationAccount?> FindByNormalizedEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => new RegistrationAccount(user.Id, user.Email, user.IsActive,
                user.EmailVerifiedAtUtc != null))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TryAddAsync(User user, EmailVerificationChallenge challenge,
        MailMessage? outboxMessage, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        dbContext.EmailVerificationChallenges.Add(challenge);
        if (outboxMessage is not null)
        {
            dbContext.EmailOutboxMessages.Add(EmailOutboxMessage.Create(outboxMessage.To, outboxMessage.Subject,
                outboxMessage.TextBody, challenge.SentAtUtc));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NormalizedEmailIndex })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> TryReplacePendingRegistrationAsync(Guid userId, string codeHash,
        string pendingDisplayName, string pendingPasswordHash, DateTimeOffset sentAtUtc,
        DateTimeOffset expiresAtUtc, MailMessage? outboxMessage, CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var isPending = await dbContext.Users.AsNoTracking()
                .AnyAsync(user => user.Id == userId && !user.IsActive && user.EmailVerifiedAtUtc == null,
                    cancellationToken);
            if (!isPending)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var updated = await dbContext.EmailVerificationChallenges
                .Where(item => item.UserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.CodeHash, codeHash)
                    .SetProperty(item => item.PendingDisplayName, pendingDisplayName)
                    .SetProperty(item => item.PendingPasswordHash, pendingPasswordHash)
                    .SetProperty(item => item.SentAtUtc, sentAtUtc)
                    .SetProperty(item => item.ExpiresAtUtc, expiresAtUtc)
                    .SetProperty(item => item.FailedAttempts, 0), cancellationToken);
            if (updated == 0)
            {
                dbContext.EmailVerificationChallenges.Add(EmailVerificationChallenge.Create(userId, codeHash,
                    sentAtUtc, expiresAtUtc, pendingDisplayName, pendingPasswordHash));
            }

            if (outboxMessage is not null)
            {
                dbContext.EmailOutboxMessages.Add(EmailOutboxMessage.Create(outboxMessage.To, outboxMessage.Subject,
                    outboxMessage.TextBody, sentAtUtc));
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return true;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                dbContext.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        });
    }

    public async Task<bool> TryReplaceVerificationAsync(Guid userId, string codeHash,
        DateTimeOffset sentAtUtc, DateTimeOffset expiresAtUtc, TimeSpan cooldown,
        MailMessage? outboxMessage, CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var cooldownThreshold = sentAtUtc - cooldown;
            var updated = await dbContext.EmailVerificationChallenges
                .Where(item => item.UserId == userId && item.SentAtUtc <= cooldownThreshold)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.CodeHash, codeHash)
                    .SetProperty(item => item.SentAtUtc, sentAtUtc)
                    .SetProperty(item => item.ExpiresAtUtc, expiresAtUtc)
                    .SetProperty(item => item.FailedAttempts, 0), cancellationToken);
            if (updated == 1)
            {
                if (outboxMessage is not null)
                {
                    dbContext.EmailOutboxMessages.Add(EmailOutboxMessage.Create(outboxMessage.To, outboxMessage.Subject,
                        outboxMessage.TextBody, sentAtUtc));
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return true;
            }

            if (await dbContext.EmailVerificationChallenges.AsNoTracking()
                    .AnyAsync(item => item.UserId == userId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            dbContext.EmailVerificationChallenges.Add(EmailVerificationChallenge.Create(userId, codeHash,
                sentAtUtc, expiresAtUtc));
            if (outboxMessage is not null)
            {
                dbContext.EmailOutboxMessages.Add(EmailOutboxMessage.Create(outboxMessage.To, outboxMessage.Subject,
                    outboxMessage.TextBody, sentAtUtc));
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return true;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                dbContext.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        });
    }

    public async Task ReleaseVerificationCooldownAsync(Guid userId, DateTimeOffset sentAtUtc,
        TimeSpan cooldown, CancellationToken cancellationToken)
    {
        var releasedAtUtc = sentAtUtc - cooldown;
        await dbContext.EmailVerificationChallenges
            .Where(item => item.UserId == userId && item.SentAtUtc == sentAtUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SentAtUtc, releasedAtUtc),
                cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    public async Task<EmailVerificationAttempt> VerifyEmailAsync(string normalizedEmail, string codeHash,
        DateTimeOffset nowUtc, int maxFailedAttempts, CancellationToken cancellationToken)
    {
        var account = await dbContext.Users.Where(user => user.NormalizedEmail == normalizedEmail)
            .Join(dbContext.EmailVerificationChallenges, user => user.Id, challenge => challenge.UserId,
                (user, challenge) => new { User = user, Challenge = challenge })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null || account.User.IsActive || !account.Challenge.CanVerify(nowUtc, maxFailedAttempts))
            return EmailVerificationAttempt.Invalid;

        var expected = Encoding.ASCII.GetBytes(account.Challenge.CodeHash);
        var supplied = Encoding.ASCII.GetBytes(codeHash);
        if (expected.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            await dbContext.EmailVerificationChallenges
                .Where(item => item.UserId == account.User.Id && item.CodeHash == account.Challenge.CodeHash &&
                               item.ExpiresAtUtc > nowUtc && item.FailedAttempts < maxFailedAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1), cancellationToken);
            return EmailVerificationAttempt.Invalid;
        }

        account.User.VerifyEmail(nowUtc, account.Challenge.PendingDisplayName, account.Challenge.PendingPasswordHash);
        dbContext.EmailVerificationChallenges.Remove(account.Challenge);
        await dbContext.SaveChangesAsync(cancellationToken);
        return EmailVerificationAttempt.Verified;
    }
}
