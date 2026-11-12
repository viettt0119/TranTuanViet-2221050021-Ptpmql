using Microsoft.AspNetCore.Identity;

namespace DemoMVC.Models;

public sealed class ApplicationRolePermission
{
    public int Id { get; set; }

    public string RoleId { get; set; } = string.Empty;

    public int PermissionId { get; set; }

    public bool IsGranted { get; set; } = true;

    public IdentityRole Role { get; set; } = null!;

    public ApplicationPermission Permission { get; set; } = null!;
}
