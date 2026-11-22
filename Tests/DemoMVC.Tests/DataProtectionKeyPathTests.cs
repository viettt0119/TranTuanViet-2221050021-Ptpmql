using DemoMVC.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace DemoMVC.Tests;

public class DataProtectionKeyPathTests
{
    [Fact]
    public void Resolve_uses_monsterasp_private_folder_next_to_site_root()
    {
        var root = Directory.CreateTempSubdirectory("demomvc-dp-");
        try
        {
            var siteRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "wwwroot"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "private"));

            var path = DataProtectionKeyPath.Resolve(EmptyConfiguration(), Environment(siteRoot.FullName));

            Assert.Equal(Path.Combine(root.FullName, "private", "keys"), path);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Resolve_returns_null_when_no_private_folder_exists()
    {
        var root = Directory.CreateTempSubdirectory("demomvc-dp-");
        try
        {
            var siteRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "wwwroot"));

            Assert.Null(DataProtectionKeyPath.Resolve(EmptyConfiguration(), Environment(siteRoot.FullName)));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Resolve_prefers_configured_path_relative_to_content_root()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "demomvc-content");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DataProtectionKeyPath.ConfigurationKey] = "App_Data/keys" })
            .Build();

        var path = DataProtectionKeyPath.Resolve(configuration, Environment(contentRoot));

        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "keys")), path);
    }

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    private static IHostEnvironment Environment(string contentRoot) =>
        Mock.Of<IHostEnvironment>(environment => environment.ContentRootPath == contentRoot);
}
