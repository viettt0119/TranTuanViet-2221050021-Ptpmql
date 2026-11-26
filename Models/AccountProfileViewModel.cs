using DemoMVC.Models.ViewModels.Students;

namespace DemoMVC.Models;

/// <summary>Dữ liệu trang "Tài khoản của tôi": chỉ gồm thông tin của chính người đang đăng nhập.</summary>
public sealed class AccountProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public string? MemberCode { get; set; }

    /// <summary>null khi tài khoản chưa liên kết với hồ sơ sinh viên nào.</summary>
    public StudentDetailsViewModel? Student { get; set; }
}
