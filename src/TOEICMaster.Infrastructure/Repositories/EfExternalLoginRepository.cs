using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Repositories;

public sealed class EfExternalLoginRepository(ToeicDbContext dbContext) : IExternalLoginRepository
{
    private const string GoogleProvider = "google";
    private const string ProviderSubjectIndex = "IX_external_identities_Provider_Subject";
    private const string NormalizedEmailIndex = "IX_users_NormalizedEmail";

    public async Task<ExternalLoginResolution> ResolveGoogleAccountAsync(VerifiedGoogleAccount account,
        DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
    {
        var existingIdentity = await FindBySubjectAsync(account.Subject, cancellationToken);
        if (existingIdentity is not null)
            return ExternalLoginResolution.Resolved(existingIdentity);

        var normalizedEmail = account.Email.ToUpperInvariant();
        var existingEmail = await FindByEmailAsync(normalizedEmail, cancellationToken);
        if (existingEmail is not null)
            return await ResolveExistingEmailAsync(existingEmail, account, createdAtUtc, cancellationToken);

        var user = User.CreateExternal(account.DisplayName, account.Email, normalizedEmail, createdAtUtc);
        dbContext.Users.Add(user);
        dbContext.ExternalIdentities.Add(ExternalIdentity.Create(user.Id, GoogleProvider, account.Subject,
            createdAtUtc));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ExternalLoginResolution.Resolved(user);
        }
        catch (DbUpdateException exception) when (IsExpectedIdentityRace(exception))
        {
            dbContext.ChangeTracker.Clear();
            existingIdentity = await FindBySubjectAsync(account.Subject, cancellationToken);
            if (existingIdentity is not null)
                return ExternalLoginResolution.Resolved(existingIdentity);

            existingEmail = await FindByEmailAsync(normalizedEmail, cancellationToken);
            if (existingEmail is null)
                throw;
            return await ResolveExistingEmailAsync(existingEmail, account, createdAtUtc, cancellationToken);
        }
    }

    private async Task<ExternalLoginResolution> ResolveExistingEmailAsync(User user, VerifiedGoogleAccount account,
        DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
    {
        if (!user.IsActive && user.EmailVerifiedAtUtc is not null)
            return ExternalLoginResolution.Resolved(user);
        return account.CanLinkExistingAccountByEmail
            ? await LinkAsync(user, account.Subject, createdAtUtc, cancellationToken)
            : ExternalLoginResolution.LinkRequired();
    }

    private async Task<ExternalLoginResolution> LinkAsync(User user, string subject, DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (user.EmailVerifiedAtUtc is null)
        {
            dbContext.Users.Attach(user);
            user.AcceptVerifiedExternalIdentity(createdAtUtc);
            var challenge = await dbContext.EmailVerificationChallenges
                .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
            if (challenge is not null)
                dbContext.EmailVerificationChallenges.Remove(challenge);
        }
        dbContext.ExternalIdentities.Add(ExternalIdentity.Create(user.Id, GoogleProvider, subject, createdAtUtc));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ExternalLoginResolution.Resolved(user);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ProviderSubjectIndex })
        {
            dbContext.ChangeTracker.Clear();
            var existingIdentity = await FindBySubjectAsync(subject, cancellationToken);
            if (existingIdentity is null)
                throw;
            return ExternalLoginResolution.Resolved(existingIdentity);
        }
    }

    private Task<User?> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        dbContext.ExternalIdentities.AsNoTracking()
            .Where(identity => identity.Provider == GoogleProvider && identity.Subject == subject)
            .Join(dbContext.Users.AsNoTracking(), identity => identity.UserId, user => user.Id, (_, user) => user)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

    private static bool IsExpectedIdentityRace(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ProviderSubjectIndex or NormalizedEmailIndex
        };
}
