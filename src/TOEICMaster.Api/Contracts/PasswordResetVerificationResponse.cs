using TOEICMaster.Application.Authentication.Commands.VerifyPasswordReset;

namespace TOEICMaster.Api.Contracts;

public sealed record PasswordResetVerificationResponse(string ResetToken, DateTimeOffset ExpiresAtUtc)
{
    public static PasswordResetVerificationResponse From(PasswordResetVerificationResult result) =>
        new(result.ResetToken, result.ExpiresAtUtc);
}
