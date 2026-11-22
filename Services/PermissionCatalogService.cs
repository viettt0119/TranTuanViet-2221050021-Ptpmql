using System.Security.Claims;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DemoMVC.Services;

public sealed class PermissionCatalogService(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    IMemoryCache memoryCache)
{
    private readonly object _invalidateLock = new();
    private CancellationTokenSource _allUsersCacheTokenSource = new();

    public async Task EnsureSystemRolesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        var adminRole = await roleManager.FindByNameAsync("Admin")
            ?? throw new InvalidOperationException("Không thể khởi tạo role Admin.");
        var permissions = await dbContext.ApplicationPermissions.ToListAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            var exists = await dbContext.ApplicationRolePermissions.AnyAsync(
                x => x.RoleId == adminRole.Id && x.PermissionId == permission.Id,
                cancellationToken);
            if (!exists)
            {
                dbContext.ApplicationRolePermissions.Add(new ApplicationRolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.Id,
                    IsGranted = true
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureDefaultUserPermissionsAsync(cancellationToken);
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var metadata = Permissions.All();
        var existing = await dbContext.ApplicationPermissions
            .ToDictionaryAsync(x => x.Key, cancellationToken);

        foreach (var item in metadata)
        {
            if (existing.TryGetValue(item.Key, out var permission))
            {
                permission.Controller = item.Module;
                permission.Action = item.Key[(item.Module.Length + 1)..];
                permission.Description = $"{item.ModuleDisplayName} – {item.ActionDisplayName}";
                permission.Module = item.Module;
                permission.ModuleDisplayName = item.ModuleDisplayName;
                permission.ActionDisplayName = item.ActionDisplayName;
                permission.Group = item.Group;
                permission.SortOrder = item.SortOrder;
                permission.IsAnonymous = false;
                permission.IsAdminOnly = false;
            }
            else
            {
                dbContext.ApplicationPermissions.Add(new ApplicationPermission
                {
                    Key = item.Key,
                    Controller = item.Module,
                    Action = item.Key[(item.Module.Length + 1)..],
                    Area = string.Empty,
                    Description = $"{item.ModuleDisplayName} – {item.ActionDisplayName}",
                    Module = item.Module,
                    ModuleDisplayName = item.ModuleDisplayName,
                    ActionDisplayName = item.ActionDisplayName,
                    Group = item.Group,
                    SortOrder = item.SortOrder
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var validKeys = metadata.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stalePermissions = await dbContext.ApplicationPermissions
            .Where(x => !validKeys.Contains(x.Key))
            .Include(x => x.RolePermissions)
            .ToListAsync(cancellationToken);

        foreach (var stale in stalePermissions)
        {
            dbContext.ApplicationRolePermissions.RemoveRange(stale.RolePermissions);
            dbContext.ApplicationPermissions.Remove(stale);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string permissionKey)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;
        if (user.IsInRole("Admin"))
            return true;

        var keys = await GetGrantedKeysAsync(user);
        return keys.Contains(permissionKey);
    }

    public async Task<HashSet<string>> GetGrantedKeysAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheKey = $"permissions:{userId}";
        if (memoryCache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
            return cached;

        var keys = await dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(dbContext.ApplicationRolePermissions,
                userRole => userRole.RoleId,
                rolePermission => rolePermission.RoleId,
                (_, rolePermission) => rolePermission)
            .Where(rolePermission => rolePermission.IsGranted)
            .Join(dbContext.ApplicationPermissions,
                rolePermission => rolePermission.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Key)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

        var allUsersToken = _allUsersCacheTokenSource.Token;
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
            .AddExpirationToken(new CancellationChangeToken(allUsersToken));
        memoryCache.Set(cacheKey, keys, options);
        return keys;
    }

    public void InvalidateUser(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            memoryCache.Remove($"permissions:{userId}");
    }

    public void InvalidateAll()
    {
        lock (_invalidateLock)
        {
            _allUsersCacheTokenSource.Cancel();
            _allUsersCacheTokenSource.Dispose();
            _allUsersCacheTokenSource = new CancellationTokenSource();
        }
    }

    public async Task EnsureDefaultUserPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var userRole = await roleManager.FindByNameAsync("User");
        if (userRole is null)
            throw new InvalidOperationException("Role User phải được khởi tạo.");

        var hasAnyPermission = await dbContext.ApplicationRolePermissions
            .AnyAsync(x => x.RoleId == userRole.Id, cancellationToken);
        if (hasAnyPermission)
            return;

        var defaultKeys = new[]
        {
            Permissions.Student.View,
            Permissions.Department.View,
            Permissions.Employee.View,
            Permissions.Product.View
        };

        var permissions = await dbContext.ApplicationPermissions
            .Where(x => defaultKeys.Contains(x.Key))
            .ToListAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            dbContext.ApplicationRolePermissions.Add(new ApplicationRolePermission
            {
                RoleId = userRole.Id,
                PermissionId = permission.Id,
                IsGranted = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string BuildPermissionKey(string module, string action) => $"{module}.{action}";
}
