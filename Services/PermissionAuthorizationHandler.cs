using Microsoft.AspNetCore.Authorization;

namespace DemoMVC.Services;

public sealed class PermissionAuthorizationHandler(PermissionCatalogService permissionCatalogService)
    : AuthorizationHandler<PermissionAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionAuthorizationRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
            return;

        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return;
        }

        if (await permissionCatalogService.HasAccessAsync(context.User, requirement.PermissionKey))
        {
            context.Succeed(requirement);
        }
    }
}
