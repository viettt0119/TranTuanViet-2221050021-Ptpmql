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

        var service = new ExcelImportService(context, new StudentCodeGenerator(context));
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

        var service = new ExcelImportService(context, new StudentCodeGenerator(context));
        var result = await service.ImportAsync(stream, "department", overwrite: true);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal("Mới", (await context.Departments.FindAsync("TEST"))!.DepartmentName);
    }

    [Fact]
    public async Task Imported_student_code_advances_counter_so_next_generated_code_does_not_collide()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        using var stream = Workbook(
            ["StudentCode", "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode"],
            ["2821050199", "Nguyen Van A", "Ha Noi", "Đại học Mỏ - Địa chất", "CNTT", "2028", "21", "05"]);

        var generator = new StudentCodeGenerator(context);
        var result = await new ExcelImportService(context, generator).ImportAsync(stream, "student", overwrite: false);
        Assert.Equal(0, result.ErrorCount);

        var next = new Models.StudentModel
        {
            FullName = "Tran Thi B", Address = "Ha Noi", DepartmentCode = "CNTT",
            AdmissionYear = 2028, TrainingSystemCode = "21", MajorCode = "05"
        };
        Assert.Equal("2821050200", await generator.CreateAsync(next));
    }

    [Fact]
    public async Task Student_code_not_matching_year_system_major_is_rejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        using var stream = Workbook(
            ["StudentCode", "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode"],
            ["2721050001", "Nguyen Van A", "Ha Noi", "Đại học Mỏ - Địa chất", "CNTT", "2028", "21", "05"]);

        var preview = await new ExcelImportService(context, new StudentCodeGenerator(context)).PreviewAsync(stream, "student");

        Assert.Contains(preview.Errors, error => error.Message.Contains("282105"));
    }

    [Fact]
    public async Task Import_role_permissions_appends_and_overwrites_grants()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var role = new Microsoft.AspNetCore.Identity.IdentityRole { Id = "editor", Name = "Editor", NormalizedName = "EDITOR" };
        context.Roles.Add(role);
        var index = new Models.ApplicationPermission { Key = "Student.Index", Controller = "Student", Action = "Index" };
        var create = new Models.ApplicationPermission { Key = "Student.Create", Controller = "Student", Action = "Create" };
        context.ApplicationPermissions.AddRange(index, create);
        await context.SaveChangesAsync();
        context.ApplicationRolePermissions.Add(new Models.ApplicationRolePermission { RoleId = role.Id, PermissionId = index.Id, IsGranted = true });
        await context.SaveChangesAsync();

        var service = new ExcelImportService(context, new StudentCodeGenerator(context));
        string[] headers = ["RoleName", "Permission", "IsGranted"];

        // Append: dòng trùng (Student.Index) bị bỏ qua, quyền mới được thêm.
        using (var stream = Workbook(headers, ["editor", "Student.Index", "0"], ["Editor", "Student.Create", ""]))
        {
            var preview = await service.PreviewAsync(stream, "rolepermission");
            Assert.Empty(preview.Errors);
            Assert.Equal(1, preview.DuplicateCount);
            var result = await service.ExecuteAsync(preview, Models.ExcelImportMode.Append);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(1, result.SkippedCount);
        }
        Assert.True((await context.ApplicationRolePermissions.SingleAsync(x => x.PermissionId == index.Id)).IsGranted);
        Assert.True((await context.ApplicationRolePermissions.SingleAsync(x => x.PermissionId == create.Id)).IsGranted);

        // Overwrite: dòng trùng được cập nhật theo IsGranted.
        using (var stream = Workbook(headers, ["Editor", "Student.Index", "0"]))
        {
            var preview = await service.PreviewAsync(stream, "rolepermission");
            var result = await service.ExecuteAsync(preview, Models.ExcelImportMode.Overwrite);
            Assert.Equal(1, result.UpdatedCount);
        }
        context.ChangeTracker.Clear();
        Assert.False((await context.ApplicationRolePermissions.SingleAsync(x => x.PermissionId == index.Id)).IsGranted);

        // Không nhập cho Admin, role hoặc quyền không tồn tại.
        using (var stream = Workbook(headers, ["Admin", "Student.Index", "1"], ["Ghost", "Student.Index", "1"], ["Editor", "Nope.Index", "1"]))
        {
            var preview = await service.PreviewAsync(stream, "rolepermission");
            Assert.Equal(3, preview.Errors.Count);
        }
    }

    private static MemoryStream Workbook(string[] headers, params string[][] rows)
    {
        var stream = new MemoryStream();
        using (var package = new ExcelPackage(stream))
        {
            var sheet = package.Workbook.Worksheets.Add("Data");
            for (var column = 0; column < headers.Length; column++)
                sheet.Cells[1, column + 1].Value = headers[column];
            for (var row = 0; row < rows.Length; row++)
                for (var column = 0; column < rows[row].Length; column++)
                    sheet.Cells[row + 2, column + 1].Value = rows[row][column];
            package.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(options);
    }
}
