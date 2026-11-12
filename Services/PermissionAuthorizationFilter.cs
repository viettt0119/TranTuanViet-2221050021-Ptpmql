using DemoMVC.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services;

public sealed class PermissionAuthorizationFilter(
    PermissionCatalogService permissionCatalogService,
    ApplicationDbContext dbContext) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context == null)
            return;

        var endpointMetadata = context.ActionDescriptor.EndpointMetadata;
        if (endpointMetadata.Any(item => item is IAllowAnonymous))
            return;

        var controllerName = context.RouteData.Values["controller"] as string;
        var actionName = context.RouteData.Values["action"] as string;
        if (string.IsNullOrWhiteSpace(controllerName) || string.IsNullOrWhiteSpace(actionName))
        {
            var controllerAction = context.ActionDescriptor as ControllerActionDescriptor;
            controllerName = controllerAction?.ControllerName;
            actionName = controllerAction?.ActionName;
        }

        if (string.IsNullOrWhiteSpace(controllerName) || string.IsNullOrWhiteSpace(actionName))
            return;

        var permissionKey = PermissionCatalogService.BuildPermissionKey(controllerName, actionName);
        var permission = await dbContext.ApplicationPermissions.FirstOrDefaultAsync(x => x.Key == permissionKey);

        if (permission is not null && permission.IsAnonymous)
            return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (context.HttpContext.User.IsInRole("Admin"))
            return;

        if (!await permissionCatalogService.HasAccessAsync(context.HttpContext.User, permissionKey))
        {
            if (IsAjaxRequest(context.HttpContext.Request))
            {
                context.Result = new JsonResult(new { success = false, message = "Bạn không có quyền truy cập chức năng này." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }

    private static bool IsAjaxRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Requested-With", out var value))
            return value.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        return request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }
}
