using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DemoMVC.Services;

/// <summary>
/// Phân quyền động cho từng action của tất cả controller, mặc định từ chối:
/// - [AllowAnonymous]: ai cũng vào được.
/// - [AdminOnly]: chỉ role Admin (bài thực hành Lec 1–6), không nằm trên ma trận quyền.
/// - [AuthenticatedOnly]: chỉ cần đăng nhập.
/// - Còn lại: người dùng phải có quyền "Controller.Action" qua một role của mình (Admin luôn được phép).
/// </summary>
public sealed class PermissionAuthorizationFilter(
    PermissionCatalogService permissionCatalogService) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ActionDescriptor.EndpointMetadata.Any(item => item is IAllowAnonymous))
            return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (context.ActionDescriptor is not ControllerActionDescriptor action)
        {
            // Không phải action MVC (không xảy ra với filter MVC); từ chối cho an toàn.
            Deny(context);
            return;
        }

        // Kiểm tra [AdminOnly] trước [AuthenticatedOnly]: action gắn cả hai vẫn chỉ cho Admin.
        if (Permissions.IsAdminOnly(action.ControllerTypeInfo, action.MethodInfo))
        {
            if (!context.HttpContext.User.IsInRole("Admin"))
                Deny(context);
            return;
        }

        if (Permissions.IsAuthenticatedOnly(action.ControllerTypeInfo, action.MethodInfo))
            return;

        if (context.HttpContext.User.IsInRole("Admin"))
            return;

        var key = Permissions.Key(action.ControllerName, action.ActionName);
        if (!await permissionCatalogService.HasAccessAsync(context.HttpContext.User, key))
            Deny(context);
    }

    private static void Deny(AuthorizationFilterContext context)
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

    public static bool IsAjaxRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Requested-With", out var value))
            return value.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        return request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }
}
