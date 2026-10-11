using System.ComponentModel.DataAnnotations;

namespace TOEICMaster.Infrastructure.Security;

public sealed class GoogleIdentityOptions
{
    public const string SectionName = "Google";

    [Required]
    public string ClientId { get; init; } = string.Empty;
}
