using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace DemoMVC.Tests;

public class PermissionCatalogServiceTests
{
    [Fact]
    public async Task EnsureDefaultUserPermissionsAsync_grants_core_modules_once_and_preserves_existing_denials()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userRole = new IdentityRole { Id = "user-role", Name = "User", NormalizedName = "USER" };
        var homePermission = Permission("Home.Index", "Home", "Index");
        var studentIndexPermission = Permission("Student.Index", "Student", "Index");
        var studentEditPermission = Permission("Student.Edit", "Student", "Edit");
        var logoutPermission = Permission("Account.Logout", "Account", "Logout");
        var adminPermission = Permission("Role.Index", "Role", "Index");
        context.Roles.Add(userRole);
        context.Users.Add(new ApplicationUser
        {
            Id = "test-user",
            UserName = "user@example.test",
            NormalizedUserName = "USER@EXAMPLE.TEST",
            Email = "user@example.test",
            NormalizedEmail = "USER@EXAMPLE.TEST",
            SecurityStamp = "test-security-stamp"
        });
        context.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = "test-user",
            RoleId = userRole.Id
        });
        context.ApplicationPermissions.AddRange(
            homePermission,
            studentIndexPermission,
            studentEditPermission,
            logoutPermission,
            adminPermission);
        context.ApplicationRolePermissions.Add(new ApplicationRolePermission
        {
            RoleId = userRole.Id,
            Permission = studentIndexPermission,
            IsGranted = false
        });
        await context.SaveChangesAsync();

        var service = new PermissionCatalogService(context);
        await service.EnsureDefaultUserPermissionsAsync();
        await service.EnsureDefaultUserPermissionsAsync();

        var assignments = await context.ApplicationRolePermissions
            .Where(item => item.RoleId == userRole.Id)
            .ToDictionaryAsync(item => item.PermissionId, item => item.IsGranted);

        Assert.False(assignments[studentIndexPermission.Id]);
        Assert.True(assignments[homePermission.Id]);
        Assert.True(assignments[studentEditPermission.Id]);
        Assert.DoesNotContain(adminPermission.Id, assignments.Keys);
        Assert.Equal(4, assignments.Count);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test-user")],
            authenticationType: "test"));
        Assert.True(await service.HasAccessAsync(user, "Home.Index"));
        Assert.True(await service.HasAccessAsync(user, "Student.Edit"));
        Assert.True(await service.HasAccessAsync(user, "Account.Logout"));
        Assert.False(await service.HasAccessAsync(user, "Student.Index"));
        Assert.False(await service.HasAccessAsync(user, "Role.Index"));
    }

    private static ApplicationPermission Permission(string key, string controller, string action) => new()
    {
        Key = key,
        Controller = controller,
        Action = action,
        Area = string.Empty,
        Description = key
    };
}
