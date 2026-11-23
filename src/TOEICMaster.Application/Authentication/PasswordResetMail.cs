using System.Globalization;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Application.Authentication;

internal static class PasswordResetMail
{
    public static MailMessage Create(string email, string code, TimeSpan lifetime)
    {
        var minutes = lifetime.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
        return new MailMessage(email, "TOEICMaster password reset code",
            $"Your password reset code is: {code}\n\nThis code is valid for {minutes} minutes. " +
            "If you did not request a password reset, please ignore this email.");
    }
}
