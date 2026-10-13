using System.Globalization;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Application.Authentication;

internal static class EmailVerificationMail
{
    public static MailMessage Create(string email, string code, TimeSpan lifetime)
    {
        var minutes = lifetime.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
        return new MailMessage(email, "Mã xác minh tài khoản TOEICMaster",
            $"Mã xác minh của bạn là: {code}\n\nMã có hiệu lực trong {minutes} phút. " +
            "Nếu bạn không đăng ký tài khoản TOEICMaster, hãy bỏ qua email này.");
    }
}
