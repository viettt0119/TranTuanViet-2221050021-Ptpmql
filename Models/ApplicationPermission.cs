namespace DemoMVC.Models;

public sealed class ApplicationPermission
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Controller { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string ModuleDisplayName { get; set; } = string.Empty;

    public string ActionDisplayName { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsAdminOnly { get; set; }

    public ICollection<ApplicationRolePermission> RolePermissions { get; set; } = new List<ApplicationRolePermission>();
}
