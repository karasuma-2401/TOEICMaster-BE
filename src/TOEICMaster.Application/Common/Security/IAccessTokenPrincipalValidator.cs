namespace TOEICMaster.Application.Common.Security;

public static class SecurityClaimTypes
{
    public const string SecurityStamp = "security_stamp";
}

public interface IAccessTokenPrincipalValidator
{
    Task<bool> IsValidAsync(Guid userId, string? roleClaim, string? securityStampClaim,
        CancellationToken cancellationToken);
}
