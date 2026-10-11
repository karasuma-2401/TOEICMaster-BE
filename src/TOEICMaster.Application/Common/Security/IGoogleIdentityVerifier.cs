namespace TOEICMaster.Application.Common.Security;

public interface IGoogleIdentityVerifier
{
    Task<VerifiedGoogleAccount> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

public sealed record VerifiedGoogleAccount(
    string Subject,
    string Email,
    string DisplayName,
    bool CanLinkExistingAccountByEmail);
