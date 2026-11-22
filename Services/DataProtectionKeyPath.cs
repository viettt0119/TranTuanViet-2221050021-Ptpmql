namespace DemoMVC.Services;

public static class DataProtectionKeyPath
{
    public const string ConfigurationKey = "DataProtection:KeysPath";

    // Trả về thư mục lưu Data Protection key, hoặc null để dùng vị trí mặc định của hệ điều hành (máy local).
    public static string? Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration[ConfigurationKey];
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath.Trim(), environment.ContentRootPath);

        // MonsterASP: thư mục "private" nằm cạnh wwwroot, ứng dụng ghi được và các lần deploy không đụng tới.
        var privateFolder = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "private"));
        return Directory.Exists(privateFolder)
            ? Path.Combine(privateFolder, "keys")
            : null;
    }
}
