using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace DemoMVC.Models;

public sealed class ApplicationUser : IdentityUser
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Tư cách đăng ký: Sinh viên hoặc Giảng viên / Cán bộ. Phải khớp với tư cách chọn khi đăng nhập.</summary>
    public AccountType AccountType { get; set; } = AccountType.Student;

    /// <summary>Mã sinh viên (10 chữ số, bắt đầu bằng 2) hoặc mã cán bộ (vd. GV0123). Duy nhất nếu có.</summary>
    [StringLength(20)]
    public string? MemberCode { get; set; }

    /// <summary>Tài khoản Giảng viên / Cán bộ tự đăng ký phải được Quản trị viên phê duyệt mới đăng nhập được.</summary>
    public bool IsApproved { get; set; } = true;
}
