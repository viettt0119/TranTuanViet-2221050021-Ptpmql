using DemoMVC.Models;
using DemoMVC.Services;
using Xunit;

namespace DemoMVC.Tests;

public class IdentitySeedOptionsValidatorTests
{
    [Fact]
    public void Validate_accepts_configured_admin_without_revealing_password()
    {
        var options = new IdentitySeedOptions
        {
            Email = "admin@example.test",
            Password = "test-only-secret",
            FullName = "Test Admin"
        };

        var result = new IdentitySeedOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(options.Password, string.Join(";", result.Failures ?? []));
    }

    [Fact]
    public void Validate_requires_email_password_and_full_name()
    {
        var result = new IdentitySeedOptionsValidator().Validate(null, new IdentitySeedOptions());

        Assert.False(result.Succeeded);
        var errors = string.Join(";", result.Failures ?? []);
        Assert.Contains("AdminSeed:Email", errors, StringComparison.Ordinal);
        Assert.Contains("AdminSeed:Password", errors, StringComparison.Ordinal);
        Assert.Contains("AdminSeed:FullName", errors, StringComparison.Ordinal);
    }
}
