using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace DemoMVC.Tests;

public class DemoDataSeederTests
{
    [Theory]
    [InlineData(nameof(Environments.Production), null)]
    [InlineData(nameof(Environments.Staging), null)]
    [InlineData(nameof(Environments.Development), false)]
    public async Task SeedAsync_when_disabled_does_not_reinsert_deleted_samples(
        string environmentName, bool? enabled)
    {
        await using var fixture = await SeedFixture.CreateAsync();
        await fixture.Seeder.SeedAsync();
        var deletedStudent = await fixture.Context.Students.FirstAsync();
        var deletedEmployee = await fixture.Context.Employees.FirstAsync();
        fixture.Context.Students.Remove(deletedStudent);
        fixture.Context.Employees.Remove(deletedEmployee);
        await fixture.Context.SaveChangesAsync();

        await fixture.CreateSeeder(environmentName, enabled).SeedAsync();

        Assert.Equal(19, await fixture.Context.Students.CountAsync());
        Assert.Equal(19, await fixture.Context.Employees.CountAsync());
        Assert.False(await fixture.Context.Students.AnyAsync(x => x.Email == deletedStudent.Email));
        Assert.False(await fixture.Context.Employees.AnyAsync(x => x.EmpId == deletedEmployee.EmpId));
    }

    [Fact]
    public async Task SeedAsync_allows_explicit_opt_in_outside_development()
    {
        await using var fixture = await SeedFixture.CreateAsync();

        await fixture.CreateSeeder(Environments.Production, true).SeedAsync();

        Assert.Equal(20, await fixture.Context.Students.CountAsync());
        Assert.Equal(20, await fixture.Context.Employees.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_adds_twenty_sample_students_and_employees_once_without_changing_departments()
    {
        await using var fixture = await SeedFixture.CreateAsync();
        fixture.Context.Students.Add(new StudentModel
        {
            StudentCode = "2699990007",
            FullName = "Sinh viên hiện có",
            Address = "Địa chỉ hiện có",
            University = "Đại học Mỏ - Địa chất",
            DepartmentCode = "MO",
            AdmissionYear = 2026,
            TrainingSystemCode = "99",
            MajorCode = "99",
            Email = "existing.student@example.test"
        });
        fixture.Context.Employees.Add(new EmployeeModel
        {
            EmpId = "EMP-EXISTING",
            FullName = "Nhân viên hiện có",
            HireDate = new DateTime(2020, 1, 1)
        });
        await fixture.Context.SaveChangesAsync();

        var departmentCodesBefore = await fixture.Context.Departments
            .OrderBy(department => department.DepartmentCode)
            .Select(department => department.DepartmentCode)
            .ToListAsync();

        await fixture.Seeder.SeedAsync();
        await fixture.Seeder.SeedAsync();

        var sampleStudents = await fixture.Context.Students
            .Where(student => student.Email != null && student.Email.StartsWith("student.sample."))
            .ToListAsync();
        var sampleEmployees = await fixture.Context.Employees
            .Where(employee => employee.EmpId.StartsWith("DEMO-EMP-"))
            .ToListAsync();

        Assert.Equal(20, sampleStudents.Count);
        Assert.Equal(20, sampleEmployees.Count);
        Assert.Equal(21, await fixture.Context.Students.CountAsync());
        Assert.Equal(21, await fixture.Context.Employees.CountAsync());
        Assert.Equal(
            departmentCodesBefore,
            await fixture.Context.Departments
                .OrderBy(department => department.DepartmentCode)
                .Select(department => department.DepartmentCode)
                .ToListAsync());
        Assert.All(sampleStudents, student =>
        {
            Assert.StartsWith("269999", student.StudentCode);
            Assert.Contains(departmentCodesBefore, code => code == student.DepartmentCode);
        });
        Assert.Equal(20, sampleStudents.Select(student => student.StudentCode).Distinct().Count());
        Assert.DoesNotContain(sampleStudents, student => student.StudentCode == "2699990007");
    }

    [Fact]
    public async Task SeedAsync_fails_explicitly_when_no_departments_exist()
    {
        await using var fixture = await SeedFixture.CreateAsync();
        fixture.Context.Departments.RemoveRange(fixture.Context.Departments);
        await fixture.Context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Seeder.SeedAsync());

        Assert.Contains("no departments exist", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SeedFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public ApplicationDbContext Context { get; }
        public DemoDataSeeder Seeder { get; }

        private SeedFixture(SqliteConnection connection, ApplicationDbContext context)
        {
            _connection = connection;
            Context = context;
            Seeder = CreateSeeder(Environments.Development);
        }

        public DemoDataSeeder CreateSeeder(string environmentName, bool? enabled = null) =>
            new(Context, new StudentCodeGenerator(Context),
                Options.Create(new DemoDataSeedOptions { Enabled = enabled }),
                new SeedEnvironment { EnvironmentName = environmentName });

        public static async Task<SeedFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new SeedFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class SeedEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "DemoMVC.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
