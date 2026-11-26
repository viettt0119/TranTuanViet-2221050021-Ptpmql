using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DemoMVC.Tests;

public class DemoDataSeederTests
{
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
            Seeder = new DemoDataSeeder(context, new StudentCodeGenerator(context));
        }

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
}
