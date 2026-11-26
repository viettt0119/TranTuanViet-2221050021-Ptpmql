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
    public async Task EnsureDefaultUserPermissionsAsync_grants_only_default_read_permissions_once()
    {
        await using var fixture = await PermissionFixture.CreateAsync();

        var userRole = new IdentityRole { Id = "user-role", Name = "User", NormalizedName = "USER" };
        fixture.Context.Roles.Add(userRole);
        fixture.Context.ApplicationPermissions.AddRange(
            Permission("Student.Index"),
            Permission("Student.Details"),
            Permission("Department.Index"),
            Permission("Student.Create"),
            Permission("Student.Delete"));
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.EnsureDefaultUserPermissionsAsync();
        await fixture.Service.EnsureDefaultUserPermissionsAsync();

        var keys = await GrantedKeysAsync(fixture, userRole.Id);
        Assert.Equal(["Department.Index", "Student.Details", "Student.Index"], keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SyncAsync_creates_one_permission_per_controller_action()
    {
        await using var fixture = await PermissionFixture.CreateAsync();

        await fixture.Service.SyncAsync();

        var stored = await fixture.Context.ApplicationPermissions.ToListAsync();
        foreach (var metadata in Permissions.All())
        {
            var permission = Assert.Single(stored, x => x.Key == metadata.Key);
            Assert.Equal(metadata.Controller, permission.Controller);
            Assert.Equal(metadata.Action, permission.Action);
            Assert.Equal(metadata.ModuleDisplayName, permission.ModuleDisplayName);
            Assert.Equal(metadata.ActionDisplayName, permission.ActionDisplayName);
        }
        Assert.Equal(Permissions.All().Count, stored.Count);
    }

    [Fact]
    public async Task SyncAsync_moves_grants_from_legacy_module_permissions_to_actions()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var role = new IdentityRole { Id = "legacy-role", Name = "Legacy", NormalizedName = "LEGACY" };
        fixture.Context.Roles.Add(role);
        var legacyView = Permission("Student.View");
        var legacyUserEdit = Permission("User.Edit");
        var legacyRevoked = Permission("Product.Delete.Old");
        fixture.Context.ApplicationPermissions.AddRange(legacyView, legacyUserEdit, legacyRevoked);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ApplicationRolePermissions.AddRange(
            new ApplicationRolePermission { RoleId = role.Id, PermissionId = legacyView.Id, IsGranted = true },
            new ApplicationRolePermission { RoleId = role.Id, PermissionId = legacyUserEdit.Id, IsGranted = true },
            new ApplicationRolePermission { RoleId = role.Id, PermissionId = legacyRevoked.Id, IsGranted = true });
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.SyncAsync();

        var keys = await GrantedKeysAsync(fixture, role.Id);
        Assert.Contains("Student.Index", keys);
        Assert.Contains("Student.Details", keys);
        Assert.Contains("UserManagement.Roles", keys);
        Assert.DoesNotContain("Student.Create", keys);
        Assert.DoesNotContain(await fixture.Context.ApplicationPermissions.ToListAsync(),
            x => x.Key is "Student.View" or "User.Edit" or "Product.Delete.Old");
    }

    [Fact]
    public async Task SyncAsync_keeps_existing_action_grants()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var role = new IdentityRole { Id = "keep-role", Name = "Keep", NormalizedName = "KEEP" };
        var permission = Permission("Student.Create");
        fixture.Context.Roles.Add(role);
        fixture.Context.ApplicationPermissions.Add(permission);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ApplicationRolePermissions.Add(new ApplicationRolePermission
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            IsGranted = true
        });
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.SyncAsync();

        Assert.Equal(["Student.Create"], await GrantedKeysAsync(fixture, role.Id));
    }

    [Fact]
    public async Task Cache_is_refreshed_after_invalidate_user()
    {
        await using var fixture = await PermissionFixture.CreateAsync();
        var role = new IdentityRole { Id = "cache-role", Name = "CacheRole", NormalizedName = "CACHEROLE" };
        var permission = Permission("Student.Index");
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
        Assert.True(await fixture.Service.HasAccessAsync(user, "Student.Index"));

        var assignment = await fixture.Context.ApplicationRolePermissions
            .SingleAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id);
        assignment.IsGranted = false;
        await fixture.Context.SaveChangesAsync();

        Assert.True(await fixture.Service.HasAccessAsync(user, "Student.Index"));
        fixture.Service.InvalidateUser("cache-user");
        Assert.False(await fixture.Service.HasAccessAsync(user, "Student.Index"));
    }

    private static async Task<List<string>> GrantedKeysAsync(PermissionFixture fixture, string roleId) =>
        await fixture.Context.ApplicationRolePermissions
            .Where(x => x.RoleId == roleId && x.IsGranted)
            .Select(x => x.Permission.Key)
            .ToListAsync();

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
