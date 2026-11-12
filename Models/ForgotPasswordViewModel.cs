using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models;

public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
