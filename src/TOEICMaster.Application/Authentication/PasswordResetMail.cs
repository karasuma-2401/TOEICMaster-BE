using System.Globalization;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Application.Authentication;

internal static class PasswordResetMail
{
    public static MailMessage Create(string email, string code, TimeSpan lifetime)
    {
        var minutes = lifetime.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
        return new MailMessage(email, "Mã đặt lại mật khẩu TOEICMaster",
            $"Mã đặt lại mật khẩu của bạn là: {code}\n\nMã có hiệu lực trong {minutes} phút. " +
            "Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.");
    }
}
