using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DemoMVC.Tests;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_retries_transient_failures_and_succeeds()
    {
        var transport = new Mock<IEmailTransport>();
        var attempts = 0;
        transport.Setup(sender => sender.SendAsync(
                It.IsAny<EmailSettings>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<EmailSettings, string, string, string, CancellationToken>((_, _, _, _, _) =>
            {
                attempts++;
                return attempts < 3
                    ? Task.FromException(new TransientEmailException("temporary", new IOException("test")))
                    : Task.CompletedTask;
            });
        var sender = new SmtpEmailSender(
            transport.Object,
            Options.Create(EmailSettingsValidatorTests.ValidSettings()),
            NullLogger<SmtpEmailSender>.Instance);

        await sender.SendAsync("recipient@example.test", "subject", "<p>body</p>");

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task SendAsync_stops_after_maximum_transient_attempts()
    {
        var transport = new Mock<IEmailTransport>();
        transport.Setup(sender => sender.SendAsync(
                It.IsAny<EmailSettings>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransientEmailException("temporary", new IOException("test")));
        var sender = new SmtpEmailSender(
            transport.Object,
            Options.Create(EmailSettingsValidatorTests.ValidSettings()),
            NullLogger<SmtpEmailSender>.Instance);

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync("recipient@example.test", "subject", "<p>body</p>"));

        Assert.Equal("The email provider could not deliver the message after the configured retry attempts.", exception.Message);
        transport.Verify(smtp => smtp.SendAsync(
            It.IsAny<EmailSettings>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task SendAsync_does_not_retry_permanent_failures()
    {
        var transport = new Mock<IEmailTransport>();
        transport.Setup(sender => sender.SendAsync(
                It.IsAny<EmailSettings>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("permanent", new InvalidOperationException()));
        var sender = new SmtpEmailSender(
            transport.Object,
            Options.Create(EmailSettingsValidatorTests.ValidSettings()),
            NullLogger<SmtpEmailSender>.Instance);

        await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync("recipient@example.test", "subject", "<p>body</p>"));
        transport.Verify(smtp => smtp.SendAsync(
            It.IsAny<EmailSettings>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
