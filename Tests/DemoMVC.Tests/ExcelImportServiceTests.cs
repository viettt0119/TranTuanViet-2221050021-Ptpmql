using DemoMVC.Data;
using DemoMVC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Globalization;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mixed_student_import_reserves_explicit_codes_before_generating_codes(bool explicitFirst)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        string[] generated = ["", "Sinh vien tu sinh", "Ha Noi", "HUMG", "CNTT", "2028", "21", "05"];
        string[] explicitCode = ["2821050001", "Sinh vien co ma", "Ha Noi", "HUMG", "CNTT", "2028", "21", "05"];
        using var stream = Workbook(
            ["StudentCode", "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode"],
            explicitFirst ? explicitCode : generated, explicitFirst ? generated : explicitCode);
        var generator = new StudentCodeGenerator(context);
        var service = new ExcelImportService(context, generator);

        var preview = await service.PreviewAsync(stream, "student");
        Assert.Empty(preview.Errors);
        Assert.Equal(2, preview.NewCount);
        Assert.Empty(await context.Students.ToListAsync());
        Assert.Empty(await context.StudentCodeCounters.ToListAsync());

        var result = await service.ExecuteAsync(preview, Models.ExcelImportMode.Append);

        Assert.Equal(2, result.AddedCount);
        Assert.Equal("2821050002", (await context.Students.SingleAsync(x => x.FullName == "Sinh vien tu sinh")).StudentCode);
        Assert.Equal(2, (await context.StudentCodeCounters.SingleAsync()).LastNumber);
    }

    [Theory]
    [InlineData("01")]
    [InlineData("+1")]
    public async Task Preview_detects_equivalent_numeric_product_keys(string secondKey)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        using var stream = Workbook(["Id", "Name", "Price"], ["1", "San pham A", "1000"], [secondKey, "San pham B", "2000"]);
        var service = new ExcelImportService(context, new StudentCodeGenerator(context));

        var preview = await service.PreviewAsync(stream, "product");

        Assert.Equal(1, preview.Valid);
        Assert.Equal(3, Assert.Single(preview.Errors).Row);
        Assert.Contains("trùng trong file", preview.Errors[0].Message);
        var result = await service.ExecuteAsync(preview, Models.ExcelImportMode.Append);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal("San pham A", (await context.Products.SingleAsync()).Name);
    }

    [Fact]
    public async Task Appending_students_synchronizes_the_counter_even_for_skipped_existing_codes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Students.Add(new Models.StudentModel
        {
            StudentCode = "2821050001", FullName = "Ten cu", Address = "Ha Noi", DepartmentCode = "CNTT",
            AdmissionYear = 2028, TrainingSystemCode = "21", MajorCode = "05"
        });
        await context.SaveChangesAsync();
        using var stream = Workbook(
            ["StudentCode", "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode"],
            ["", "Sinh vien moi", "Ha Noi", "HUMG", "CNTT", "2028", "21", "05"],
            ["2821050001", "Khong ghi de", "Ha Noi", "HUMG", "CNTT", "2028", "21", "05"]);
        var service = new ExcelImportService(context, new StudentCodeGenerator(context));
        var preview = await service.PreviewAsync(stream, "student");

        var result = await service.ExecuteAsync(preview, Models.ExcelImportMode.Append);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("Ten cu", (await context.Students.SingleAsync(x => x.StudentCode == "2821050001")).FullName);
        Assert.Equal("2821050002", (await context.Students.SingleAsync(x => x.FullName == "Sinh vien moi")).StudentCode);
        Assert.Equal(2, (await context.StudentCodeCounters.SingleAsync()).LastNumber);
    }

    [Theory]
    [InlineData("en-US", false)]
    [InlineData("vi-VN", false)]
    [InlineData("vi-VN", true)]
    public async Task Product_prices_are_independent_of_server_culture_and_excel_display_format(string culture, bool numericCell)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var context = CreateContext(connection);
            await context.Database.EnsureCreatedAsync();
            using var stream = new MemoryStream();
            using (var package = new ExcelPackage(stream))
            {
                var sheet = package.Workbook.Worksheets.Add("Data");
                sheet.Cells[1, 1].Value = "Id";
                sheet.Cells[1, 2].Value = "Name";
                sheet.Cells[1, 3].Value = "Price";
                sheet.Cells[2, 1].Value = 1;
                sheet.Cells[2, 2].Value = "San pham A";
                sheet.Cells[2, 3].Value = numericCell ? 1234.50m : "1234.50";
                if (numericCell) sheet.Cells[2, 3].Style.Numberformat.Format = "#,##0 \"VND\"";
                package.Save();
            }
            stream.Position = 0;
            var service = new ExcelImportService(context, new StudentCodeGenerator(context));
            var preview = await service.PreviewAsync(stream, "product");
            Assert.Empty(preview.Errors);

            await service.ExecuteAsync(preview, Models.ExcelImportMode.Append);

            Assert.Equal(1234.50m, (await context.Products.SingleAsync()).Price);
            var row = Assert.Single(preview.Rows);
            row.IsExisting = true;
            row.ExistingValues = new(StringComparer.OrdinalIgnoreCase) { ["Name"] = "San pham A", ["Price"] = "1234.5" };
            Assert.Empty(ExcelImportService.DescribeOverwrite("product", row));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("1234,50")]
    [InlineData("1,234.50")]
    public async Task Preview_rejects_ambiguous_text_prices_instead_of_changing_the_amount(string price)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        using var stream = Workbook(["Id", "Name", "Price"], ["1", "San pham A", price]);
        var service = new ExcelImportService(context, new StudentCodeGenerator(context));

        var preview = await service.PreviewAsync(stream, "product");

        Assert.Single(preview.Errors);
        Assert.Empty(preview.Rows);
        Assert.Empty(await context.Products.ToListAsync());
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

    [Fact]
    public async Task Overwriting_a_student_keeps_columns_absent_from_the_file_and_invalidates_open_forms()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var original = new Models.StudentModel
        {
            StudentCode = "2821050199", FullName = "Tên cũ", Address = "Ha Noi", DepartmentCode = "CNTT",
            AdmissionYear = 2028, TrainingSystemCode = "21", MajorCode = "05",
            Email = "giu@example.test", PhoneNumber = "0900000001"
        };
        context.Students.Add(original);
        await context.SaveChangesAsync();
        var stampBeforeImport = original.ConcurrencyStamp;
        context.ChangeTracker.Clear();

        using var stream = Workbook(
            ["StudentCode", "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode"],
            ["2821050199", "Tên mới", "Ha Noi", "Đại học Mỏ - Địa chất", "CNTT", "2028", "21", "05"]);
        var service = new ExcelImportService(context, new StudentCodeGenerator(context));
        var preview = await service.PreviewAsync(stream, "student");

        var change = Assert.Single(ExcelImportService.DescribeOverwrite("student", Assert.Single(preview.Rows)));
        Assert.Equal(new Models.ImportFieldChange("FullName", "Tên cũ", "Tên mới"), change);

        await service.ExecuteAsync(preview, Models.ExcelImportMode.Overwrite);
        context.ChangeTracker.Clear();
        var stored = await context.Students.SingleAsync();
        Assert.Equal("Tên mới", stored.FullName);
        Assert.Equal("giu@example.test", stored.Email);
        Assert.Equal("0900000001", stored.PhoneNumber);
        Assert.NotEqual(stampBeforeImport, stored.ConcurrencyStamp);
    }

    [Theory]
    [InlineData("1000", false)]
    [InlineData("1000.00", false)]
    [InlineData("2000", true)]
    public void Overwrite_description_compares_prices_by_value(string filePrice, bool changed)
    {
        var row = new Models.ExcelImportRow
        {
            IsExisting = true,
            Values = new(StringComparer.OrdinalIgnoreCase) { ["Id"] = "1", ["Name"] = "San pham", ["Price"] = filePrice },
            ExistingValues = new(StringComparer.OrdinalIgnoreCase) { ["Id"] = "1", ["Name"] = "San pham", ["Price"] = "1000" }
        };

        var changes = ExcelImportService.DescribeOverwrite("product", row);

        Assert.Equal(changed, changes.Any(item => item.Field == "Price"));
    }

    [Theory]
    [InlineData("employee", "EmpId,FullName,HireDate", "NV1,Nguyen Van A,khong-phai-ngay")]
    [InlineData("product", "Id,Name,Price", "1,Ten san pham,999")]
    [InlineData("department", "DepartmentCode,DepartmentName", "ma sai,Khoa")]
    public async Task Preview_applies_the_same_rules_as_the_forms(string entity, string headers, string row)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        using var stream = Workbook(headers.Split(','), row.Split(','));

        var preview = await new ExcelImportService(context, new StudentCodeGenerator(context)).PreviewAsync(stream, entity);

        Assert.Single(preview.Errors);
        Assert.Empty(preview.Rows);
    }

    [Theory]
    [InlineData("0abcdefghi")]
    [InlineData("1234567890")]
    public async Task Preview_rejects_phone_numbers_the_student_form_rejects(string phone)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        using var stream = Workbook(
            ["FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode", "PhoneNumber"],
            ["Nguyen Van A", "Ha Noi", "Đại học Mỏ - Địa chất", "CNTT", "2028", "21", "05", phone]);

        var preview = await new ExcelImportService(context, new StudentCodeGenerator(context)).PreviewAsync(stream, "student");

        Assert.Contains("Số điện thoại", Assert.Single(preview.Errors).Message);
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
