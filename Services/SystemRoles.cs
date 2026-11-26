namespace DemoMVC.Services;

/// <summary>
/// Vai trò hệ thống được tạo sẵn khi khởi động. Không đổi tên, không xoá được (ứng dụng phụ thuộc vào tên của chúng);
/// quyền của User và GiangVien vẫn chỉnh được trên ma trận phân quyền.
/// </summary>
public static class SystemRoles
{
    public const string Admin = "Admin";

    /// <summary>Vai trò mặc định của sinh viên tự đăng ký.</summary>
    public const string User = "User";

    /// <summary>Vai trò gán cho giảng viên / cán bộ khi được Quản trị viên phê duyệt.</summary>
    public const string Lecturer = "GiangVien";

    public static readonly IReadOnlyList<string> All = [Admin, User, Lecturer];

    public static bool IsSystemRole(string? roleName) =>
        roleName is not null && All.Contains(roleName, StringComparer.OrdinalIgnoreCase);
}
