namespace DemoMVC.Data;

/// <summary>Danh mục khoa của Trường Đại học Mỏ - Địa chất dùng để khởi tạo dữ liệu.</summary>
public static class UniversityDepartments
{
    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        ("MO", "Mỏ"),
        ("DK", "Dầu khí và Năng lượng"),
        ("DC", "Khoa học và Kỹ thuật Địa chất"),
        ("TD", "Trắc địa - Bản đồ và Quản lý đất đai"),
        ("CNTT", "Công nghệ thông tin"),
        ("CD", "Cơ - Điện"),
        ("KT", "Kinh tế - Quản trị kinh doanh"),
        ("XD", "Xây dựng"),
        ("MT", "Môi trường"),
        ("KHCB", "Khoa học cơ bản"),
        ("LLCT", "Lý luận chính trị")
    ];

    /// <summary>Tên do bản seed cũ đặt; chỉ những bản ghi còn giữ tên này mới được đổi sang tên đúng.</summary>
    public static readonly IReadOnlyDictionary<string, string> LegacySeedNames = new Dictionary<string, string>
    {
        ["MO"] = "Khoa học mỏ",
        ["DC"] = "Địa chất",
        ["KT"] = "Kinh tế"
    };
}
