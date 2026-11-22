using System.Text.Encodings.Web;

namespace DemoMVC.Services;

public sealed class AuthEmailTemplateRenderer(HtmlEncoder htmlEncoder)
{
    // Khớp với cấu hình token trong Program.cs.
    public static readonly TimeSpan ConfirmationLinkLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan PasswordResetLinkLifetime = TimeSpan.FromHours(1);

    public string Confirmation(string fullName, string confirmationUrl)
    {
        return Layout(
            fullName,
            "Cảm ơn bạn đã đăng ký tài khoản DemoMVC. Hãy bấm nút bên dưới để xác thực địa chỉ email và kích hoạt tài khoản.",
            "Xác thực email",
            confirmationUrl,
            $"Liên kết có hiệu lực trong {ConfirmationLinkLifetime.TotalHours:0} giờ. Nếu bạn không đăng ký tài khoản, hãy bỏ qua email này.");
    }

    public string PasswordReset(string fullName, string resetUrl)
    {
        return Layout(
            fullName,
            "Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.",
            "Đặt lại mật khẩu",
            resetUrl,
            $"Liên kết có hiệu lực trong {PasswordResetLinkLifetime.TotalHours:0} giờ. Nếu bạn không yêu cầu thay đổi này, hãy bỏ qua email.");
    }

    public string PasswordChanged(string fullName)
    {
        return $"""
            <p>Xin chào {htmlEncoder.Encode(fullName)},</p>
            <p>Mật khẩu tài khoản của bạn vừa được thay đổi.</p>
            <p>Nếu bạn không thực hiện thay đổi này, hãy liên hệ quản trị viên ngay.</p>
            """;
    }

    private string Layout(string fullName, string intro, string buttonText, string url, string footer)
    {
        var encodedUrl = htmlEncoder.Encode(url);
        return $"""
            <p>Xin chào {htmlEncoder.Encode(fullName)},</p>
            <p>{htmlEncoder.Encode(intro)}</p>
            <p><a href="{encodedUrl}" style="display:inline-block;padding:10px 18px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px">{htmlEncoder.Encode(buttonText)}</a></p>
            <p>Nếu nút không hoạt động, hãy sao chép liên kết sau vào trình duyệt:</p>
            <p>{encodedUrl}</p>
            <p>{htmlEncoder.Encode(footer)}</p>
            """;
    }
}
