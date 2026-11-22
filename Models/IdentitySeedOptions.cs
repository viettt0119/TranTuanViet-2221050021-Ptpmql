namespace DemoMVC.Models;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "AdminSeed";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Bật tạm thời (AdminSeed__ResetPassword=true) để đặt lại mật khẩu admin đã tồn tại theo AdminSeed:Password.
    public bool ResetPassword { get; set; }
}
