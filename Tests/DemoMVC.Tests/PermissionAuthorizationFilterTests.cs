using DemoMVC.Controllers;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace DemoMVC.Tests;

public class PermissionAuthorizationFilterTests
{
    [Fact]
    public async Task Ajax_request_without_permission_returns_403_json()
    {
        await using var fixture = await FilterFixture.CreateAsync();
        var context = fixture.CreateContext(new StudentController(fixture.Context, fixture.StudentService));
        context.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-without-permission")],
            "test"));

        await fixture.Filter.OnAuthorizationAsync(context);

        var json = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
        Assert.Contains("không có quyền", json.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Normal_request_without_permission_redirects_to_access_denied()
    {
        await using var fixture = await FilterFixture.CreateAsync();
        var context = fixture.CreateContext(new StudentController(fixture.Context, fixture.StudentService));
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-without-permission")],
            "test"));

        await fixture.Filter.OnAuthorizationAsync(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("AccessDenied", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
    }

    [Fact]
    public async Task Admin_or_granted_user_is_allowed()
    {
        await using var fixture = await FilterFixture.CreateAsync();

        var adminContext = fixture.CreateContext(new StudentController(fixture.Context, fixture.StudentService));
        adminContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "admin-user"), new Claim(ClaimTypes.Role, "Admin")],
            "test"));
        await fixture.Filter.OnAuthorizationAsync(adminContext);
        Assert.Null(adminContext.Result);

        var role = new IdentityRole { Id = "granted-role", Name = "Granted", NormalizedName = "GRANTED" };
        var permission = new ApplicationPermission
        {
            Key = Permissions.Student.View,
            Controller = "Student",
            Action = "Index",
            Description = "Sinh viên – Xem"
        };
        fixture.Context.Roles.Add(role);
        fixture.Context.ApplicationPermissions.Add(permission);
        fixture.Context.Users.Add(new ApplicationUser
        {
            Id = "granted-user",
            UserName = "granted@example.test",
            Email = "granted@example.test"
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = "granted-user",
            RoleId = role.Id
        });
        fixture.Context.ApplicationRolePermissions.Add(new ApplicationRolePermission
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            IsGranted = true
        });
        await fixture.Context.SaveChangesAsync();

        var grantedContext = fixture.CreateContext(new StudentController(fixture.Context, fixture.StudentService));
        grantedContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "granted-user")],
            "test"));
        await fixture.Filter.OnAuthorizationAsync(grantedContext);
        Assert.Null(grantedContext.Result);
    }

    [Fact]
    public void Every_public_student_action_has_permission()
    {
        foreach (var method in typeof(StudentController).GetMethods(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName || method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.NonActionAttribute>() is not null)
                continue;

            var permission = method.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.NotNull(permission);
            Assert.StartsWith("Student.", permission!.Key);
        }
    }

    private sealed class FilterFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        public ApplicationDbContext Context { get; }
        public StudentService StudentService { get; }
        public PermissionCatalogService Catalog { get; }
        public PermissionAuthorizationFilter Filter { get; }

        private FilterFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            ApplicationDbContext context,
            StudentService studentService,
            PermissionCatalogService catalog,
            PermissionAuthorizationFilter filter)
        {
            _connection = connection;
            _provider = provider;
            Context = context;
            StudentService = studentService;
            Catalog = catalog;
            Filter = filter;
        }

        public static async Task<FilterFixture> CreateAsync()
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
            var catalog = new PermissionCatalogService(context, roleManager, cache);
            var studentCodeGenerator = new StudentCodeGenerator(context);
            var studentService = new StudentService(context, studentCodeGenerator);
            var filter = new PermissionAuthorizationFilter(catalog);
            return new FilterFixture(connection, provider, context, studentService, catalog, filter);
        }

        public AuthorizationFilterContext CreateContext(Controller controller)
        {
            var action = new ControllerActionDescriptor
            {
                ControllerName = "Student",
                ActionName = "Index",
                ControllerTypeInfo = typeof(StudentController).GetTypeInfo(),
                MethodInfo = typeof(StudentController).GetMethod(
                    nameof(StudentController.Index),
                    BindingFlags.Instance | BindingFlags.Public)!
            };
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = HttpMethods.Get;
            httpContext.Request.Headers["Content-Type"] = "text/html";
            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                action);
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
