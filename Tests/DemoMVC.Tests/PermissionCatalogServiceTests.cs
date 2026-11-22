using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xunit;

namespace DemoMVC.Tests;

public class PermissionCatalogServiceTests
{
    [Fact]
    public async Task EnsureDefaultUserPermissionsAsync_grants_only_view_permissions_once()
    {
        await using var fixture = await PermissionFixture.CreateAsync();

        var userRole = new IdentityRole { Id = "user-role", Name = "User", NormalizedName = "USER" };
        fixture.Context.Roles.Add(userRole);
        fixture.Context.ApplicationPermissions.AddRange(
            Permission(Permissions.Student.View),
            Permission(Permissions.Department.View),
            Permission(Permissions.Employee.View),
            Permission(Permissions.Product.View),
            Permission(Permissions.Student.Create));
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.EnsureDefaultUserPermissionsAsync();
        await fixture.Service.EnsureDefaultUserPermissionsAsync();

        var assignments = await fixture.Context.ApplicationRolePermissions
            .Where(x => x.RoleId == userRole.Id)
            .Join(fixture.Context.ApplicationPermissions,
                x => x.PermissionId,
                x => x.Id,
                (assignment, permission) => new { permission.Key, assignment.IsGranted })
            .ToListAsync();

        Assert.Equal(4, assignments.Count);
        Assert.All(assignments, item => Assert.True(item.IsGranted));
        Assert.DoesNotContain(assignments, item => item.Key == Permissions.Student.Create);
    }

    [Fact]
    public async Task SyncAsync_creates_module_permissions_and_removes_old_controller_action_permissions()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var oldPermission = Permission("Student.Index");
        var role = new IdentityRole { Id = "old-role", Name = "OldRole", NormalizedName = "OLDROLE" };
        fixture.Context.Roles.Add(role);
        fixture.Context.ApplicationPermissions.Add(oldPermission);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ApplicationRolePermissions.Add(new ApplicationRolePermission
        {
            RoleId = role.Id,
            PermissionId = oldPermission.Id,
            IsGranted = true
        });
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.SyncAsync();

        Assert.DoesNotContain(await fixture.Context.ApplicationPermissions.ToListAsync(),
            item => item.Key == "Student.Index");
        Assert.DoesNotContain(await fixture.Context.ApplicationRolePermissions.ToListAsync(),
            item => item.PermissionId == oldPermission.Id);
        foreach (var metadata in Permissions.All())
        {
            var permission = await fixture.Context.ApplicationPermissions.SingleAsync(x => x.Key == metadata.Key);
            Assert.Equal(metadata.ModuleDisplayName, permission.ModuleDisplayName);
            Assert.Equal(metadata.ActionDisplayName, permission.ActionDisplayName);
            Assert.Equal(metadata.Group, permission.Group);
            Assert.Equal(metadata.SortOrder, permission.SortOrder);
        }
    }

    [Fact]
    public async Task Cache_is_refreshed_after_invalidate_user()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var role = new IdentityRole { Id = "cache-role", Name = "CacheRole", NormalizedName = "CACHEROLE" };
        var permission = Permission(Permissions.Student.View);
        fixture.Context.Roles.Add(role);
        fixture.Context.ApplicationPermissions.Add(permission);
        fixture.Context.Users.Add(new ApplicationUser
        {
            Id = "cache-user",
            UserName = "cache@example.test",
            Email = "cache@example.test",
            EmailConfirmed = true
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = "cache-user",
            RoleId = role.Id
        });
        fixture.Context.ApplicationRolePermissions.Add(new ApplicationRolePermission
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            IsGranted = true
        });
        await fixture.Context.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "cache-user")],
            "test"));
        Assert.True(await fixture.Service.HasAccessAsync(user, Permissions.Student.View));

        var assignment = await fixture.Context.ApplicationRolePermissions
            .SingleAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id);
        assignment.IsGranted = false;
        await fixture.Context.SaveChangesAsync();

        Assert.True(await fixture.Service.HasAccessAsync(user, Permissions.Student.View));
        fixture.Service.InvalidateUser("cache-user");
        Assert.False(await fixture.Service.HasAccessAsync(user, Permissions.Student.View));
    }

    [Fact]
    public void All_permissions_cover_module_metadata()
    {
        var all = Permissions.All();
        Assert.Contains(all, x => x.Key == Permissions.Student.View);
        Assert.Contains(all, x => x.Key == Permissions.Student.Create);
        Assert.Contains(all, x => x.ModuleDisplayName == "Sinh viên");
        Assert.DoesNotContain(all, x => x.Module is "Lesson" or "Lesson4" or "Lec5");
    }

    private static ApplicationPermission Permission(string key)
    {
        var parts = key.Split('.');
        return new ApplicationPermission
        {
            Key = key,
            Controller = parts[0],
            Action = parts.Length > 1 ? parts[1] : string.Empty,
            Area = string.Empty,
            Description = key
        };
    }

    private sealed class PermissionFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        public ApplicationDbContext Context { get; }
        public PermissionCatalogService Service { get; }

        private PermissionFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            ApplicationDbContext context,
            PermissionCatalogService service)
        {
            _connection = connection;
            _provider = provider;
            Context = context;
            Service = service;
        }

        public static async Task<PermissionFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            var provider = services.BuildServiceProvider();
            var context = provider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();
            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            var cache = provider.GetRequiredService<IMemoryCache>();
            var service = new PermissionCatalogService(context, roleManager, cache);
            return new PermissionFixture(connection, provider, context, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
