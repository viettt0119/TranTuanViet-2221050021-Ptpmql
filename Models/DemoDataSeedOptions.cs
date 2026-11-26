namespace DemoMVC.Models;

public sealed class DemoDataSeedOptions
{
    public const string SectionName = "DemoDataSeed";

    // Không cấu hình: chỉ tạo dữ liệu minh họa khi chạy Development.
    public bool? Enabled { get; set; }
}
