using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Repositories;

public sealed class EfUserRegistrationRepository(ToeicDbContext dbContext) : IUserRegistrationRepository
{
    private const string NormalizedEmailIndex = "IX_users_NormalizedEmail";

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NormalizedEmailIndex })
        {
            return false;
        }
    }
}
