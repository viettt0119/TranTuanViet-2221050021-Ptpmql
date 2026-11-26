using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Services;

public sealed record PermissionMetadata(
    string Key,
    string Controller,
    string Action,
    string ModuleDisplayName,
    string ActionDisplayName,
    string Group,
    int SortOrder);

/// <summary>
/// Tự phát hiện mọi Controller/Action bằng reflection và sinh khoá quyền "Controller.Action".
/// GET và POST cùng tên action (vd. Create) dùng chung một quyền; [ActionName] được tôn trọng
/// (DeleteConfirmed có [ActionName("Delete")] thuộc quyền "X.Delete").
/// </summary>
public static class Permissions
{
    private static readonly Dictionary<string, string> DefaultActionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Index"] = "Xem danh sách",
        ["Details"] = "Xem chi tiết",
        ["Create"] = "Thêm",
        ["Edit"] = "Sửa",
        ["Delete"] = "Xoá",
        ["Export"] = "Xuất Excel"
    };

    private static readonly Lazy<IReadOnlyList<PermissionMetadata>> Cached =
        new(() => Discover(typeof(Permissions).Assembly));

    public static string Key(string controller, string action) => $"{controller}.{action}";

    /// <summary>Danh sách quyền của toàn bộ ứng dụng (tính một lần).</summary>
    public static IReadOnlyList<PermissionMetadata> All() => Cached.Value;

    public static IReadOnlyList<PermissionMetadata> Discover(Assembly assembly)
    {
        var result = new Dictionary<string, PermissionMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var controllerType in GetControllerTypes(assembly))
        {
            var controllerName = GetControllerName(controllerType);
            var module = controllerType.GetCustomAttribute<PermissionModuleAttribute>();

            foreach (var method in GetActionMethods(controllerType))
            {
                if (!RequiresPermission(controllerType, method))
                    continue;

                var actionName = GetActionName(method);
                var key = Key(controllerName, actionName);
                var displayName = method.GetCustomAttribute<PermissionActionAttribute>()?.DisplayName;

                if (result.TryGetValue(key, out var existing))
                {
                    // Action GET/POST cùng tên: lấy tên hiển thị tuỳ biến nếu một trong hai có khai báo.
                    if (displayName is not null && existing.ActionDisplayName == DefaultActionName(actionName))
                        result[key] = existing with { ActionDisplayName = displayName };
                    continue;
                }

                result[key] = new PermissionMetadata(
                    key,
                    controllerName,
                    actionName,
                    module?.DisplayName ?? controllerName,
                    displayName ?? DefaultActionName(actionName),
                    module?.Group ?? "Khác",
                    module?.SortOrder ?? 1000);
            }
        }

        return result.Values
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Controller, StringComparer.Ordinal)
            .ThenBy(x => x.Action, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// false nếu action cho khách ([AllowAnonymous]), chỉ cần đăng nhập ([AuthenticatedOnly])
    /// hoặc chỉ dành cho Admin ([AdminOnly]).
    /// </summary>
    public static bool RequiresPermission(Type controllerType, MethodInfo method) =>
        !IsAnonymous(controllerType, method) &&
        !IsAuthenticatedOnly(controllerType, method) &&
        !IsAdminOnly(controllerType, method);

    public static bool IsAdminOnly(Type controllerType, MethodInfo method) =>
        method.IsDefined(typeof(AdminOnlyAttribute), inherit: true) ||
        controllerType.IsDefined(typeof(AdminOnlyAttribute), inherit: true);

    public static bool IsAnonymous(Type controllerType, MethodInfo method) =>
        method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) ||
        controllerType.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);

    public static bool IsAuthenticatedOnly(Type controllerType, MethodInfo method) =>
        method.IsDefined(typeof(AuthenticatedOnlyAttribute), inherit: true) ||
        controllerType.IsDefined(typeof(AuthenticatedOnlyAttribute), inherit: true);

    public static IEnumerable<Type> GetControllerTypes(Assembly assembly) =>
        assembly.GetTypes().Where(type =>
            type is { IsClass: true, IsAbstract: false, IsPublic: true, ContainsGenericParameters: false } &&
            typeof(ControllerBase).IsAssignableFrom(type) &&
            !type.IsDefined(typeof(NonControllerAttribute), inherit: true));

    public static IEnumerable<MethodInfo> GetActionMethods(Type controllerType) =>
        controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method =>
                !method.IsSpecialName &&
                method.DeclaringType is not null &&
                typeof(ControllerBase).IsAssignableFrom(method.DeclaringType) &&
                method.DeclaringType != typeof(Controller) &&
                method.DeclaringType != typeof(ControllerBase) &&
                !method.IsDefined(typeof(NonActionAttribute), inherit: true));

    public static string GetControllerName(Type controllerType) =>
        controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerType.Name[..^"Controller".Length]
            : controllerType.Name;

    public static string GetActionName(MethodInfo method) =>
        method.GetCustomAttribute<ActionNameAttribute>()?.Name ?? method.Name;

    private static string DefaultActionName(string action) =>
        DefaultActionNames.TryGetValue(action, out var name) ? name : action;
}
