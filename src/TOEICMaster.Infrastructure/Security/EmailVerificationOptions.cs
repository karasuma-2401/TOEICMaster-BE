using System.ComponentModel.DataAnnotations;

namespace TOEICMaster.Infrastructure.Security;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    [Required, MinLength(32)] public string HashKey { get; init; } = string.Empty;
    [Range(1, 60)] public int LifetimeMinutes { get; init; } = 10;
    [Range(10, 3600)] public int ResendCooldownSeconds { get; init; } = 60;
    [Range(1, 20)] public int MaxFailedAttempts { get; init; } = 5;
}
