using System.Text.Encodings.Web;

namespace DemoMVC.Services;

public sealed class AuthEmailTemplateRenderer(HtmlEncoder htmlEncoder)
{
    public string Confirmation(string fullName, string confirmationUrl)
    {
        return $"""
            <p>Xin chào {htmlEncoder.Encode(fullName)},</p>
            <p>Để xác thực địa chỉ email, hãy mở liên kết sau:</p>
            <p><a href="{htmlEncoder.Encode(confirmationUrl)}">Xác thực email</a></p>
            """;
    }

    public string PasswordReset(string fullName, string resetUrl)
    {
        return $"""
            <p>Xin chào {htmlEncoder.Encode(fullName)},</p>
            <p>Để đặt lại mật khẩu, hãy mở liên kết sau:</p>
            <p><a href="{htmlEncoder.Encode(resetUrl)}">Đặt lại mật khẩu</a></p>
            <p>Nếu bạn không yêu cầu thay đổi này, hãy bỏ qua email.</p>
            """;
    }

    public string PasswordChanged(string fullName)
    {
        return $"""
            <p>Xin chào {htmlEncoder.Encode(fullName)},</p>
            <p>Mật khẩu tài khoản của bạn vừa được thay đổi.</p>
            <p>Nếu bạn không thực hiện thay đổi này, hãy liên hệ quản trị viên ngay.</p>
            """;
    }
}
