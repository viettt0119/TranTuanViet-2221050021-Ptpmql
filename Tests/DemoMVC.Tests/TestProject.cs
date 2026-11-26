namespace DemoMVC.Tests;

internal static class TestProject
{
    /// <summary>Thư mục gốc repository (chứa DemoMVC.csproj), dùng cho kiểm thử đọc mã nguồn.</summary>
    public static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DemoMVC.csproj")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Không tìm thấy thư mục chứa DemoMVC.csproj.");
    }
}
