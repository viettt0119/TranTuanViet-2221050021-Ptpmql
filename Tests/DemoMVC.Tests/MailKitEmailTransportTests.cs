using DemoMVC.Models;
using DemoMVC.Services;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DemoMVC.Tests;

public class MailKitEmailTransportTests
{
    [Theory]
    [InlineData(587, true, SecureSocketOptions.StartTls)]
    [InlineData(465, true, SecureSocketOptions.SslOnConnect)]
    [InlineData(465, false, SecureSocketOptions.SslOnConnect)]
    [InlineData(25, false, SecureSocketOptions.StartTlsWhenAvailable)]
    public void ResolveSocketOptions_matches_port_and_tls_setting(int port, bool useStartTls, SecureSocketOptions expected)
    {
        var settings = new EmailSettings { Port = port, UseStartTls = useStartTls };

        Assert.Equal(expected, MailKitEmailTransport.ResolveSocketOptions(settings));
    }

    [Fact]
    public void ToPlainText_keeps_link_targets_readable()
    {
        var text = MailKitEmailTransport.ToPlainText(
            "<p>Xin chào A,</p><p><a href=\"https://example.test/Account/ConfirmEmail?userId=1&amp;token=x\">Xác thực email</a></p>");

        Assert.Contains("Xin chào A,", text, StringComparison.Ordinal);
        Assert.Contains("Xác thực email: https://example.test/Account/ConfirmEmail?userId=1&token=x", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmtpEmailSender_reports_incomplete_settings_as_delivery_failure()
    {
        var transport = new Mock<IEmailTransport>();
        var options = new OptionsManager<EmailSettings>(new OptionsFactory<EmailSettings>(
            [],
            [],
            [new EmailSettingsValidator()]));
        var sender = new SmtpEmailSender(transport.Object, options, NullLogger<SmtpEmailSender>.Instance);

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync("recipient@example.test", "subject", "<p>body</p>"));

        Assert.IsType<OptionsValidationException>(exception.InnerException);
        transport.Verify(smtp => smtp.SendAsync(
            It.IsAny<EmailSettings>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
