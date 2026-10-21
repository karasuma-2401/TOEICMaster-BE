using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Profile;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Profiles;

public sealed class EfProfileRepository(ToeicDbContext dbContext) : IProfileRepository
{
    public Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken) =>
        await dbContext.Users.Where(user => user.Id == userId && user.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DisplayName, displayName),
                cancellationToken) == 1;

    public async Task<bool> UpdateProfileAsync(Guid userId, string displayName, string? phone, DateOnly? dateOfBirth,
        int? targetScore, CancellationToken cancellationToken) =>
        await dbContext.Users.Where(user => user.Id == userId && user.IsActive)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.DisplayName, displayName)
                    .SetProperty(user => user.Phone, phone)
                    .SetProperty(user => user.DateOfBirth, dateOfBirth)
                    .SetProperty(user => user.TargetScore, targetScore),
                cancellationToken) == 1;

    public async Task<bool> UpdateAvatarAsync(Guid userId, string objectKey, CancellationToken cancellationToken) =>
        await dbContext.Users.Where(user => user.Id == userId && user.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.AvatarObjectKey, objectKey),
                cancellationToken) == 1;

    public async Task<string?> ClearAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        var objectKey = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => user.AvatarObjectKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (objectKey is null)
            return null;

        var updated = await dbContext.Users
            .Where(user => user.Id == userId && user.IsActive && user.AvatarObjectKey == objectKey)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.AvatarObjectKey, (string?)null),
                cancellationToken);
        return updated == 1 ? objectKey : null;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentHash, string newHash,
        DateTimeOffset changedAtUtc, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var updated = await dbContext.Users
                .Where(user => user.Id == userId && user.IsActive && user.PasswordHash == currentHash)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, newHash),
                    cancellationToken);
            if (updated != 1)
                return false;

            await dbContext.UserSessions
                .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, changedAtUtc),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }
}
