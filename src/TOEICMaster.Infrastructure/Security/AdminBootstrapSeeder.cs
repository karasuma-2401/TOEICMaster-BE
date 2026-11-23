using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Security;

public sealed class AdminBootstrapSeeder(
    ToeicDbContext dbContext,
    IOptions<AdminBootstrapOptions> options,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IAdminBootstrapSeeder
{
    public async Task<AdminBootstrapSeedResult> SeedAsync(CancellationToken cancellationToken)
    {
        var email = options.Value.Email.Trim();
        if (email.Length is 0 or > 254 || !new EmailAddressAttribute().IsValid(email))
            throw new InvalidOperationException("ADMIN_SEED_EMAIL must be a valid email.");

        var normalizedEmail = email.ToUpperInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (user is not null)
        {
            if (!user.IsActive)
            {
                throw new InvalidOperationException(
                    "ADMIN_SEED_EMAIL already belongs to an unverified account. Please verify that account or use a new seed email.");
            }

            if (user.Role == UserRole.Admin)
                return new AdminBootstrapSeedResult(AdminBootstrapSeedAction.AlreadyAdmin);

            user.PromoteToAdmin();
            await dbContext.SaveChangesAsync(cancellationToken);
            return new AdminBootstrapSeedResult(AdminBootstrapSeedAction.PromotedExisting);
        }

        var displayName = options.Value.DisplayName.Trim();
        if (displayName.Length is 0 or > 100)
            throw new InvalidOperationException("ADMIN_SEED_DISPLAY_NAME must be between 1 and 100 characters.");

        var password = options.Value.Password;
        if (password.Length is < 8 or > 128)
            throw new InvalidOperationException("ADMIN_SEED_PASSWORD must be between 8 and 128 characters.");

        var nowUtc = timeProvider.GetUtcNow();
        user = User.CreateLocal(displayName, email, normalizedEmail, passwordHasher.Hash(password), nowUtc);
        user.VerifyEmail(nowUtc);
        user.PromoteToAdmin();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdminBootstrapSeedResult(AdminBootstrapSeedAction.Created);
    }
}
