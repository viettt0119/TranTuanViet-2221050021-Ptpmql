using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models;

public sealed class LoginViewModel
{
    /// <summary>Tư cách đăng nhập; phải khớp với tư cách đã đăng ký của tài khoản.</summary>
    public AccountType AccountType { get; set; } = AccountType.Student;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
