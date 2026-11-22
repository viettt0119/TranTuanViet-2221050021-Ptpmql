namespace DemoMVC.Services;

public sealed record PermissionMetadata(
    string Key,
    string Module,
    string ModuleDisplayName,
    string ActionDisplayName,
    string Group,
    int SortOrder);

public static class Permissions
{
    public static class Student
    {
        public const string View = "Student.View";
        public const string Create = "Student.Create";
        public const string Edit = "Student.Edit";
        public const string Delete = "Student.Delete";
        public const string Export = "Student.Export";
    }

    public static class Department
    {
        public const string View = "Department.View";
        public const string Create = "Department.Create";
        public const string Edit = "Department.Edit";
        public const string Delete = "Department.Delete";
    }

    public static class Employee
    {
        public const string View = "Employee.View";
        public const string Create = "Employee.Create";
        public const string Edit = "Employee.Edit";
        public const string Delete = "Employee.Delete";
    }

    public static class Product
    {
        public const string View = "Product.View";
        public const string Create = "Product.Create";
        public const string Edit = "Product.Edit";
        public const string Delete = "Product.Delete";
    }

    public static class ExcelImport
    {
        public const string View = "ExcelImport.View";
        public const string Import = "ExcelImport.Import";
    }

    public static class User
    {
        public const string View = "User.View";
        public const string Edit = "User.Edit";
    }

    public static class Role
    {
        public const string View = "Role.View";
        public const string Create = "Role.Create";
        public const string Edit = "Role.Edit";
        public const string Delete = "Role.Delete";
    }

    public static IReadOnlyList<PermissionMetadata> All()
    {
        var permissions = new List<PermissionMetadata>();
        AddModule(permissions, "Student", "Sinh viên", "Đào tạo", 10,
            ("View", "Xem"), ("Create", "Thêm"), ("Edit", "Sửa"), ("Delete", "Xoá"), ("Export", "Xuất dữ liệu"));
        AddModule(permissions, "Department", "Khoa", "Đào tạo", 20,
            ("View", "Xem"), ("Create", "Thêm"), ("Edit", "Sửa"), ("Delete", "Xoá"));
        AddModule(permissions, "Employee", "Nhân viên", "Nhân sự", 30,
            ("View", "Xem"), ("Create", "Thêm"), ("Edit", "Sửa"), ("Delete", "Xoá"));
        AddModule(permissions, "Product", "Sản phẩm", "Kinh doanh", 40,
            ("View", "Xem"), ("Create", "Thêm"), ("Edit", "Sửa"), ("Delete", "Xoá"));
        AddModule(permissions, "ExcelImport", "Nhập Excel", "Hệ thống", 50,
            ("View", "Xem"), ("Import", "Nhập dữ liệu"));
        AddModule(permissions, "User", "Người dùng", "Hệ thống", 60,
            ("View", "Xem"), ("Edit", "Sửa"));
        AddModule(permissions, "Role", "Vai trò", "Hệ thống", 70,
            ("View", "Xem"), ("Create", "Thêm"), ("Edit", "Sửa"), ("Delete", "Xoá"));
        return permissions;
    }

    private static void AddModule(
        ICollection<PermissionMetadata> target,
        string module,
        string moduleDisplayName,
        string group,
        int sortOrder,
        params (string Action, string DisplayName)[] actions)
    {
        foreach (var action in actions)
        {
            target.Add(new PermissionMetadata(
                $"{module}.{action.Action}",
                module,
                moduleDisplayName,
                action.DisplayName,
                group,
                sortOrder));
        }
    }
}
