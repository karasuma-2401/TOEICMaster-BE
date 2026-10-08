using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace TOEICMaster.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    [Required] public string Host { get; init; } = string.Empty;
    [Range(1, 65535)] public int Port { get; init; } = 1025;
    [Required, EmailAddress] public string FromAddress { get; init; } = "noreply@toeicmaster.test";
    public string FromName { get; init; } = "TOEICMaster";
    public SecureSocketOptions Security { get; init; } = SecureSocketOptions.None;
    public string? Username { get; init; }
    public string? Password { get; init; }
}
