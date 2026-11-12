using System.IO;
using System.Net.Sockets;
using DemoMVC.Models;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DemoMVC.Services;

public sealed class MailKitEmailTransport : IEmailTransport
{
    public async Task SendAsync(
        EmailSettings settings,
        string email,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient();
            var secureSocketOptions = settings.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;
            await client.ConnectAsync(settings.Host, settings.Port, secureSocketOptions, cancellationToken);
            await client.AuthenticateAsync(settings.UserName, settings.Password, cancellationToken);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (SmtpCommandException ex) when (IsTransientStatus(ex.StatusCode))
        {
            throw new TransientEmailException("The SMTP server reported a temporary failure.", ex);
        }
        catch (Exception ex) when (ex is IOException or SocketException or SmtpProtocolException)
        {
            throw new TransientEmailException("The SMTP connection failed temporarily.", ex);
        }
        catch (SmtpCommandException ex)
        {
            throw new EmailDeliveryException("The SMTP server rejected the email.", ex);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            throw new EmailDeliveryException("SMTP authentication failed.", ex);
        }
    }

    private static bool IsTransientStatus(SmtpStatusCode statusCode) =>
        statusCode is SmtpStatusCode.ServiceNotAvailable
            or SmtpStatusCode.MailboxBusy
            or SmtpStatusCode.ErrorInProcessing;
}
