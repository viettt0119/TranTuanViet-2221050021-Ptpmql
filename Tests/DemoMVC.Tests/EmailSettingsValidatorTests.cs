using DemoMVC.Models;
using Xunit;

namespace DemoMVC.Tests;

public class EmailSettingsValidatorTests
{
    [Fact]
    public void Validate_accepts_complete_https_settings()
    {
        var result = new EmailSettingsValidator().Validate(null, ValidSettings());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("https://user:pass@example.test")]
    [InlineData("https://example.test/?token=unsafe")]
    public void Validate_rejects_unsafe_public_base_urls(string publicBaseUrl)
    {
        var settings = ValidSettings();
        settings.PublicBaseUrl = publicBaseUrl;

        var result = new EmailSettingsValidator().Validate(null, settings);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_reports_missing_smtp_configuration()
    {
        var result = new EmailSettingsValidator().Validate(null, new EmailSettings());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Email:Host", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("Email:Password", StringComparison.Ordinal));
    }

    internal static EmailSettings ValidSettings() => new()
    {
        Host = "smtp.example.test",
        Port = 587,
        UseStartTls = true,
        UserName = "mailer@example.test",
        Password = "test-only-secret",
        SenderEmail = "noreply@example.test",
        SenderName = "DemoMVC",
        PublicBaseUrl = "https://example.test"
    };
}
