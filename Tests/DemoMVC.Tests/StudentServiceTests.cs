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

    [Fact]
    public async Task Create_and_update_reject_email_duplicates_regardless_of_case_and_surrounding_spaces()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        await SeedProfilesAsync(fixture, " First@Example.Test ", "second@example.test");
        var create = await fixture.Service.CreateAsync(Form(" first@example.test "));
        Assert.False(create.Success);
        Assert.Contains(nameof(StudentFormViewModel.Email), create.FieldErrors.Keys);

        var second = await fixture.Context.Students.SingleAsync(x => x.Email == "second@example.test");
        var edit = (await fixture.Service.GetForEditAsync(second.Id))!;
        edit.Email = " FIRST@EXAMPLE.TEST ";
        var update = await fixture.Service.UpdateAsync(second.Id, edit);
        Assert.False(update.Success);
        Assert.Contains(nameof(StudentFormViewModel.Email), update.FieldErrors.Keys);
        Assert.Equal("second@example.test", second.Email);
    }

    [Fact]
    public async Task Create_and_update_store_normalized_email_and_allow_the_same_record_to_keep_it()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        Assert.True((await fixture.Service.CreateAsync(Form(" Student@Example.Test "))).Success);
        var student = await fixture.Context.Students.SingleAsync();
        Assert.Equal("student@example.test", student.Email);

        var edit = (await fixture.Service.GetForEditAsync(student.Id))!;
        edit.Email = " STUDENT@EXAMPLE.TEST ";
        Assert.True((await fixture.Service.UpdateAsync(student.Id, edit)).Success);
        Assert.Equal("student@example.test", (await fixture.Context.Students.SingleAsync()).Email);
    }

    [Fact]
    public async Task Student_validation_rejects_invalid_form_fields_before_reserving_a_code()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        var invalid = Form();
        invalid.FullName = new string('A', 101);
        invalid.Gender = (StudentGender)99;
        invalid.TrainingSystemCode = "X";
        invalid.PhoneNumber = "123";
        var result = await fixture.Service.CreateAsync(invalid);

        Assert.False(result.Success);
        Assert.Contains(nameof(StudentFormViewModel.FullName), result.FieldErrors.Keys);
        Assert.Contains(nameof(StudentFormViewModel.Gender), result.FieldErrors.Keys);
        Assert.Contains(nameof(StudentFormViewModel.TrainingSystemCode), result.FieldErrors.Keys);
        Assert.Contains(nameof(StudentFormViewModel.PhoneNumber), result.FieldErrors.Keys);
        Assert.Empty(await fixture.Context.Students.ToListAsync());
        Assert.Empty(await fixture.Context.StudentCodeCounters.ToListAsync());
    }

    [Fact]
    public async Task Student_requires_an_existing_department_and_database_protects_the_referenced_department()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        var invalid = Form();
        invalid.DepartmentCode = "MISSING";
        var rejected = await fixture.Service.CreateAsync(invalid);
        Assert.False(rejected.Success);
        Assert.Contains(nameof(StudentFormViewModel.DepartmentCode), rejected.FieldErrors.Keys);
        Assert.Empty(await fixture.Context.StudentCodeCounters.ToListAsync());

        Assert.True((await fixture.Service.CreateAsync(Form())).Success);
        var department = await fixture.Context.Departments.SingleAsync(x => x.DepartmentCode == "CNTT");
        fixture.Context.Departments.Remove(department);
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
        fixture.Context.ChangeTracker.Clear();
        Assert.True(await fixture.Context.Departments.AnyAsync(x => x.DepartmentCode == "CNTT"));
        Assert.Single(await fixture.Context.Students.ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_student_does_not_reuse_an_issued_code()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        var first = await fixture.Service.CreateAsync(Form());
        Assert.True(first.Success);
        Assert.Equal("2621050001", first.EntityCode);
        var student = await fixture.Context.Students.SingleAsync();
        Assert.True((await fixture.Service.DeleteAsync(student.Id, student.ConcurrencyStamp)).Success);

        var second = await fixture.Service.CreateAsync(Form());
        Assert.True(second.Success);
        Assert.Equal("2621050002", second.EntityCode);
    }

    [Fact]
    public async Task Failed_insert_rolls_back_the_code_counter()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        // Mô phỏng dữ liệu cũ có mã nhưng chưa có bộ đếm tương ứng.
        fixture.Context.Students.Add(Student("2621050001", "Existing", "CNTT", 2026));
        await fixture.Context.SaveChangesAsync();

        var failed = await fixture.Service.CreateAsync(Form());

        Assert.False(failed.Success);
        Assert.Single(await fixture.Context.Students.ToListAsync());
        Assert.Empty(await fixture.Context.StudentCodeCounters.ToListAsync());
    }

    [Fact]
    public async Task Export_uses_the_list_filters_and_sorting_but_includes_all_matching_pages()
    {
        await using var fixture = await StudentFixture.CreateAsync();
        for (var i = 1; i <= 12; i++)
            fixture.Context.Students.Add(Student($"262105{i:D4}", $"Match {i:D2}", "CNTT", 2026));
        fixture.Context.Students.Add(Student("2521050001", "Match previous year", "CNTT", 2025));
        fixture.Context.Students.Add(Student("2621050013", "Match other department", "KT", 2026));
        fixture.Context.Students.Add(Student("2621050014", "Outside search", "CNTT", 2026));
        await fixture.Context.SaveChangesAsync();
        var filter = new StudentFilterViewModel
        {
            Q = "Match", DepartmentCode = "CNTT", AdmissionYear = 2026,
            Sort = "name", Dir = "desc", PageSize = 10
        };

        var page = await fixture.Service.GetPagedAsync(filter);
        var export = await fixture.Service.GetForExportAsync(filter);

        Assert.Equal(12, page.Students.TotalRecords);
        Assert.Equal(10, page.Students.Items.Count);
        Assert.Equal(12, export.Count);
        Assert.Equal("Match 12", export[0].FullName);
        Assert.Equal("Match 01", export[^1].FullName);
        Assert.Equal(page.Students.Items.Select(x => x.Id), export.Take(10).Select(x => x.Id));
        Assert.All(export, x => Assert.NotNull(x.Department));
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

    private static StudentFormViewModel Form(string? email = null) => new()
    {
        FullName = "Sinh viên kiểm thử", Address = "Hà Nội", DepartmentCode = "CNTT",
        University = "Đại học Mỏ - Địa chất", AdmissionYear = 2026,
        TrainingSystemCode = "21", MajorCode = "05", Email = email
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
