using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Repositories;

public sealed class EfAuthenticationRepository(ToeicDbContext dbContext) : IAuthenticationRepository
{
    public Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<AuthenticationSession?> FindActiveSessionAsync(string refreshTokenHash, DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        dbContext.UserSessions.AsNoTracking()
            .Where(session => session.RefreshTokenHash == refreshTokenHash && session.RevokedAtUtc == null &&
                              session.ExpiresAtUtc > nowUtc)
            .Join(dbContext.Users.AsNoTracking(), session => session.UserId, user => user.Id,
                (session, user) => new AuthenticationSession(session, user))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TryRotateSessionAsync(Guid currentSessionId, UserSession replacement,
        DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.UserSessions.Add(replacement);
            await dbContext.SaveChangesAsync(cancellationToken);

            var updated = await dbContext.UserSessions
                .Where(session => session.Id == currentSessionId && session.RevokedAtUtc == null &&
                                  session.ExpiresAtUtc > revokedAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(session => session.RevokedAtUtc, revokedAtUtc)
                    .SetProperty(session => session.ReplacedBySessionId, replacement.Id), cancellationToken);
            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task RevokeSessionAsync(string refreshTokenHash, DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.UserSessions
            .Where(session => session.RefreshTokenHash == refreshTokenHash && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, revokedAtUtc),
                cancellationToken);
    }
}
