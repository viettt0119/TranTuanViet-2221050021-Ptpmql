using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace DemoMVC.Models;

public sealed class EmailSettingsValidator : IValidateOptions<EmailSettings>
{
    public ValidateOptionsResult Validate(string? name, EmailSettings options)
    {
        var errors = new List<string>();
        var host = options.Host?.Trim();
        var userName = options.UserName?.Trim();
        var password = options.Password?.Trim();
        var senderEmail = options.SenderEmail?.Trim();
        var senderName = options.SenderName?.Trim();
        var publicBaseUrl = options.PublicBaseUrl?.Trim();

        if (string.IsNullOrWhiteSpace(host))
            errors.Add("Email:Host is required.");
        if (options.Port is < 1 or > 65535)
            errors.Add("Email:Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(userName))
            errors.Add("Email:UserName is required.");
        if (string.IsNullOrWhiteSpace(password))
            errors.Add("Email:Password is required.");
        if (!IsValidEmail(senderEmail))
            errors.Add("Email:SenderEmail must be a valid email address.");
        if (string.IsNullOrWhiteSpace(senderName))
            errors.Add("Email:SenderName is required.");
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            errors.Add("Email:PublicBaseUrl must be an absolute HTTPS URL without credentials, query, or fragment.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsValidEmail(string? email)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(email) &&
                string.Equals(new MailAddress(email).Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
