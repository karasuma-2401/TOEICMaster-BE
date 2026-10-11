using System.ComponentModel.DataAnnotations;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Infrastructure.Security;

public sealed class GoogleIdentityVerifier(IOptions<GoogleIdentityOptions> options) : IGoogleIdentityVerifier
{
    private readonly GoogleJsonWebSignature.ValidationSettings _validationSettings = new()
    {
        Audience = [options.Value.ClientId]
    };

    public async Task<VerifiedGoogleAccount> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, _validationSettings)
                .WaitAsync(cancellationToken);
        }
        catch (InvalidJwtException)
        {
            throw new InvalidGoogleCredentialException();
        }

        return CreateVerifiedAccount(payload);
    }

    internal static VerifiedGoogleAccount CreateVerifiedAccount(GoogleJsonWebSignature.Payload payload)
    {
        var subject = payload.Subject?.Trim();
        var email = payload.Email?.Trim();
        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(subject) || subject.Length > 255 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            throw new InvalidGoogleCredentialException();

        var displayName = payload.Name?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = email[..email.IndexOf('@')];
        if (displayName.Length > 100)
            displayName = displayName[..100];

        var googleIsAuthoritativeForEmail = email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) ||
                                            !string.IsNullOrWhiteSpace(payload.HostedDomain);
        return new VerifiedGoogleAccount(subject, email, displayName, googleIsAuthoritativeForEmail);
    }
}
