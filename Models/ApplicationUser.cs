using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace DemoMVC.Models;

public sealed class ApplicationUser : IdentityUser
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
