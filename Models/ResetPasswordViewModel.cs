using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models;

public sealed class ResetPasswordViewModel
{
    // Email, UserId và Token là trường ẩn lấy từ liên kết trong email.
    private const string InvalidLinkMessage = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";

    [Required(ErrorMessage = InvalidLinkMessage)]
    [EmailAddress(ErrorMessage = InvalidLinkMessage)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = InvalidLinkMessage)]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = InvalidLinkMessage)]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
