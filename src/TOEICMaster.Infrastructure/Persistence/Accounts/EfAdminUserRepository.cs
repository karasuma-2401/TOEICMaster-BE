using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Accounts;

public sealed class EfAdminUserRepository(ToeicDbContext dbContext) : IAdminUserRepository
{
    private const string NormalizedEmailIndex = "IX_users_NormalizedEmail";

    public async Task<AdminUserDto?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await AdminUserReader.ProjectRows(
                dbContext.Users.AsNoTracking().Where(user => user.Id == userId),
                dbContext.UserSessions.AsNoTracking())
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : AdminUserReader.ToDto(row);
    }

    public async Task<bool> TryCreateAsync(User user, EmailVerificationChallenge? challenge,
        CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        if (challenge is not null)
            dbContext.EmailVerificationChallenges.Add(challenge);

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

    public async Task<AdminUserDto> UpdateRoleAsync(Guid actorUserId, Guid targetUserId, UserRole role,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == targetUserId,
                cancellationToken) ?? throw new AdminUserNotFoundException();

            if (user.Role == role)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            if (actorUserId == targetUserId)
                throw new AdminUserOperationConflictException(
                    role == UserRole.User ? "CANNOT_DEMOTE_SELF" : "CANNOT_CHANGE_SELF_ROLE",
                    "Cannot change your own role.");

            if (user.Role == UserRole.Admin && role == UserRole.User)
            {
                if (user.IsActive)
                {
                    await EnsureAnotherActiveAdminExistsAsync(targetUserId,
                        "Cannot demote the last active admin.", cancellationToken);
                }

                await RevokeActiveSessionsAsync(targetUserId, nowUtc, cancellationToken);
            }

            user.ChangeRole(role);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return await GetByIdAsync(targetUserId, cancellationToken)
            ?? throw new AdminUserNotFoundException();
    }

    public async Task<string?> DeleteAsync(Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == targetUserId,
                cancellationToken) ?? throw new AdminUserNotFoundException();

            if (actorUserId == targetUserId)
                throw new AdminUserOperationConflictException("CANNOT_DELETE_SELF",
                    "Cannot delete your own admin account.");

            if (user.Role == UserRole.Admin && user.IsActive)
            {
                await EnsureAnotherActiveAdminExistsAsync(targetUserId,
                    "Cannot delete the last active admin.", cancellationToken);
            }

            // Clear UserSession self-referencing ReplacedBySessionId FK before cascade-deleting the user's sessions.
            await dbContext.UserSessions
                .Where(session => session.UserId == targetUserId && session.ReplacedBySessionId != null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.ReplacedBySessionId, (Guid?)null),
                    cancellationToken);

            var avatarObjectKey = user.AvatarObjectKey;
            dbContext.Users.Remove(user);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            {
                dbContext.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken);
                throw new AdminUserOperationConflictException("USER_HAS_REFERENCED_CONTENT",
                    "Cannot delete a user who owns content or referenced data.");
            }

            await transaction.CommitAsync(cancellationToken);
            return avatarObjectKey;
        });
    }

    public async Task LockAsync(Guid actorUserId, Guid targetUserId, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == targetUserId,
                cancellationToken) ?? throw new AdminUserNotFoundException();

            if (actorUserId == targetUserId)
                throw new AdminUserOperationConflictException("CANNOT_LOCK_SELF",
                    "Cannot lock your own admin account.");

            if (!user.IsActive && user.EmailVerifiedAtUtc is null)
                throw new AdminUserOperationConflictException("USER_PENDING_VERIFICATION",
                    "Cannot lock an account pending email verification.");

            if (user.IsActive && user.Role == UserRole.Admin)
            {
                await EnsureAnotherActiveAdminExistsAsync(targetUserId,
                    "Cannot lock the last active admin.", cancellationToken);
            }

            if (user.IsActive)
            {
                user.Lock();
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await RevokeActiveSessionsAsync(targetUserId, nowUtc, cancellationToken);
            await dbContext.PasswordResetChallenges
                .Where(challenge => challenge.UserId == targetUserId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task UnlockAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == targetUserId,
            cancellationToken) ?? throw new AdminUserNotFoundException();

        if (!user.IsActive && user.EmailVerifiedAtUtc is null)
            throw new AdminUserOperationConflictException("USER_PENDING_VERIFICATION",
                "Cannot unlock an account pending email verification.");

        if (!user.IsActive)
        {
            user.Unlock();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<AdminPasswordResetTarget?> GetPasswordResetTargetAsync(Guid targetUserId,
        CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.Id == targetUserId)
            .Select(user => new AdminPasswordResetTarget(
                user.Id,
                user.Email,
                user.NormalizedEmail,
                user.IsActive,
                user.EmailVerifiedAtUtc != null,
                user.PasswordHash != null))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task EnsureAnotherActiveAdminExistsAsync(Guid excludedUserId, string conflictMessage,
        CancellationToken cancellationToken)
    {
        var activeAdminIds = await dbContext.Database
            .SqlQuery<Guid>(
                $"SELECT \"Id\" AS \"Value\" FROM users WHERE \"Role\" = {(short)UserRole.Admin} AND \"IsActive\" = true FOR UPDATE")
            .ToListAsync(cancellationToken);

        if (activeAdminIds.Count(id => id != excludedUserId) == 0)
            throw new AdminUserOperationConflictException("LAST_ACTIVE_ADMIN", conflictMessage);
    }

    private Task RevokeActiveSessionsAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
        dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, nowUtc),
                cancellationToken);
}
