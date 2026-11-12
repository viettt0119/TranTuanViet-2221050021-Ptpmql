using Microsoft.AspNetCore.Authorization;

namespace DemoMVC.Services;

public sealed class PermissionAuthorizationRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
