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
            Key = "Student.Index",
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
    public async Task Action_without_any_attribute_is_denied_by_default()
    {
        // Action không gắn attribute nào phải được cấp quyền "Controller.Action" mới vào được.
        await using var fixture = await FilterFixture.CreateAsync();
        var context = fixture.CreateContext(typeof(ProductController), "Product", nameof(ProductController.Create), "Create");
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-without-permission"), new Claim(ClaimTypes.Role, "User")],
            "test"));

        await fixture.Filter.OnAuthorizationAsync(context);

        Assert.IsType<RedirectToActionResult>(context.Result);
    }

    [Fact]
    public async Task Landing_page_is_anonymous_and_dashboard_needs_login()
    {
        await using var fixture = await FilterFixture.CreateAsync();
        var landingPage = fixture.CreateContext(typeof(HomeController), "Home", nameof(HomeController.Index), "Index");
        await fixture.Filter.OnAuthorizationAsync(landingPage);
        Assert.Null(landingPage.Result);

        var dashboard = fixture.CreateContext(typeof(HomeController), "Home", nameof(HomeController.Dashboard), "Dashboard");
        dashboard.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-without-permission")],
            "test"));
        await fixture.Filter.OnAuthorizationAsync(dashboard);
        Assert.Null(dashboard.Result);

        var anonymousDashboard = fixture.CreateContext(typeof(HomeController), "Home", nameof(HomeController.Dashboard), "Dashboard");
        await fixture.Filter.OnAuthorizationAsync(anonymousDashboard);
        Assert.IsType<ChallengeResult>(anonymousDashboard.Result);
    }

    [Fact]
    public void Every_action_of_every_controller_is_anonymous_authenticated_only_or_has_a_permission()
    {
        var discovered = Permissions.Discover(typeof(StudentController).Assembly)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var authenticatedOnly = new List<string>();
        var adminOnlyControllers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var controller in Permissions.GetControllerTypes(typeof(StudentController).Assembly))
        {
            foreach (var method in Permissions.GetActionMethods(controller))
            {
                var key = Permissions.Key(Permissions.GetControllerName(controller), Permissions.GetActionName(method));
                if (Permissions.IsAnonymous(controller, method))
                    continue;
                if (Permissions.IsAdminOnly(controller, method))
                {
                    Assert.DoesNotContain(key, discovered);
                    adminOnlyControllers.Add(Permissions.GetControllerName(controller));
                    continue;
                }
                if (Permissions.IsAuthenticatedOnly(controller, method))
                {
                    authenticatedOnly.Add(key);
                    continue;
                }

                Assert.Contains(key, discovered);
            }
        }

        // Danh sách action chỉ cần đăng nhập phải nhỏ và có chủ đích; thêm action mới vào đây là một quyết định bảo mật.
        Assert.Equal(
            new[] { "Account.ChangePassword", "Account.Logout", "Home.Dashboard" },
            authenticatedOnly.Distinct().Order(StringComparer.Ordinal).ToArray());
        // Chỉ các bài thực hành Lec 1–6 được dành riêng cho Admin.
        Assert.Equal(
            new[] { "Lec5", "Lesson", "Lesson4" },
            adminOnlyControllers.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Discovery_covers_previously_unprotected_controllers_and_uses_action_names()
    {
        var keys = Permissions.All().Select(x => x.Key).ToList();

        Assert.Contains("Student.Index", keys);
        Assert.Contains("Student.Delete", keys);       // DeleteConfirmed có [ActionName("Delete")]
        Assert.DoesNotContain("Student.DeleteConfirmed", keys);
        // Bài thực hành Lec 1–6 là [AdminOnly]: không nằm trên ma trận phân quyền động.
        Assert.DoesNotContain(keys, key =>
            key.StartsWith("Lesson.", StringComparison.Ordinal) ||
            key.StartsWith("Lesson4.", StringComparison.Ordinal) ||
            key.StartsWith("Lec5.", StringComparison.Ordinal));
        Assert.Contains("Role.Permissions", keys);
        Assert.Contains("UserManagement.Roles", keys);
        Assert.DoesNotContain(keys, key => key.StartsWith("Account.", StringComparison.Ordinal));
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(PermissionCatalogService.DefaultUserPermissionKeys, key => Assert.Contains(key, keys));
    }

    [Theory]
    [InlineData(typeof(LessonController), "Lesson", nameof(LessonController.Index))]
    [InlineData(typeof(LessonController), "Lesson", nameof(LessonController.Lesson6))]
    [InlineData(typeof(Lesson4.Controllers.Lesson4Controller), "Lesson4", "Index")]
    [InlineData(typeof(Lec5Controller), "Lec5", nameof(Lec5Controller.Create))]
    public async Task Lesson_pages_are_admin_only(Type controllerType, string controllerName, string action)
    {
        await using var fixture = await FilterFixture.CreateAsync();

        var admin = fixture.CreateContext(controllerType, controllerName, action, action);
        admin.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "admin-user"), new Claim(ClaimTypes.Role, "Admin")],
            "test"));
        await fixture.Filter.OnAuthorizationAsync(admin);
        Assert.Null(admin.Result);

        var user = fixture.CreateContext(controllerType, controllerName, action, action);
        user.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "normal-user"), new Claim(ClaimTypes.Role, "User")],
            "test"));
        await fixture.Filter.OnAuthorizationAsync(user);
        Assert.IsType<RedirectToActionResult>(user.Result);

        var ajax = fixture.CreateContext(controllerType, controllerName, action, action);
        ajax.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        ajax.HttpContext.User = user.HttpContext.User;
        await fixture.Filter.OnAuthorizationAsync(ajax);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<JsonResult>(ajax.Result).StatusCode);
    }

    [Fact]
    public async Task Leftover_lesson_grant_from_old_matrix_does_not_open_lesson_pages()
    {
        // CSDL cũ có thể còn quyền "Lesson.Lesson1" đã cấp cho role User; [AdminOnly] vẫn phải từ chối.
        await using var fixture = await FilterFixture.CreateAsync();
        var role = new IdentityRole { Id = "user-role", Name = "User", NormalizedName = "USER" };
        var permission = new ApplicationPermission
        {
            Key = "Lesson.Lesson1",
            Controller = "Lesson",
            Action = "Lesson1",
            Description = "Bài 1"
        };
        fixture.Context.Roles.Add(role);
        fixture.Context.ApplicationPermissions.Add(permission);
        fixture.Context.Users.Add(new ApplicationUser { Id = "old-user", UserName = "old@example.test", Email = "old@example.test" });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserRoles.Add(new IdentityUserRole<string> { UserId = "old-user", RoleId = role.Id });
        fixture.Context.ApplicationRolePermissions.Add(new ApplicationRolePermission
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            IsGranted = true
        });
        await fixture.Context.SaveChangesAsync();

        var context = fixture.CreateContext(typeof(LessonController), "Lesson", nameof(LessonController.Lesson1), "Lesson1");
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "old-user"), new Claim(ClaimTypes.Role, "User")],
            "test"));
        await fixture.Filter.OnAuthorizationAsync(context);

        Assert.IsType<RedirectToActionResult>(context.Result);
    }

    [Fact]
    public async Task Sync_removes_lesson_permissions_left_in_the_database()
    {
        await using var fixture = await FilterFixture.CreateAsync();
        fixture.Context.ApplicationPermissions.Add(new ApplicationPermission
        {
            Key = "Lec5.Index",
            Controller = "Lec5",
            Action = "Index",
            Description = "Lec5"
        });
        await fixture.Context.SaveChangesAsync();

        await fixture.Catalog.SyncAsync();

        Assert.False(await fixture.Context.ApplicationPermissions.AnyAsync(x =>
            x.Controller == "Lec5" || x.Controller == "Lesson" || x.Controller == "Lesson4"));
    }

    [Fact]
    public void Lesson_catalog_links_point_to_existing_admin_only_actions()
    {
        var assembly = typeof(LessonController).Assembly;
        foreach (var item in LessonController.Catalog)
        {
            var controller = Permissions.GetControllerTypes(assembly)
                .Single(type => Permissions.GetControllerName(type) == item.Controller);
            var method = Permissions.GetActionMethods(controller)
                .First(candidate => Permissions.GetActionName(candidate) == item.Action);
            Assert.True(Permissions.IsAdminOnly(controller, method), $"{item.Controller}.{item.Action} phải là [AdminOnly].");
        }

        Assert.Equal(Enumerable.Range(1, 6), LessonController.Catalog.Select(x => x.Lecture).Distinct().Order());
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

        public AuthorizationFilterContext CreateContext(Controller controller) =>
            CreateContext(typeof(StudentController), "Student", nameof(StudentController.Index), "Index");

        public AuthorizationFilterContext CreateContext(Type controllerType, string controllerName, string methodName, string actionName)
        {
            var action = new ControllerActionDescriptor
            {
                ControllerName = controllerName,
                ActionName = actionName,
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                EndpointMetadata = new List<object>(),
                MethodInfo = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .First(method => method.Name == methodName)
            };
            foreach (var metadata in action.MethodInfo.GetCustomAttributes(inherit: true))
                action.EndpointMetadata.Add(metadata);
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
