namespace TOEICMaster.Infrastructure.Security;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
