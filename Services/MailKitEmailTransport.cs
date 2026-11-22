using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using DemoMVC.Models;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DemoMVC.Services;

public sealed partial class MailKitEmailTransport : IEmailTransport
{
    private static readonly TimeSpan SmtpTimeout = TimeSpan.FromSeconds(20);

    public async Task SendAsync(
        EmailSettings settings,
        string email,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SenderName.Trim(), settings.EffectiveSenderEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new BodyBuilder
            {
                HtmlBody = htmlMessage,
                // Phần văn bản thuần giúp email ít bị Gmail đánh dấu spam.
                TextBody = ToPlainText(htmlMessage)
            }.ToMessageBody();

            using var client = new SmtpClient { Timeout = (int)SmtpTimeout.TotalMilliseconds };
            await client.ConnectAsync(settings.Host.Trim(), settings.Port, ResolveSocketOptions(settings), cancellationToken);
            await client.AuthenticateAsync(settings.UserName.Trim(), settings.EffectivePassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SmtpCommandException ex) when (IsTransientStatus(ex.StatusCode))
        {
            throw new TransientEmailException("The SMTP server reported a temporary failure.", ex);
        }
        catch (Exception ex) when (ex is IOException or SmtpProtocolException)
        {
            throw new TransientEmailException("The SMTP connection failed temporarily.", ex);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            throw new EmailDeliveryException(
                "SMTP authentication failed. For Gmail, use a 16-character App Password and the full Gmail address as Email:UserName.",
                ex);
        }
        catch (SocketException ex)
        {
            // Sai host/port hoặc hosting chặn cổng SMTP: thử lại không giúp được gì.
            throw new EmailDeliveryException("Could not connect to the SMTP server. Check Email:Host and Email:Port.", ex);
        }
        catch (Exception ex) when (ex is not TransientEmailException and not EmailDeliveryException)
        {
            throw new EmailDeliveryException($"The SMTP server rejected the email ({ex.GetType().Name}).", ex);
        }
    }

    public static SecureSocketOptions ResolveSocketOptions(EmailSettings settings)
    {
        // 465 dùng SSL ngay khi kết nối; 587 (Gmail) dùng STARTTLS.
        if (settings.Port == 465)
            return SecureSocketOptions.SslOnConnect;

        return settings.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.StartTlsWhenAvailable;
    }

    public static string ToPlainText(string html)
    {
        var withLinks = AnchorRegex().Replace(html, match => $"{match.Groups["text"].Value}: {match.Groups["href"].Value}");
        var withLineBreaks = BlockEndRegex().Replace(withLinks, Environment.NewLine);
        var withoutTags = TagRegex().Replace(withLineBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var lines = decoded
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsTransientStatus(SmtpStatusCode statusCode) =>
        statusCode is SmtpStatusCode.ServiceNotAvailable
            or SmtpStatusCode.MailboxBusy
            or SmtpStatusCode.ErrorInProcessing;

    [GeneratedRegex("<a\\s[^>]*href=\"(?<href>[^\"]*)\"[^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex("</(p|div|h[1-6]|li|tr)>|<br\\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
