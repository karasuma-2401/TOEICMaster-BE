using System.ComponentModel.DataAnnotations;

namespace TOEICMaster.Infrastructure;

public sealed class ConnectionStringsOptions
{
    [Required] public string Postgres { get; init; } = string.Empty;
    [Required] public string Redis { get; init; } = string.Empty;
}
