using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models;

/// <summary>Tư cách đăng ký / đăng nhập của tài khoản.</summary>
public enum AccountType
{
    [Display(Name = "Sinh viên")]
    Student = 0,

    [Display(Name = "Giảng viên / Cán bộ")]
    Staff = 1
}

public static class AccountTypeExtensions
{
    public static string DisplayName(this AccountType type) => type switch
    {
        AccountType.Staff => "Giảng viên / Cán bộ",
        _ => "Sinh viên"
    };

    public static string CodeLabel(this AccountType type) =>
        type == AccountType.Staff ? "Mã cán bộ" : "Mã sinh viên";
}
