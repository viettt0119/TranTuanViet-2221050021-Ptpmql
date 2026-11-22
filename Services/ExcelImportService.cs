using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace DemoMVC.Services;

public sealed class ExcelImportService(ApplicationDbContext context)
{
    public async Task<ExcelImportResult> ImportAsync(
        Stream stream,
        string entity,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(stream, entity, cancellationToken);
        if (preview.Errors.Count > 0)
        {
            var errorResult = new ExcelImportResult { ErrorCount = preview.Errors.Count };
            foreach (var error in preview.Errors)
                errorResult.Errors.Add($"Dòng {error.Row}: {error.Message}");
            return errorResult;
        }
        if (!overwrite && preview.DuplicateCount > 0)
        {
            var duplicateResult = new ExcelImportResult
            {
                ErrorCount = preview.DuplicateCount
            };
            foreach (var row in preview.Rows)
            {
                var duplicate = await IsDuplicateAsync(preview.Entity, row.Values, cancellationToken);
                if (duplicate)
                    duplicateResult.Errors.Add($"Dòng {row.RowNumber}: dữ liệu trùng khoá.");
            }
            return duplicateResult;
        }
        var result = await ExecuteAsync(
            preview,
            overwrite ? ExcelImportMode.Overwrite : ExcelImportMode.Append,
            cancellationToken);
        return result;
    }
    public async Task<ExcelImportPreview> PreviewAsync(Stream stream, string entity, CancellationToken cancellationToken = default)
    {
        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        var preview = new ExcelImportPreview { Entity = entity.ToLowerInvariant() };
        if (worksheet is null)
        {
            preview.Errors.Add(new ImportRowError(0, "File Excel không có worksheet."));
            return preview;
        }
        var dimension = worksheet.Dimension;
        if (dimension is null)
        {
            preview.Errors.Add(new ImportRowError(0, "File Excel không có dữ liệu."));
            return preview;
        }

        var normalizedEntity = preview.Entity;
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= dimension.End.Column; column++)
        {
            var header = worksheet.Cells[1, column].Text.Trim();
            if (!string.IsNullOrWhiteSpace(header) && !headers.ContainsKey(header))
                headers[header] = column;
        }

        var requiredHeaders = normalizedEntity switch
        {
            "department" => new[] { "DepartmentCode", "DepartmentName" },
            "employee" => new[] { "EmpId", "FullName", "HireDate" },
            "product" => new[] { "Id", "Name", "Price" },
            "student" => new[] { "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode" },
            _ => Array.Empty<string>()
        };
        if (requiredHeaders.Length == 0)
        {
            preview.Errors.Add(new ImportRowError(0, "Loại dữ liệu không được hỗ trợ."));
            return preview;
        }
        foreach (var header in requiredHeaders)
        {
            if (!headers.ContainsKey(header))
                preview.Errors.Add(new ImportRowError(1, $"Thiếu cột bắt buộc: {header}"));
        }
        if (preview.Errors.Count > 0)
            return preview;

        preview.Total = Math.Max(0, dimension.End.Row - 1);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var row = 2; row <= dimension.End.Row; row++)
        {
            var values = headers.ToDictionary(
                pair => pair.Key,
                pair => worksheet.Cells[row, pair.Value].Text.Trim(),
                StringComparer.OrdinalIgnoreCase);

            var key = normalizedEntity switch
            {
                "department" => values.GetValueOrDefault("DepartmentCode", ""),
                "employee" => values.GetValueOrDefault("EmpId", ""),
                "product" => values.GetValueOrDefault("Id", ""),
                "student" => values.GetValueOrDefault("StudentCode", ""),
                _ => ""
            };

            try
            {
                ValidatePreviewRow(normalizedEntity, values);
                if (!string.IsNullOrWhiteSpace(key) && !seenKeys.Add(key))
                    throw new InvalidOperationException($"Khoá '{key}' bị trùng trong file.");

                var duplicate = await IsDuplicateAsync(normalizedEntity, values, cancellationToken);
                if (duplicate)
                    preview.DuplicateCount++;
                else
                    preview.NewCount++;

                preview.Rows.Add(new ExcelImportRow { RowNumber = row, Values = values });
                preview.Valid++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                preview.Errors.Add(new ImportRowError(row, ex.Message));
            }
        }
        return preview;
    }

    public async Task<ExcelImportResult> ExecuteAsync(
        ExcelImportPreview preview,
        ExcelImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var result = new ExcelImportResult();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in preview.Rows)
            {
                try
                {
                    var changed = await ExecuteRowAsync(preview.Entity, row, mode, cancellationToken);
                    if (changed == 1) result.AddedCount++;
                    else if (changed == 2) result.UpdatedCount++;
                    else result.SkippedCount++;
                    result.SuccessCount++;
                }
                catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or FormatException)
                {
                    throw new InvalidOperationException($"Dòng {row.RowNumber}: {ex.Message}", ex);
                }
            }
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private void ValidatePreviewRow(string entity, Dictionary<string, string> values)
    {
        switch (entity)
        {
            case "department":
                if (string.IsNullOrWhiteSpace(values.GetValueOrDefault("DepartmentCode")))
                    throw new InvalidOperationException("Mã Khoa là bắt buộc.");
                if (string.IsNullOrWhiteSpace(values.GetValueOrDefault("DepartmentName")))
                    throw new InvalidOperationException("Tên Khoa là bắt buộc.");
                break;
            case "employee":
                if (string.IsNullOrWhiteSpace(values.GetValueOrDefault("EmpId")) ||
                    string.IsNullOrWhiteSpace(values.GetValueOrDefault("FullName")))
                    throw new InvalidOperationException("Mã nhân viên và họ tên là bắt buộc.");
                if (!DateTime.TryParse(values.GetValueOrDefault("HireDate"), out _))
                    throw new FormatException("Ngày vào làm không hợp lệ.");
                break;
            case "product":
                if (!int.TryParse(values.GetValueOrDefault("Id"), out _) ||
                    !decimal.TryParse(values.GetValueOrDefault("Price"), out var price) ||
                    price is < 1000 or > 100000000)
                    throw new InvalidOperationException("ID hoặc giá sản phẩm không hợp lệ.");
                if (string.IsNullOrWhiteSpace(values.GetValueOrDefault("Name")) ||
                    values["Name"].Length is < 5 or > 100)
                    throw new InvalidOperationException("Tên sản phẩm phải từ 5 đến 100 ký tự.");
                break;
            case "student":
                if (string.IsNullOrWhiteSpace(values.GetValueOrDefault("FullName")) ||
                    string.IsNullOrWhiteSpace(values.GetValueOrDefault("Address")) ||
                    string.IsNullOrWhiteSpace(values.GetValueOrDefault("DepartmentCode")))
                    throw new InvalidOperationException("Họ tên, địa chỉ và mã Khoa là bắt buộc.");
                if (!int.TryParse(values.GetValueOrDefault("AdmissionYear"), out var year) || year is < 2000 or > 2099)
                    throw new InvalidOperationException("Năm nhập học phải từ 2000 đến 2099.");
                if (values.GetValueOrDefault("TrainingSystemCode") is not { Length: 2 } ||
                    values.GetValueOrDefault("MajorCode") is not { Length: 2 })
                    throw new InvalidOperationException("Mã hệ và mã ngành phải gồm 2 chữ số.");
                if (values.TryGetValue("Email", out var email) &&
                    !string.IsNullOrWhiteSpace(email) &&
                    !System.Net.Mail.MailAddress.TryCreate(email, out _))
                    throw new InvalidOperationException("Email không hợp lệ.");
                if (values.TryGetValue("PhoneNumber", out var phone) &&
                    !string.IsNullOrWhiteSpace(phone) &&
                    (phone.Length != 10 || !phone.StartsWith('0')))
                    throw new InvalidOperationException("Số điện thoại gồm 10 chữ số và bắt đầu bằng 0.");
                if (values.TryGetValue("DateOfBirth", out var dateOfBirth) &&
                    !string.IsNullOrWhiteSpace(dateOfBirth) &&
                    !DateOnly.TryParse(dateOfBirth, out _))
                    throw new InvalidOperationException("Ngày sinh không hợp lệ.");
                break;
        }
    }

    private async Task<bool> IsDuplicateAsync(
        string entity,
        Dictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        return entity switch
        {
            "department" => await context.Departments.AnyAsync(
                item => item.DepartmentCode == values["DepartmentCode"].ToUpperInvariant(), cancellationToken),
            "employee" => await context.Employees.AnyAsync(
                item => item.EmpId == values["EmpId"], cancellationToken),
            "product" => await context.Products.AnyAsync(
                item => item.Id == int.Parse(values["Id"]), cancellationToken),
            "student" => !string.IsNullOrWhiteSpace(values.GetValueOrDefault("StudentCode")) &&
                await context.Students.AnyAsync(
                    item => item.StudentCode == values["StudentCode"], cancellationToken),
            _ => false
        };
    }

    private async Task<int> ExecuteRowAsync(
        string entity,
        ExcelImportRow row,
        ExcelImportMode mode,
        CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case "department":
                return await ExecuteDepartmentAsync(row.Values, mode, cancellationToken);
            case "employee":
                return await ExecuteEmployeeAsync(row.Values, mode, cancellationToken);
            case "product":
                return await ExecuteProductAsync(row.Values, mode, cancellationToken);
            case "student":
                return await ExecuteStudentAsync(row.Values, mode, cancellationToken);
            default:
                throw new InvalidOperationException("Loại dữ liệu không được hỗ trợ.");
        }
    }

    private async Task<int> ExecuteDepartmentAsync(Dictionary<string, string> values, ExcelImportMode mode, CancellationToken cancellationToken)
    {
        var code = values["DepartmentCode"].Trim().ToUpperInvariant();
        var existing = await context.Departments.FindAsync([code], cancellationToken);
        if (existing is not null)
        {
            if (mode == ExcelImportMode.Append) return 0;
            existing.DepartmentName = values["DepartmentName"].Trim();
            return 2;
        }
        context.Departments.Add(new DepartmentModel
        {
            DepartmentCode = code,
            DepartmentName = values["DepartmentName"].Trim()
        });
        return 1;
    }

    private async Task<int> ExecuteEmployeeAsync(Dictionary<string, string> values, ExcelImportMode mode, CancellationToken cancellationToken)
    {
        var id = values["EmpId"].Trim();
        var existing = await context.Employees.FindAsync([id], cancellationToken);
        if (existing is not null)
        {
            if (mode == ExcelImportMode.Append) return 0;
            existing.FullName = values["FullName"].Trim();
            existing.HireDate = DateTime.Parse(values["HireDate"]);
            return 2;
        }
        context.Employees.Add(new EmployeeModel
        {
            EmpId = id,
            FullName = values["FullName"].Trim(),
            HireDate = DateTime.Parse(values["HireDate"])
        });
        return 1;
    }

    private async Task<int> ExecuteProductAsync(Dictionary<string, string> values, ExcelImportMode mode, CancellationToken cancellationToken)
    {
        var id = int.Parse(values["Id"]);
        var existing = await context.Products.FindAsync([id], cancellationToken);
        if (existing is not null)
        {
            if (mode == ExcelImportMode.Append) return 0;
            existing.Name = values["Name"].Trim();
            existing.Price = decimal.Parse(values["Price"]);
            return 2;
        }
        context.Products.Add(new ProductModel
        {
            Name = values["Name"].Trim(),
            Price = decimal.Parse(values["Price"])
        });
        return 1;
    }

    private async Task<int> ExecuteStudentAsync(Dictionary<string, string> values, ExcelImportMode mode, CancellationToken cancellationToken)
    {
        var code = values.GetValueOrDefault("StudentCode", "").Trim();
        var departmentCode = values["DepartmentCode"].Trim().ToUpperInvariant();
        if (!await context.Departments.AnyAsync(item => item.DepartmentCode == departmentCode, cancellationToken))
            throw new InvalidOperationException($"Khoa {departmentCode} không tồn tại.");

        var existing = string.IsNullOrWhiteSpace(code)
            ? null
            : await context.Students.FirstOrDefaultAsync(item => item.StudentCode == code, cancellationToken);
        if (existing is not null)
        {
            if (mode == ExcelImportMode.Append) return 0;
            existing.FullName = values["FullName"].Trim();
            existing.Address = values["Address"].Trim();
            existing.University = values["University"].Trim();
            existing.DepartmentCode = departmentCode;
            existing.Email = NormalizeImportOptional(values, "Email");
            existing.PhoneNumber = NormalizeImportOptional(values, "PhoneNumber");
            if (values.TryGetValue("DateOfBirth", out var existingDobText) &&
                DateOnly.TryParse(existingDobText, out var existingDob))
                existing.DateOfBirth = existingDob;
            if (values.TryGetValue("Gender", out var existingGenderText) &&
                Enum.TryParse<StudentGender>(existingGenderText, ignoreCase: true, out var existingGender))
                existing.Gender = existingGender;
            return 2;
        }

        var student = new StudentModel
        {
            FullName = values["FullName"].Trim(),
            Address = values["Address"].Trim(),
            University = values.GetValueOrDefault("University", "Đại học Mỏ - Địa chất").Trim(),
            DepartmentCode = departmentCode,
            AdmissionYear = int.Parse(values["AdmissionYear"]),
            TrainingSystemCode = values["TrainingSystemCode"].Trim(),
            MajorCode = values["MajorCode"].Trim(),
            StudentCode = code,
            Email = NormalizeImportOptional(values, "Email"),
            PhoneNumber = NormalizeImportOptional(values, "PhoneNumber")
        };
        if (values.TryGetValue("DateOfBirth", out var dateOfBirthText) &&
            DateOnly.TryParse(dateOfBirthText, out var dateOfBirth))
            student.DateOfBirth = dateOfBirth;
        if (values.TryGetValue("Gender", out var genderText) &&
            Enum.TryParse<StudentGender>(genderText, ignoreCase: true, out var gender))
            student.Gender = gender;
        if (string.IsNullOrWhiteSpace(student.StudentCode))
            await AssignNextStudentCodeAsync(student, cancellationToken);
        context.Students.Add(student);
        return 1;
    }

    private async Task AssignNextStudentCodeAsync(StudentModel student, CancellationToken cancellationToken)
    {
        var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "StudentCodeCounters"
                ("AdmissionYear", "TrainingSystemCode", "MajorCode", "LastNumber")
            VALUES ({student.AdmissionYear}, {student.TrainingSystemCode}, {student.MajorCode}, 1)
            ON CONFLICT ("AdmissionYear", "TrainingSystemCode", "MajorCode")
            DO UPDATE SET "LastNumber" = "LastNumber" + 1
            WHERE "LastNumber" < 9999;
            """, cancellationToken);
        if (changed == 0)
            throw new InvalidOperationException("Nhóm năm/hệ/ngành này đã cấp hết 9999 mã.");
        var number = await context.StudentCodeCounters
            .AsNoTracking()
            .Where(item => item.AdmissionYear == student.AdmissionYear &&
                item.TrainingSystemCode == student.TrainingSystemCode &&
                item.MajorCode == student.MajorCode)
            .Select(item => item.LastNumber)
            .SingleAsync(cancellationToken);
        student.StudentCode = $"{student.AdmissionYear % 100:D2}{student.TrainingSystemCode}{student.MajorCode}{number:D4}";
    }

    private static string? NormalizeImportOptional(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
