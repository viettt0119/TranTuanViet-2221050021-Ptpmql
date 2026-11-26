using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace DemoMVC.Tests;

public class AccountTypeAndApprovalTests
{
    [Theory]
    [InlineData(AccountType.Student, "2521050001", true)]
    [InlineData(AccountType.Student, " 2521050001 ", true)]
    [InlineData(AccountType.Student, "1521050001", false)]  // không bắt đầu bằng 2
    [InlineData(AccountType.Student, "252105000", false)]   // 9 chữ số
    [InlineData(AccountType.Student, "25210500AB", false)]
    [InlineData(AccountType.Student, "", false)]
    [InlineData(AccountType.Staff, "GV0123", true)]
    [InlineData(AccountType.Staff, "gv0123", true)]          // chuẩn hoá thành chữ in hoa
    [InlineData(AccountType.Staff, "2521050001", false)]
    [InlineData(AccountType.Staff, "G1", false)]
    [InlineData(AccountType.Staff, null, false)]
    public void Member_code_format_depends_on_account_type(AccountType type, string? code, bool valid)
    {
        var model = new RegisterViewModel
        {
            AccountType = type,
            MemberCode = code,
            FullName = "Người dùng",
            Email = "user@example.test",
            Password = "GoodPassword!123",
            ConfirmPassword = "GoodPassword!123"
        };

        var errors = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);

        Assert.Equal(valid, isValid);
        if (!valid)
            Assert.Contains(errors, error => error.MemberNames.Contains(nameof(RegisterViewModel.MemberCode)));
    }

    [Fact]
    public async Task Pending_approval_is_saved_as_false_not_replaced_by_the_database_default()
    {
        // IsApproved có HasDefaultValue(true); thiếu HasSentinel(true) thì EF bỏ false khỏi INSERT và giảng viên tự được duyệt.
        await using var fixture = await IdentityFixture.CreateAsync();
        var staff = NewUser("gv@example.test", AccountType.Staff, "GV0123", approved: false);

        Assert.True((await fixture.UserManager.CreateAsync(staff, "GoodPassword!123")).Succeeded);

        fixture.Context.ChangeTracker.Clear();
        var reloaded = await fixture.Context.Users.AsNoTracking().SingleAsync(x => x.Email == "gv@example.test");
        Assert.False(reloaded.IsApproved);
        Assert.Equal(AccountType.Staff, reloaded.AccountType);
    }

    [Fact]
    public async Task Duplicate_member_code_is_rejected_by_the_user_validator()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        Assert.True((await fixture.UserManager.CreateAsync(
            NewUser("a@example.test", AccountType.Student, "2521050001"), "GoodPassword!123")).Succeeded);

        var result = await fixture.UserManager.CreateAsync(
            NewUser("b@example.test", AccountType.Student, "2521050001"), "GoodPassword!123");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == MemberCodeUserValidator.DuplicateCode);
    }

    [Fact]
    public async Task Approve_lets_staff_sign_in_and_assigns_the_lecturer_role()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var staff = NewUser("gv@example.test", AccountType.Staff, "GV0123", approved: false);
        await fixture.UserManager.CreateAsync(staff, "GoodPassword!123");
        var controller = fixture.CreateUserManagementController();

        var result = await controller.Approve(staff.Id);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Null(json.StatusCode);
        Assert.True(JsonSerializer.SerializeToElement(json.Value).GetProperty("success").GetBoolean());
        var reloaded = await fixture.UserManager.FindByIdAsync(staff.Id);
        Assert.True(reloaded!.IsApproved);
        Assert.True(await fixture.UserManager.IsInRoleAsync(reloaded, SystemRoles.Lecturer));
    }

    [Fact]
    public async Task Approve_refuses_student_accounts()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var student = NewUser("sv@example.test", AccountType.Student, "2521050001");
        await fixture.UserManager.CreateAsync(student, "GoodPassword!123");

        var result = await fixture.CreateUserManagementController().Approve(student.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<JsonResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approve_rolls_back_both_role_and_approval_when_identity_fails(bool failApprovalUpdate)
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var staff = NewUser("pending@example.test", AccountType.Staff, "GV0123", approved: false);
        Assert.True((await fixture.UserManager.CreateAsync(staff, "GoodPassword!123")).Succeeded);
        Assert.True((await fixture.RoleManager.CreateAsync(new IdentityRole(SystemRoles.Lecturer))).Succeeded);
        var manager = fixture.CreateFailingApprovalManager(failApprovalUpdate);

        var result = await fixture.CreateUserManagementController(manager).Approve(staff.Id);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, Assert.IsType<JsonResult>(result).StatusCode);
        fixture.Context.ChangeTracker.Clear();
        var stored = await fixture.UserManager.FindByIdAsync(staff.Id);
        Assert.False(stored!.IsApproved);
        Assert.False(await fixture.UserManager.IsInRoleAsync(stored, SystemRoles.Lecturer));

        // Sau lỗi có thể thử lại thành công, không mắc kẹt ở trạng thái được duyệt nhưng thiếu vai trò.
        var retry = Assert.IsType<JsonResult>(await fixture.CreateUserManagementController().Approve(staff.Id));
        Assert.Null(retry.StatusCode);
        Assert.True((await fixture.UserManager.FindByIdAsync(staff.Id))!.IsApproved);
        Assert.Single(await fixture.Context.UserRoles.Where(x => x.UserId == staff.Id).ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approve_is_idempotent_and_repairs_an_old_approved_account_without_a_role(bool alreadyApproved)
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var staff = NewUser("staff@example.test", AccountType.Staff, "GV0123", approved: alreadyApproved);
        Assert.True((await fixture.UserManager.CreateAsync(staff, "GoodPassword!123")).Succeeded);
        var controller = fixture.CreateUserManagementController();

        Assert.Null(Assert.IsType<JsonResult>(await controller.Approve(staff.Id)).StatusCode);
        Assert.Null(Assert.IsType<JsonResult>(await controller.Approve(staff.Id)).StatusCode);

        fixture.Context.ChangeTracker.Clear();
        Assert.True((await fixture.UserManager.FindByIdAsync(staff.Id))!.IsApproved);
        Assert.Single(await fixture.Context.UserRoles.Where(x => x.UserId == staff.Id).ToListAsync());
        Assert.Single(await fixture.Context.Roles.Where(x => x.Name == SystemRoles.Lecturer).ToListAsync());
    }

    [Fact]
    public void Approve_is_a_separate_permission_on_the_matrix()
    {
        var approve = Assert.Single(Permissions.All(), x => x.Key == "UserManagement.Approve");
        Assert.Equal("Phê duyệt giảng viên", approve.ActionDisplayName);
        Assert.All(PermissionCatalogService.DefaultLecturerPermissionKeys,
            key => Assert.Contains(Permissions.All(), x => x.Key == key));
    }

    [Fact]
    public void System_roles_include_the_lecturer_role()
    {
        Assert.True(SystemRoles.IsSystemRole("giangvien"));
        Assert.True(SystemRoles.IsSystemRole("USER"));
        Assert.False(SystemRoles.IsSystemRole("Kế toán"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Roles_rolls_back_the_whole_assignment_when_an_identity_step_fails(bool failRemoval)
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var user = NewUser("roles@example.test", AccountType.Student, "2521050001");
        Assert.True((await fixture.UserManager.CreateAsync(user, "GoodPassword!123")).Succeeded);
        var oldRole = new IdentityRole("Reader");
        var newRole = new IdentityRole("Editor");
        Assert.True((await fixture.RoleManager.CreateAsync(oldRole)).Succeeded);
        Assert.True((await fixture.RoleManager.CreateAsync(newRole)).Succeeded);
        Assert.True((await fixture.UserManager.AddToRoleAsync(user, oldRole.Name!)).Succeeded);
        var controller = fixture.CreateUserManagementController(fixture.CreateFailingRolesManager(failRemoval));

        var result = await controller.Roles(user.Id, [newRole.Id]);

        Assert.Equal("_RolesModal", Assert.IsType<PartialViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, controller.Response.StatusCode);
        Assert.False(controller.ModelState.IsValid);
        fixture.Context.ChangeTracker.Clear();
        var stored = (await fixture.UserManager.FindByIdAsync(user.Id))!;
        Assert.Equal(new[] { "Reader" }, await fixture.UserManager.GetRolesAsync(stored));

        var retry = await fixture.CreateUserManagementController().Roles(user.Id, [newRole.Id]);
        Assert.True(JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(retry).Value)
            .GetProperty("success").GetBoolean());
        fixture.Context.ChangeTracker.Clear();
        stored = (await fixture.UserManager.FindByIdAsync(user.Id))!;
        Assert.Equal(new[] { "Editor" }, await fixture.UserManager.GetRolesAsync(stored));
    }

    [Fact]
    public async Task Roles_rejects_a_deleted_or_unknown_selection_without_removing_existing_roles()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var user = NewUser("roles@example.test", AccountType.Student, "2521050001");
        Assert.True((await fixture.UserManager.CreateAsync(user, "GoodPassword!123")).Succeeded);
        Assert.True((await fixture.RoleManager.CreateAsync(new IdentityRole("Reader"))).Succeeded);
        Assert.True((await fixture.UserManager.AddToRoleAsync(user, "Reader")).Succeeded);
        var controller = fixture.CreateUserManagementController();

        var result = await controller.Roles(user.Id, ["deleted-role"]);

        Assert.IsType<PartialViewResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, controller.Response.StatusCode);
        Assert.Equal(new[] { "Reader" }, await fixture.UserManager.GetRolesAsync(user));
    }

    [Fact]
    public async Task Roles_cannot_remove_the_last_admin_or_add_another_role_during_the_rejected_request()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var user = NewUser("admin@example.test", AccountType.Staff, "GV0123");
        Assert.True((await fixture.UserManager.CreateAsync(user, "GoodPassword!123")).Succeeded);
        Assert.True((await fixture.RoleManager.CreateAsync(new IdentityRole(SystemRoles.Admin))).Succeeded);
        var otherRole = new IdentityRole("Reader");
        Assert.True((await fixture.RoleManager.CreateAsync(otherRole)).Succeeded);
        Assert.True((await fixture.UserManager.AddToRoleAsync(user, SystemRoles.Admin)).Succeeded);
        var controller = fixture.CreateUserManagementController();

        var result = await controller.Roles(user.Id, [otherRole.Id]);

        Assert.IsType<PartialViewResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, controller.Response.StatusCode);
        Assert.Equal(new[] { SystemRoles.Admin }, await fixture.UserManager.GetRolesAsync(user));
    }

    private static ApplicationUser NewUser(string email, AccountType type, string code, bool approved = true) => new()
    {
        UserName = email,
        Email = email,
        FullName = email,
        EmailConfirmed = true,
        AccountType = type,
        MemberCode = code,
        IsApproved = approved
    };

    private sealed class IdentityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        public ApplicationDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole> RoleManager { get; }

        private IdentityFixture(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
            Context = provider.GetRequiredService<ApplicationDbContext>();
            UserManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        }

        public static async Task<IdentityFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddUserValidator<MemberCodeUserValidator>();
            var provider = services.BuildServiceProvider();
            await provider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            return new IdentityFixture(connection, provider);
        }

        public UserManagementController CreateUserManagementController(UserManager<ApplicationUser>? manager = null)
        {
            var catalog = new PermissionCatalogService(Context, RoleManager, _provider.GetRequiredService<IMemoryCache>());
            return new UserManagementController(manager ?? UserManager, RoleManager, Context, catalog)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, "admin-user"), new Claim(ClaimTypes.Role, SystemRoles.Admin)],
                            "test"))
                    }
                }
            };
        }

        public UserManager<ApplicationUser> CreateFailingApprovalManager(bool failApprovalUpdate)
        {
            var mock = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            mock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .Returns((string id) => UserManager.FindByIdAsync(id));
            mock.Setup(x => x.IsInRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Returns((ApplicationUser user, string role) => UserManager.IsInRoleAsync(user, role));
            mock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Returns(async (ApplicationUser user, string role) =>
                {
                    // Ghi thật vào SQLite trước khi mô phỏng lỗi để chứng minh transaction hoàn tác được.
                    var result = await UserManager.AddToRoleAsync(user, role);
                    return failApprovalUpdate ? result : Failure();
                });
            mock.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
                .Returns(async (ApplicationUser user) =>
                {
                    var result = await UserManager.UpdateAsync(user);
                    return failApprovalUpdate ? Failure() : result;
                });
            return mock.Object;

            static IdentityResult Failure() => IdentityResult.Failed(new IdentityError
            {
                Code = "ApprovalTestFailure", Description = "Không thể hoàn tất phê duyệt."
            });
        }

        public UserManager<ApplicationUser> CreateFailingRolesManager(bool failRemoval)
        {
            var mock = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            mock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .Returns((string id) => UserManager.FindByIdAsync(id));
            mock.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .Returns((ApplicationUser user) => UserManager.GetRolesAsync(user));
            mock.Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
                .Returns(async (ApplicationUser user, IEnumerable<string> roles) =>
                {
                    var result = await UserManager.AddToRolesAsync(user, roles);
                    return failRemoval ? result : Failure();
                });
            mock.Setup(x => x.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
                .Returns(async (ApplicationUser user, IEnumerable<string> roles) =>
                {
                    await UserManager.RemoveFromRolesAsync(user, roles);
                    return Failure();
                });
            return mock.Object;

            static IdentityResult Failure() => IdentityResult.Failed(new IdentityError
            {
                Code = "RoleTestFailure", Description = "Không thể hoàn tất gán vai trò."
            });
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
