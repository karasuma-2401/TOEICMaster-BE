namespace TOEICMaster.Application.Common.Security;

public interface IPasswordResetTokenService
{
    TimeSpan Lifetime { get; }
    IssuedPasswordResetToken Issue();
    string Hash(string token);
}

public sealed record IssuedPasswordResetToken(string Token, string TokenHash);
