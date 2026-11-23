using System.Globalization;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Application.Authentication;

internal static class EmailVerificationMail
{
    public static MailMessage Create(string email, string code, TimeSpan lifetime)
    {
        var minutes = lifetime.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
        return new MailMessage(email, "TOEICMaster account verification code",
            $"Your verification code is: {code}\n\nThis code is valid for {minutes} minutes. " +
            "If you did not register for a TOEICMaster account, please ignore this email.");
    }
}
