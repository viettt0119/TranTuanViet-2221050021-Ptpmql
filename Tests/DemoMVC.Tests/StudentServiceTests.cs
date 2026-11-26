using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.ViewModels.Students;
using DemoMVC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DemoMVC.Tests;

public class StudentServiceTests
{
    [Fact]
    public async Task GetPagedAsync_filters_department_year_and_keyword_and_pages_in_database()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        fixture.Context.Departments.Add(new DepartmentModel
        {
            DepartmentCode = "TEST",
            DepartmentName = "Khoa kiểm thử"
        });
        fixture.Context.Students.AddRange(
            Student("2821050001", "Nguyen Van A", "TEST", 2026),
            Student("2821050002", "Tran Thi B", "TEST", 2026),
            Student("2721050003", "Le Van C", "TEST", 2025));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.GetPagedAsync(new StudentFilterViewModel
        {
            Q = "Nguyen",
            DepartmentCode = "TEST",
            AdmissionYear = 2026,
            Page = 1,
            PageSize = 10
        });

        Assert.Single(result.Students.Items);
        Assert.Equal("2821050001", result.Students.Items[0].StudentCode);
    }

    [Fact]
    public async Task UpdateAsync_rejects_duplicate_email_and_keeps_original_student_code()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        fixture.Context.Departments.Add(new DepartmentModel
        {
            DepartmentCode = "TEST",
            DepartmentName = "Khoa kiểm thử"
        });
        var first = Student("2821050001", "Nguyen Van A", "TEST", 2026);
        first.Email = "first@example.test";
        var second = Student("2821050002", "Tran Thi B", "TEST", 2026);
        second.Email = "second@example.test";
        fixture.Context.Students.AddRange(first, second);
        await fixture.Context.SaveChangesAsync();

        var duplicateResult = await fixture.Service.UpdateAsync(first.Id, new StudentFormViewModel
        {
            Id = first.Id,
            StudentCode = "9999999999",
            FullName = first.FullName,
            Address = first.Address,
            University = first.University,
            DepartmentCode = "TEST",
            AdmissionYear = first.AdmissionYear,
            TrainingSystemCode = first.TrainingSystemCode,
            MajorCode = first.MajorCode,
            Email = "second@example.test",
            ConcurrencyStamp = first.ConcurrencyStamp
        });

        Assert.False(duplicateResult.Success);
        Assert.True(duplicateResult.FieldErrors.ContainsKey(nameof(StudentFormViewModel.Email)));

        var reloaded = await fixture.Context.Students.FindAsync(first.Id);
        Assert.Equal("2821050001", reloaded!.StudentCode);
    }

    [Fact]
    public async Task UpdateAsync_rejects_stale_concurrency_stamp()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        fixture.Context.Departments.Add(new DepartmentModel
        {
            DepartmentCode = "TEST",
            DepartmentName = "Khoa kiểm thử"
        });
        var student = Student("2821050001", "Nguyen Van A", "TEST", 2026);
        fixture.Context.Students.Add(student);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.UpdateAsync(student.Id, new StudentFormViewModel
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = "Ten moi",
            Address = student.Address,
            University = student.University,
            DepartmentCode = "TEST",
            AdmissionYear = student.AdmissionYear,
            TrainingSystemCode = student.TrainingSystemCode,
            MajorCode = student.MajorCode,
            ConcurrencyStamp = Guid.NewGuid()
        });

        Assert.False(result.Success);
        Assert.Contains("thay đổi", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("own@example.test")]
    [InlineData("  OWN@Example.TEST ")]
    public async Task GetOwnProfileAsync_returns_the_record_with_the_confirmed_account_email(string accountEmail)
    {
        await using var fixture = await StudentFixture.CreateAsync();
        await SeedProfilesAsync(fixture, "Own@example.test", "other@example.test");

        var profile = await fixture.Service.GetOwnProfileAsync(Account(accountEmail));

        Assert.NotNull(profile);
        Assert.Equal("2821050001", profile.StudentCode);
        Assert.Equal("Khoa kiểm thử", profile.DepartmentName);
    }

    [Fact]
    public async Task GetOwnProfileAsync_does_not_trust_the_self_declared_student_code()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        await SeedProfilesAsync(fixture, "own@example.test", "other@example.test");
        var account = Account("stranger@example.test");
        account.MemberCode = "2821050001";

        Assert.Null(await fixture.Service.GetOwnProfileAsync(account));
    }

    [Theory]
    [InlineData(false, AccountType.Student)]
    [InlineData(true, AccountType.Staff)]
    public async Task GetOwnProfileAsync_requires_a_confirmed_student_account(bool emailConfirmed, AccountType type)
    {
        await using var fixture = await StudentFixture.CreateAsync();
        await SeedProfilesAsync(fixture, "own@example.test", "other@example.test");
        var account = Account("own@example.test");
        account.EmailConfirmed = emailConfirmed;
        account.AccountType = type;

        Assert.Null(await fixture.Service.GetOwnProfileAsync(account));
    }

    [Fact]
    public async Task GetOwnProfileAsync_links_nothing_when_two_records_share_the_email()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        // Chỉ mục duy nhất của SQLite phân biệt hoa/thường nên hai hồ sơ này cùng tồn tại được.
        await SeedProfilesAsync(fixture, "own@example.test", "OWN@example.test");

        Assert.Null(await fixture.Service.GetOwnProfileAsync(Account("own@example.test")));
    }

    private static ApplicationUser Account(string email) => new()
    {
        Email = email,
        FullName = "Sinh vien",
        AccountType = AccountType.Student,
        EmailConfirmed = true
    };

    private static async Task SeedProfilesAsync(StudentFixture fixture, string firstEmail, string secondEmail)
    {
        fixture.Context.Departments.Add(new DepartmentModel
        {
            DepartmentCode = "TEST",
            DepartmentName = "Khoa kiểm thử"
        });
        var first = Student("2821050001", "Nguyen Van A", "TEST", 2026);
        first.Email = firstEmail;
        var second = Student("2821050002", "Tran Thi B", "TEST", 2026);
        second.Email = secondEmail;
        fixture.Context.Students.AddRange(first, second);
        await fixture.Context.SaveChangesAsync();
    }

    private static StudentModel Student(string code, string name, string departmentCode, int year) => new()
    {
        StudentCode = code,
        FullName = name,
        Address = "Dia chi",
        University = "Đại học Mỏ - Địa chất",
        DepartmentCode = departmentCode,
        AdmissionYear = year,
        TrainingSystemCode = "21",
        MajorCode = "05",
        ConcurrencyStamp = Guid.NewGuid()
    };

    private sealed class StudentFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public StudentService Service { get; }

        private StudentFixture(SqliteConnection connection, ApplicationDbContext context, StudentService service)
        {
            _connection = connection;
            Context = context;
            Service = service;
        }

        public static async Task<StudentFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var service = new StudentService(context, new StudentCodeGenerator(context));
            return new StudentFixture(connection, context, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
