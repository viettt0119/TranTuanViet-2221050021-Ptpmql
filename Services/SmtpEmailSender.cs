using Microsoft.Extensions.Options;
using DemoMVC.Models;

namespace DemoMVC.Services;

public sealed class SmtpEmailSender(
    IEmailTransport transport,
    IOptions<EmailSettings> emailOptions,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private const int MaxAttempts = 3;

    public async Task SendAsync(
        string email,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken = default)
    {
        var settings = emailOptions.Value;
        Exception? lastTransientError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await transport.SendAsync(settings, email, subject, htmlMessage, cancellationToken);
                return;
            }
            catch (TransientEmailException ex) when (attempt < MaxAttempts)
            {
                lastTransientError = ex;
                logger.LogWarning(
                    "Transient SMTP failure on attempt {Attempt} of {MaxAttempts} ({ExceptionType}).",
                    attempt,
                    MaxAttempts,
                    ex.GetType().Name);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
            catch (TransientEmailException ex)
            {
                lastTransientError = ex;
            }
        }

        throw new EmailDeliveryException(
            "The email provider could not deliver the message after the configured retry attempts.",
            lastTransientError ?? new InvalidOperationException("The SMTP retry loop ended unexpectedly."));
    }
}
