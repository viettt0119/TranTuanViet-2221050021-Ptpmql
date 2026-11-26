namespace DemoMVC.Services;

/// <summary>
/// Tên hiển thị của controller trên ma trận phân quyền. Không bắt buộc: thiếu thì dùng tên controller.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PermissionModuleAttribute(string displayName, string group, int sortOrder) : Attribute
{
    public string DisplayName { get; } = displayName;
    public string Group { get; } = group;
    public int SortOrder { get; } = sortOrder;
}

/// <summary>
/// Tên hiển thị của một action trên ma trận phân quyền (ghi đè tên mặc định như "Xem danh sách").
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PermissionActionAttribute(string displayName) : Attribute
{
    public string DisplayName { get; } = displayName;
}

/// <summary>
/// Action chỉ cần đăng nhập, không cần cấp quyền (trang tổng quan, đăng xuất, đổi mật khẩu của chính mình).
/// Mọi action khác không có [AllowAnonymous] đều phải được cấp quyền "Controller.Action".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuthenticatedOnlyAttribute : Attribute;
