using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Infrastructure.Security;

namespace TOEICMaster.Tests.Infrastructure.Security;

public sealed class GoogleIdentityVerifierTests
{
    [Fact]
    public async Task Malformed_token_is_mapped_to_the_application_authentication_error()
    {
        var verifier = new GoogleIdentityVerifier(Options.Create(new GoogleIdentityOptions
        {
            ClientId = "test-client.apps.googleusercontent.com"
        }));

        await Assert.ThrowsAsync<InvalidGoogleCredentialException>(() =>
            verifier.VerifyAsync("not-a-google-jwt", CancellationToken.None));
    }

    [Theory]
    [InlineData("learner@gmail.com", null, true)]
    [InlineData("learner@school.edu", "school.edu", true)]
    [InlineData("learner@outlook.com", null, false)]
    public void Account_linking_policy_follows_google_email_authority(string email, string? hostedDomain,
        bool canLink)
    {
        var payload = CreatePayload(email, hostedDomain);

        var account = GoogleIdentityVerifier.CreateVerifiedAccount(payload);

        Assert.Equal(canLink, account.CanLinkExistingAccountByEmail);
        Assert.Equal("google-subject", account.Subject);
    }

    [Fact]
    public void Unverified_email_is_rejected_even_when_the_token_signature_was_valid()
    {
        var payload = CreatePayload("learner@gmail.com", null);
        payload.EmailVerified = false;

        Assert.Throws<InvalidGoogleCredentialException>(() =>
            GoogleIdentityVerifier.CreateVerifiedAccount(payload));
    }

    [Fact]
    public void Missing_name_uses_a_bounded_email_fallback()
    {
        var payload = CreatePayload("learner@gmail.com", null);
        payload.Name = null;

        var account = GoogleIdentityVerifier.CreateVerifiedAccount(payload);

        Assert.Equal("learner", account.DisplayName);
    }

    private static GoogleJsonWebSignature.Payload CreatePayload(string email, string? hostedDomain) => new()
    {
        Subject = "google-subject",
        Email = email,
        EmailVerified = true,
        Name = "Google Learner",
        HostedDomain = hostedDomain
    };
}
