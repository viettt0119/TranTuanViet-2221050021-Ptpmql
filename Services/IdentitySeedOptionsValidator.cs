using System.Net.Mail;
using DemoMVC.Models;
using Microsoft.Extensions.Options;

namespace DemoMVC.Services;

public sealed class IdentitySeedOptionsValidator : IValidateOptions<IdentitySeedOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentitySeedOptions options)
    {
        var errors = new List<string>();
        var email = options.Email?.Trim();
        var password = options.Password?.Trim();
        var fullName = options.FullName?.Trim();

        if (!IsValidEmail(email))
            errors.Add("AdminSeed:Email must be a valid email address.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            errors.Add("AdminSeed:Password is required and must be at least 12 characters long.");
        if (string.IsNullOrWhiteSpace(fullName))
            errors.Add("AdminSeed:FullName is required.");

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
