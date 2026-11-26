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

        public UserManagementController CreateUserManagementController()
        {
            var catalog = new PermissionCatalogService(Context, RoleManager, _provider.GetRequiredService<IMemoryCache>());
            return new UserManagementController(UserManager, RoleManager, Context, catalog)
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

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
