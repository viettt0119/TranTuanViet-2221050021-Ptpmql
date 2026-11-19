using DemoMVC.Data;
using DemoMVC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Xunit;

namespace DemoMVC.Tests;

public class ExcelImportServiceTests
{
    static ExcelImportServiceTests()
    {
        ExcelPackage.License.SetNonCommercialOrganization("DemoMVC Tests");
    }

    [Fact]
    public async Task Import_department_returns_error_count_for_duplicate_without_overwrite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Departments.Add(new Models.DepartmentModel { DepartmentCode = "TEST", DepartmentName = "Cũ" });
        await context.SaveChangesAsync();

        using var stream = new MemoryStream();
        using (var package = new ExcelPackage(stream))
        {
            var sheet = package.Workbook.Worksheets.Add("Departments");
            sheet.Cells[1, 1].Value = "DepartmentCode";
            sheet.Cells[1, 2].Value = "DepartmentName";
            sheet.Cells[2, 1].Value = "TEST";
            sheet.Cells[2, 2].Value = "Mới";
            package.Save();
        }
        stream.Position = 0;

        var service = new ExcelImportService(context);
        var result = await service.ImportAsync(stream, "department", overwrite: false);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Contains("trùng khoá", result.Errors[0]);
    }

    [Fact]
    public async Task Import_department_overwrites_existing_row()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Departments.Add(new Models.DepartmentModel { DepartmentCode = "TEST", DepartmentName = "Cũ" });
        await context.SaveChangesAsync();

        using var stream = new MemoryStream();
        using (var package = new ExcelPackage(stream))
        {
            var sheet = package.Workbook.Worksheets.Add("Departments");
            sheet.Cells[1, 1].Value = "DepartmentCode";
            sheet.Cells[1, 2].Value = "DepartmentName";
            sheet.Cells[2, 1].Value = "TEST";
            sheet.Cells[2, 2].Value = "Mới";
            package.Save();
        }
        stream.Position = 0;

        var service = new ExcelImportService(context);
        var result = await service.ImportAsync(stream, "department", overwrite: true);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal("Mới", (await context.Departments.FindAsync("TEST"))!.DepartmentName);
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(options);
    }
}
