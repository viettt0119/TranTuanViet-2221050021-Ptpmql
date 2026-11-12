using System.Reflection;
using System.Security.Claims;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services;

public sealed class PermissionCatalogService(ApplicationDbContext dbContext)
{
    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var permissions = DiscoverPermissions();
        var existing = await dbContext.ApplicationPermissions
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Key, cancellationToken);

        foreach (var permission in permissions)
        {
            if (existing.TryGetValue(permission.Key, out var current))
            {
                if (current.Controller != permission.Controller ||
                    current.Action != permission.Action ||
                    current.Area != permission.Area ||
                    current.Description != permission.Description ||
                    current.IsAnonymous != permission.IsAnonymous ||
                    current.IsAdminOnly != permission.IsAdminOnly)
                {
                    current.Controller = permission.Controller;
                    current.Action = permission.Action;
                    current.Area = permission.Area;
                    current.Description = permission.Description;
                    current.IsAnonymous = permission.IsAnonymous;
                    current.IsAdminOnly = permission.IsAdminOnly;
                    dbContext.ApplicationPermissions.Update(current);
                }
            }
            else
            {
                dbContext.ApplicationPermissions.Add(permission);
            }
        }

        var keys = permissions.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = await dbContext.ApplicationPermissions
            .Where(x => !keys.Contains(x.Key))
            .ToListAsync(cancellationToken);

        foreach (var item in stale)
        {
            var roleEntries = await dbContext.ApplicationRolePermissions
                .Where(x => x.PermissionId == item.Id)
                .ToListAsync(cancellationToken);
            if (roleEntries.Count > 0)
            {
                dbContext.ApplicationRolePermissions.RemoveRange(roleEntries);
            }

            dbContext.ApplicationPermissions.Remove(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string permissionKey)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var roleIds = await dbContext.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync();

        if (roleIds.Count == 0)
            return false;

        var permission = await dbContext.ApplicationPermissions
            .FirstOrDefaultAsync(x => x.Key == permissionKey, CancellationToken.None);

        if (permission is null)
            return false;

        var adminRoleId = await dbContext.Roles
            .Where(x => x.Name == "Admin")
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(adminRoleId) && roleIds.Contains(adminRoleId))
            return true;

        var granted = await dbContext.ApplicationRolePermissions
            .AnyAsync(x => x.PermissionId == permission.Id && x.IsGranted && roleIds.Contains(x.RoleId), CancellationToken.None);

        return granted;
    }

    private static IReadOnlyList<ApplicationPermission> DiscoverPermissions()
    {
        var types = typeof(Program).Assembly
            .DefinedTypes
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToList();

        var results = new List<ApplicationPermission>();
        foreach (var type in types)
        {
            var controllerName = type.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? type.Name[..^"Controller".Length]
                : type.Name;

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.GetCustomAttribute<NonActionAttribute>() is not null)
                    continue;

                var hasExplicitHttpVerb = method.GetCustomAttribute<HttpGetAttribute>() is not null ||
                    method.GetCustomAttribute<HttpPostAttribute>() is not null ||
                    method.GetCustomAttribute<HttpPutAttribute>() is not null ||
                    method.GetCustomAttribute<HttpDeleteAttribute>() is not null ||
                    method.GetCustomAttribute<HttpPatchAttribute>() is not null ||
                    method.GetCustomAttribute<AcceptVerbsAttribute>() is not null;

                // MVC treats action methods without an explicit verb as GET requests. Include
                // those actions so the permission catalog matches the actual app routes.
                if (!hasExplicitHttpVerb && method.Name == "Dispose")
                    continue;

                var isAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
                    type.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

                results.Add(new ApplicationPermission
                {
                    Key = BuildPermissionKey(controllerName, method.Name),
                    Controller = controllerName,
                    Action = method.Name,
                    Area = string.Empty,
                    Description = $"{controllerName}.{method.Name}",
                    IsAnonymous = isAnonymous,
                    IsAdminOnly = false
                });
            }
        }

        return results.DistinctBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string BuildPermissionKey(string controller, string action) => $"{controller}.{action}";
}
