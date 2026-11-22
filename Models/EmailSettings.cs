namespace DemoMVC.Models;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Để trống thì dùng UserName: Gmail yêu cầu địa chỉ gửi trùng tài khoản đăng nhập SMTP.
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "DemoMVC";

    // Để trống thì liên kết trong email được tạo theo domain của request hiện tại.
    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsGmail =>
        Host.Trim().EndsWith("gmail.com", StringComparison.OrdinalIgnoreCase);

    public string EffectiveSenderEmail =>
        string.IsNullOrWhiteSpace(SenderEmail) ? UserName.Trim() : SenderEmail.Trim();

    // Google hiển thị App Password dạng "abcd efgh ijkl mnop"; SMTP cần 16 ký tự liền nhau.
    public string EffectivePassword =>
        IsGmail ? string.Concat(Password.Where(character => !char.IsWhiteSpace(character))) : Password;
}
