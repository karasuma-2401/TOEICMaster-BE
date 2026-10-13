namespace TOEICMaster.Application.Common.Security;

public interface IEmailVerificationCodeService
{
    TimeSpan Lifetime { get; }
    TimeSpan ResendCooldown { get; }
    int MaxFailedAttempts { get; }
    IssuedEmailVerificationCode Issue(string normalizedEmail);
    string Hash(string normalizedEmail, string code);
}

public sealed record IssuedEmailVerificationCode(string Code, string CodeHash);
