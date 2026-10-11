using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Common.Interfaces;

public interface IExternalLoginRepository
{
    Task<ExternalLoginResolution> ResolveGoogleAccountAsync(VerifiedGoogleAccount account,
        DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
}

public sealed record ExternalLoginResolution(User? User, bool RequiresExplicitLink)
{
    public static ExternalLoginResolution Resolved(User user) => new(user, false);
    public static ExternalLoginResolution LinkRequired() => new(null, true);
}
