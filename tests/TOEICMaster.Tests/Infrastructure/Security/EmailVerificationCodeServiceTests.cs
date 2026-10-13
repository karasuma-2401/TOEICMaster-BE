using Microsoft.Extensions.Options;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Security;

namespace TOEICMaster.Tests.Infrastructure.Security;

public sealed class EmailVerificationCodeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Code_service_issues_six_digits_and_uses_a_contextual_hmac()
    {
        var service = new EmailVerificationCodeService(Options.Create(new EmailVerificationOptions
        {
            HashKey = "test-email-verification-key-32-characters"
        }));

        var issued = service.Issue("LEARNER@EXAMPLE.COM");

        Assert.Matches("^[0-9]{6}$", issued.Code);
        Assert.Equal(64, issued.CodeHash.Length);
        Assert.Equal(issued.CodeHash, service.Hash("LEARNER@EXAMPLE.COM", issued.Code));
        Assert.NotEqual(issued.CodeHash, service.Hash("OTHER@EXAMPLE.COM", issued.Code));
    }

    [Fact]
    public void Challenge_enforces_expiry_attempt_limit_and_resend_cooldown()
    {
        var challenge = EmailVerificationChallenge.Create(Guid.NewGuid(), new string('A', 64), Now,
            Now.AddMinutes(10));

        Assert.False(challenge.CanResend(Now.AddSeconds(59), TimeSpan.FromMinutes(1)));
        Assert.True(challenge.CanResend(Now.AddMinutes(1), TimeSpan.FromMinutes(1)));
        for (var attempt = 0; attempt < 5; attempt++)
            challenge.RecordFailedAttempt();
        Assert.False(challenge.CanVerify(Now.AddMinutes(1), 5));

        challenge.ReplaceCode(new string('B', 64), Now.AddMinutes(1), Now.AddMinutes(11));
        Assert.True(challenge.CanVerify(Now.AddMinutes(2), 5));
        Assert.False(challenge.CanVerify(Now.AddMinutes(11), 5));
    }

    [Fact]
    public void Verified_google_identity_safely_claims_a_pending_local_account()
    {
        var user = User.CreateLocal("Learner", "learner@gmail.com", "LEARNER@GMAIL.COM", "attacker-known-hash",
            Now);

        user.AcceptVerifiedExternalIdentity(Now.AddMinutes(1));

        Assert.True(user.IsActive);
        Assert.Equal(Now.AddMinutes(1), user.EmailVerifiedAtUtc);
        Assert.Null(user.PasswordHash);
    }
}
