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
        var settings = GetValidatedSettings();
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
                    ex.InnerException?.GetType().Name ?? ex.GetType().Name);
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

    private EmailSettings GetValidatedSettings()
    {
        try
        {
            return emailOptions.Value;
        }
        catch (OptionsValidationException ex)
        {
            // Thiếu cấu hình SMTP không được làm sập request: báo lỗi gửi email để controller xử lý thân thiện.
            logger.LogError(
                "SMTP settings are incomplete: {Failures}",
                string.Join(" ", ex.Failures));
            throw new EmailDeliveryException("SMTP settings are incomplete.", ex);
        }
    }
}
