using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DemoMVC.Services;

public sealed class PermissionAuthorizationFilter(
    PermissionCatalogService permissionCatalogService) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context == null)
            return;

        var endpointMetadata = context.ActionDescriptor.EndpointMetadata;
        if (endpointMetadata.Any(item => item is IAllowAnonymous))
            return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var controllerAction = context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
        var methodPermission = controllerAction?.MethodInfo.GetCustomAttribute<RequirePermissionAttribute>();
        var classPermission = controllerAction?.ControllerTypeInfo.GetCustomAttribute<RequirePermissionAttribute>();
        var permission = methodPermission ?? classPermission;
        if (permission is null)
            return;

        if (context.HttpContext.User.IsInRole("Admin"))
            return;

        if (!await permissionCatalogService.HasAccessAsync(context.HttpContext.User, permission.Key))
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

    public static bool IsAjaxRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Requested-With", out var value))
            return value.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        return request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }
}
