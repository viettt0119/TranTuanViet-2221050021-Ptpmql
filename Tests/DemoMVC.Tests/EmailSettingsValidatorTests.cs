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
    [InlineData("ftp://example.test")]
    [InlineData("/relative/path")]
    [InlineData("https://user:pass@example.test")]
    [InlineData("https://example.test/?token=unsafe")]
    [InlineData("https://example.test/#fragment")]
    public void Validate_rejects_unsafe_public_base_urls(string publicBaseUrl)
    {
        var settings = ValidSettings();
        settings.PublicBaseUrl = publicBaseUrl;

        var result = new EmailSettingsValidator().Validate(null, settings);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://lecture0119.runasp.net")]
    [InlineData("https://lecture0119.runasp.net/")]
    public void Validate_accepts_empty_or_http_public_base_url(string publicBaseUrl)
    {
        var settings = ValidSettings();
        settings.PublicBaseUrl = publicBaseUrl;

        var result = new EmailSettingsValidator().Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_accepts_gmail_settings_without_explicit_sender()
    {
        var settings = new EmailSettings
        {
            UserName = "student.demo@gmail.com",
            Password = "abcd efgh ijkl mnop"
        };

        var result = new EmailSettingsValidator().Validate(null, settings);

        Assert.True(result.Succeeded);
        Assert.Equal("student.demo@gmail.com", settings.EffectiveSenderEmail);
        Assert.Equal("abcdefghijklmnop", settings.EffectivePassword);
    }

    [Fact]
    public void Effective_password_keeps_spaces_for_non_gmail_hosts()
    {
        var settings = ValidSettings();
        settings.Password = "pass with spaces";

        Assert.Equal("pass with spaces", settings.EffectivePassword);
    }

    [Fact]
    public void Validate_reports_missing_smtp_configuration()
    {
        var result = new EmailSettingsValidator().Validate(null, new EmailSettings { Host = "" });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Email:Host", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("Email:UserName", StringComparison.Ordinal));
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
