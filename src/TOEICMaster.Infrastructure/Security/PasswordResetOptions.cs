using System.ComponentModel.DataAnnotations;

namespace TOEICMaster.Infrastructure.Security;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    [Range(1, 60)] public int TokenLifetimeMinutes { get; init; } = 10;
}
