using System.Security.Claims;
using DemoMVC.Controllers;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OfficeOpenXml;
using Xunit;

namespace DemoMVC.Tests;

public class ExcelImportControllerTests
{
    static ExcelImportControllerTests()
    {
        ExcelPackage.License.SetNonCommercialOrganization("DemoMVC Tests");
    }

    [Fact]
    public async Task Upload_page_lists_only_entities_the_user_may_add_or_edit()
    {
        await using var fixture = await Fixture.CreateAsync("Department.Edit", "Student.Create", "Product.Index");

        var view = Assert.IsType<ViewResult>(await fixture.Controller().Index("student"));

        var allowed = Assert.IsAssignableFrom<IReadOnlyList<string>>(view.ViewData["AllowedEntities"]);
        Assert.Equal(["department", "student"], allowed);
        Assert.Equal("student", view.ViewData["SelectedEntity"]);
    }

    [Theory]
    [InlineData("product")]
    [InlineData("rolepermission")]
    public async Task Import_and_template_are_forbidden_outside_the_granted_modules(string entity)
    {
        await using var fixture = await Fixture.CreateAsync("Department.Create");
        var controller = fixture.Controller();

        Assert.IsType<ForbidResult>(await controller.Import(DepartmentFile("MOI", "Khoa mới"), entity));
        Assert.IsType<ForbidResult>(await controller.DownloadTemplate(entity));
    }

    [Fact]
    public async Task Unknown_entity_is_rejected()
    {
        await using var fixture = await Fixture.CreateAsync("Department.Create");

        var result = await fixture.Controller().Import(DepartmentFile("MOI", "Khoa mới"), "users");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_permission_alone_allows_append_but_not_overwrite_and_a_preview_runs_once()
    {
        await using var fixture = await Fixture.CreateAsync("Department.Create");
        var controller = fixture.Controller();
        var preview = await PreviewAsync(controller, DepartmentFile("CNTT", "Tên ghi đè"));

        Assert.True(preview.CanAppend);
        Assert.False(preview.CanOverwrite);
        Assert.IsType<ForbidResult>(await controller.Confirm(preview.Token, ExcelImportMode.Overwrite));

        var appended = Assert.IsType<ViewResult>(await controller.Confirm(preview.Token, ExcelImportMode.Append));
        var result = Assert.IsType<ExcelImportResult>(appended.Model);
        Assert.Equal(ExcelImportStatus.Unchanged, result.Status);
        Assert.NotEqual("Tên ghi đè", (await fixture.Context.Departments.AsNoTracking()
            .SingleAsync(x => x.DepartmentCode == "CNTT")).DepartmentName);

        var again = Assert.IsType<ViewResult>(await controller.Confirm(preview.Token, ExcelImportMode.Append));
        Assert.Equal("Index", again.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Preview_of_another_user_cannot_be_confirmed()
    {
        await using var fixture = await Fixture.CreateAsync("Department.Create");
        var preview = await PreviewAsync(fixture.Controller(), DepartmentFile("MOI", "Khoa mới"));

        var result = await fixture.Controller(userId: "someone-else", isAdmin: true)
            .Confirm(preview.Token, ExcelImportMode.Append);

        Assert.IsType<ForbidResult>(result);
        Assert.False(await fixture.Context.Departments.AnyAsync(x => x.DepartmentCode == "MOI"));
    }

    [Fact]
    public async Task Permission_revoked_after_preview_blocks_the_confirmation()
    {
        await using var fixture = await Fixture.CreateAsync("Department.Create");
        var controller = fixture.Controller();
        var preview = await PreviewAsync(controller, DepartmentFile("MOI", "Khoa mới"));
        await fixture.RevokeAllAsync();

        var result = await controller.Confirm(preview.Token, ExcelImportMode.Append);

        Assert.IsType<ForbidResult>(result);
        Assert.False(await fixture.Context.Departments.AnyAsync(x => x.DepartmentCode == "MOI"));
    }

    [Fact]
    public async Task Overwrite_is_refused_when_the_record_changed_after_preview()
    {
        await using var fixture = await Fixture.CreateAsync("Department.Create", "Department.Edit");
        var controller = fixture.Controller();
        var preview = await PreviewAsync(controller, DepartmentFile("CNTT", "Tên trong file"));
        var department = await fixture.Context.Departments.SingleAsync(x => x.DepartmentCode == "CNTT");
        department.DepartmentName = "Người khác vừa sửa";
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var view = Assert.IsType<ViewResult>(await controller.Confirm(preview.Token, ExcelImportMode.Overwrite));

        var result = Assert.IsType<ExcelImportResult>(view.Model);
        Assert.Equal(ExcelImportStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("thay đổi", StringComparison.Ordinal));
        Assert.Equal("Người khác vừa sửa", (await fixture.Context.Departments.AsNoTracking()
            .SingleAsync(x => x.DepartmentCode == "CNTT")).DepartmentName);
    }

    [Fact]
    public async Task File_that_is_not_a_workbook_shows_a_message_instead_of_failing()
    {
        await using var fixture = await Fixture.CreateAsync("Department.Create");
        var controller = fixture.Controller();
        var bytes = "khong phai file excel"u8.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "hong.xlsx");

        var view = Assert.IsType<ViewResult>(await controller.Import(file, "department"));

        Assert.Equal("Index", view.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    private static async Task<ExcelImportPreview> PreviewAsync(ExcelImportController controller, IFormFile file)
    {
        var view = Assert.IsType<ViewResult>(await controller.Import(file, "department"));
        return Assert.IsType<ExcelImportPreview>(view.Model);
    }

    private static FormFile DepartmentFile(string code, string name)
    {
        var stream = new MemoryStream();
        using (var package = new ExcelPackage(stream))
        {
            var sheet = package.Workbook.Worksheets.Add("Data");
            sheet.Cells[1, 1].Value = "DepartmentCode";
            sheet.Cells[1, 2].Value = "DepartmentName";
            sheet.Cells[2, 1].Value = code;
            sheet.Cells[2, 2].Value = name;
            package.Save();
        }
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "khoa.xlsx");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly PermissionCatalogService _catalog;
        public ApplicationDbContext Context { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext context)
        {
            _connection = connection;
            Context = context;
            var roleManager = new RoleManager<IdentityRole>(
                Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);
            _catalog = new PermissionCatalogService(context, roleManager, _cache);
        }

        public static async Task<Fixture> CreateAsync(params string[] permissionKeys)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();

            context.Users.Add(new ApplicationUser { Id = "importer", UserName = "importer", FullName = "Importer" });
            context.Roles.Add(new IdentityRole { Id = "role", Name = "Importer", NormalizedName = "IMPORTER" });
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = "importer", RoleId = "role" });
            foreach (var key in permissionKeys)
            {
                var parts = key.Split('.');
                context.ApplicationRolePermissions.Add(new ApplicationRolePermission
                {
                    RoleId = "role",
                    Permission = new ApplicationPermission { Key = key, Controller = parts[0], Action = parts[1] },
                    IsGranted = true
                });
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new Fixture(connection, context);
        }

        public ExcelImportController Controller(string userId = "importer", bool isAdmin = false)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
            if (isAdmin)
                claims.Add(new Claim(ClaimTypes.Role, SystemRoles.Admin));
            var service = new ExcelImportService(Context, new StudentCodeGenerator(Context));
            return new ExcelImportController(service, _cache, _catalog)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                    }
                }
            };
        }

        public async Task RevokeAllAsync()
        {
            await Context.ApplicationRolePermissions.ExecuteDeleteAsync();
            _catalog.InvalidateAll();
        }

        public async ValueTask DisposeAsync()
        {
            _cache.Dispose();
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
