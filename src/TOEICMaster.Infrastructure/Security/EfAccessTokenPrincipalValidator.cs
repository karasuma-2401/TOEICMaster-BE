using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Security;

public sealed class EfAccessTokenPrincipalValidator(ToeicDbContext dbContext) : IAccessTokenPrincipalValidator
{
    public async Task<bool> IsValidAsync(Guid userId, string? roleClaim, string? securityStampClaim,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roleClaim) || !Guid.TryParse(securityStampClaim, out var expectedStamp))
            return false;

        var account = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.IsActive, user.Role, user.SecurityStamp })
            .SingleOrDefaultAsync(cancellationToken);

        return account is not null &&
               account.IsActive &&
               string.Equals(account.Role.ToString(), roleClaim, StringComparison.Ordinal) &&
               account.SecurityStamp == expectedStamp;
    }
}
