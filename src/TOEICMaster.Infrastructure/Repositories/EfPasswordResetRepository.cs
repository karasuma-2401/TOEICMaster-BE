using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Repositories;

public sealed class EfPasswordResetRepository(ToeicDbContext dbContext) : IPasswordResetRepository
{
    public Task<PasswordResetAccount?> FindEligibleAccountAsync(string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedEmail && user.IsActive &&
                           user.EmailVerifiedAtUtc != null && user.PasswordHash != null)
            .Select(user => new PasswordResetAccount(user.Id, user.Email))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TryReplaceChallengeAsync(Guid userId, string codeHash,
        DateTimeOffset sentAtUtc, DateTimeOffset expiresAtUtc, TimeSpan cooldown,
        CancellationToken cancellationToken)
    {
        var cooldownThreshold = sentAtUtc - cooldown;
        var updated = await dbContext.PasswordResetChallenges
            .Where(item => item.UserId == userId && item.SentAtUtc <= cooldownThreshold)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.CodeHash, codeHash)
                .SetProperty(item => item.SentAtUtc, sentAtUtc)
                .SetProperty(item => item.ExpiresAtUtc, expiresAtUtc)
                .SetProperty(item => item.FailedAttempts, 0)
                .SetProperty(item => item.ResetTokenHash, (string?)null)
                .SetProperty(item => item.ResetTokenExpiresAtUtc, (DateTimeOffset?)null), cancellationToken);
        if (updated == 1)
            return true;
        if (await dbContext.PasswordResetChallenges.AsNoTracking()
                .AnyAsync(item => item.UserId == userId, cancellationToken))
            return false;

        dbContext.PasswordResetChallenges.Add(PasswordResetChallenge.Create(userId, codeHash,
            sentAtUtc, expiresAtUtc));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task ReleaseChallengeCooldownAsync(Guid userId, DateTimeOffset sentAtUtc,
        TimeSpan cooldown, CancellationToken cancellationToken)
    {
        var releasedAtUtc = sentAtUtc - cooldown;
        await dbContext.PasswordResetChallenges
            .Where(item => item.UserId == userId && item.SentAtUtc == sentAtUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SentAtUtc, releasedAtUtc),
                cancellationToken);
    }

    public async Task<PasswordResetCodeAttempt> VerifyCodeAsync(string normalizedEmail, string codeHash,
        DateTimeOffset nowUtc, int maxFailedAttempts, string resetTokenHash,
        DateTimeOffset resetTokenExpiresAtUtc, CancellationToken cancellationToken)
    {
        var account = await dbContext.Users.AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedEmail && user.IsActive &&
                           user.EmailVerifiedAtUtc != null && user.PasswordHash != null)
            .Join(dbContext.PasswordResetChallenges.AsNoTracking(), user => user.Id,
                challenge => challenge.UserId, (user, challenge) => challenge)
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null || !account.CanVerify(nowUtc, maxFailedAttempts))
            return PasswordResetCodeAttempt.Invalid;

        var expected = Encoding.ASCII.GetBytes(account.CodeHash);
        var supplied = Encoding.ASCII.GetBytes(codeHash);
        if (expected.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            await dbContext.PasswordResetChallenges
                .Where(item => item.UserId == account.UserId && item.CodeHash == account.CodeHash &&
                               item.ResetTokenHash == null && item.ExpiresAtUtc > nowUtc &&
                               item.FailedAttempts < maxFailedAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1), cancellationToken);
            return PasswordResetCodeAttempt.Invalid;
        }

        var updated = await dbContext.PasswordResetChallenges
            .Where(item => item.UserId == account.UserId && item.CodeHash == account.CodeHash &&
                           item.ResetTokenHash == null && item.ExpiresAtUtc > nowUtc &&
                           item.FailedAttempts < maxFailedAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ResetTokenHash, resetTokenHash)
                .SetProperty(item => item.ResetTokenExpiresAtUtc, resetTokenExpiresAtUtc), cancellationToken);
        return updated == 1 ? PasswordResetCodeAttempt.Verified : PasswordResetCodeAttempt.Invalid;
    }

    public async Task<bool> ResetPasswordAsync(string resetTokenHash, string passwordHash,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var challenge = await dbContext.PasswordResetChallenges.AsNoTracking()
                .Where(item => item.ResetTokenHash == resetTokenHash &&
                               item.ResetTokenExpiresAtUtc > nowUtc)
                .Select(item => new { item.UserId })
                .SingleOrDefaultAsync(cancellationToken);
            if (challenge is null)
                return false;

            var consumed = await dbContext.PasswordResetChallenges
                .Where(item => item.UserId == challenge.UserId && item.ResetTokenHash == resetTokenHash &&
                               item.ResetTokenExpiresAtUtc > nowUtc)
                .ExecuteDeleteAsync(cancellationToken);
            if (consumed != 1)
                return false;

            var updated = await dbContext.Users
                .Where(user => user.Id == challenge.UserId && user.IsActive &&
                               user.EmailVerifiedAtUtc != null && user.PasswordHash != null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, passwordHash),
                    cancellationToken);
            if (updated != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await dbContext.UserSessions
                .Where(session => session.UserId == challenge.UserId && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, nowUtc),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }
}
