using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.ViewModels.Students;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace DemoMVC.Services;

public sealed class ExcelImportService(ApplicationDbContext context, StudentCodeGenerator studentCodeGenerator)
{
    public const int MaximumRows = 2000;
    public static IReadOnlyList<string> SupportedEntities { get; } =
        ["department", "employee", "product", "student", "rolepermission"];

    public static bool IsSupportedEntity(string? entity) =>
        entity is not null && SupportedEntities.Contains(entity);

    /// <summary>
    /// Các trường sẽ thực sự đổi nếu ghi đè dòng trùng này, so sánh sau khi chuẩn hoá giống lúc lưu.
    /// Cột không có trong file thì giữ nguyên dữ liệu cũ nên không xuất hiện ở đây.
    /// </summary>
    public static IReadOnlyList<ImportFieldChange> DescribeOverwrite(string entity, ExcelImportRow row)
    {
        if (!row.IsExisting)
            return [];

        string[] fields = entity switch
        {
            "department" => ["DepartmentName"],
            "employee" => ["FullName", "HireDate"],
            "product" => ["Name", "Price"],
            "student" => ["FullName", "Address", "University", "DepartmentCode", "Email", "PhoneNumber", "DateOfBirth", "Gender"],
            "rolepermission" => ["IsGranted"],
            _ => []
        };

        var changes = new List<ImportFieldChange>();
        foreach (var field in fields)
        {
            // IsGranted vắng mặt nghĩa là "cấp quyền" (xem ParseGranted), không phải giữ nguyên.
            if (!row.Values.ContainsKey(field) && field != "IsGranted")
                continue;

            var oldValue = row.ExistingValues.GetValueOrDefault(field, "");
            var newValue = row.Values.GetValueOrDefault(field, "").Trim();
            var unchanged = false;
            switch (field)
            {
                case "HireDate" when DateTime.TryParse(newValue, out var hireDate):
                    newValue = hireDate.ToString("yyyy-MM-dd");
                    break;
                case "DateOfBirth" when DateOnly.TryParse(newValue, out var dateOfBirth):
                    newValue = dateOfBirth.ToString("yyyy-MM-dd");
                    break;
                case "Gender" when Enum.TryParse<StudentGender>(newValue, true, out var gender):
                    newValue = gender.ToString();
                    break;
                case "Price" when TryParsePrice(newValue, out var price):
                    unchanged = TryParsePrice(oldValue, out var oldPrice) && oldPrice == price;
                    newValue = price.ToString(CultureInfo.InvariantCulture);
                    break;
                case "IsGranted":
                    newValue = ParseGranted(row.Values) ? "1" : "0";
                    break;
            }

            if (!unchanged && !string.Equals(oldValue, newValue, StringComparison.Ordinal))
                changes.Add(new ImportFieldChange(field, oldValue, newValue));
        }
        return changes;
    }

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
        var preview = new ExcelImportPreview { Entity = entity.Trim().ToLowerInvariant() };
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
        if (dimension.End.Row > MaximumRows + 1 || dimension.End.Column > 30)
        {
            preview.Errors.Add(new ImportRowError(0, $"Mỗi file tối đa {MaximumRows} dòng dữ liệu và 30 cột."));
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
            "rolepermission" => new[] { "RoleName", "Permission" },
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

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var row = 2; row <= dimension.End.Row; row++)
        {
            var values = headers.ToDictionary(
                pair => pair.Key,
                pair => ReadCellValue(worksheet.Cells[row, pair.Value], pair.Key),
                StringComparer.OrdinalIgnoreCase);
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            preview.Total++;
            if (values.ContainsKey("DepartmentCode")) values["DepartmentCode"] = values["DepartmentCode"].ToUpperInvariant();
            if (values.ContainsKey("Email")) values["Email"] = values["Email"].ToLowerInvariant();

            var key = normalizedEntity switch
            {
                "department" => values.GetValueOrDefault("DepartmentCode", ""),
                "employee" => values.GetValueOrDefault("EmpId", ""),
                "product" => int.TryParse(values.GetValueOrDefault("Id"), out var productId)
                    ? productId.ToString(CultureInfo.InvariantCulture) : values.GetValueOrDefault("Id", ""),
                "student" => values.GetValueOrDefault("StudentCode", ""),
                "rolepermission" => $"{values.GetValueOrDefault("RoleName", "")}|{values.GetValueOrDefault("Permission", "")}",
                _ => ""
            };

            try
            {
                ValidatePreviewRow(normalizedEntity, values);
                await ValidatePreviewRowAsync(normalizedEntity, values, cancellationToken);
                if (!string.IsNullOrWhiteSpace(key) && !seenKeys.Add(key))
                    throw new InvalidOperationException($"Khoá '{key}' bị trùng trong file.");
                if (normalizedEntity == "student" && values.TryGetValue("Email", out var email) &&
                    !string.IsNullOrEmpty(email) && !seenEmails.Add(email))
                    throw new InvalidOperationException("Email bị trùng trong file.");

                var importRow = new ExcelImportRow { RowNumber = row, Values = values };
                await CaptureExistingAsync(normalizedEntity, importRow, cancellationToken);
                if (importRow.IsExisting)
                    preview.DuplicateCount++;
                else
                    preview.NewCount++;

                preview.Rows.Add(importRow);
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
        if (!Enum.IsDefined(mode) || !IsSupportedEntity(preview.Entity))
            throw new InvalidOperationException("Loại dữ liệu hoặc chế độ nhập không hợp lệ.");
        var result = new ExcelImportResult { ErrorCount = preview.Errors.Count };
        result.Errors.AddRange(preview.Errors.Select(error => $"Dòng {error.Row}: {error.Message}"));
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Lưu các mã có sẵn trước để nâng bộ đếm, tránh dòng tự sinh chiếm mã của dòng sau trong file.
            // OrderBy ổn định nên giữ thứ tự tương đối giữa các dòng tự sinh và trong từng nhóm mã có sẵn.
            var rows = preview.Entity == "student"
                ? preview.Rows.OrderBy(row => string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("StudentCode")))
                : preview.Rows.AsEnumerable();
            foreach (var row in rows)
            {
                try
                {
                    ValidatePreviewRow(preview.Entity, row.Values);
                    await ValidatePreviewRowAsync(preview.Entity, row.Values, cancellationToken);
                    var current = new ExcelImportRow { Values = row.Values };
                    await CaptureExistingAsync(preview.Entity, current, cancellationToken);
                    if (current.IsExisting != row.IsExisting ||
                        (mode == ExcelImportMode.Overwrite && row.IsExisting &&
                         (current.ConcurrencyStamp != row.ConcurrencyStamp ||
                          current.ExistingValues.Count != row.ExistingValues.Count ||
                          current.ExistingValues.Any(pair => row.ExistingValues.GetValueOrDefault(pair.Key) != pair.Value))))
                        throw new InvalidOperationException("Dữ liệu đã thay đổi từ lúc xem trước. Vui lòng tải file lên lại.");
                    var changed = await ExecuteRowAsync(preview.Entity, row, mode, cancellationToken);
                    if (changed == 1) result.AddedCount++;
                    else if (changed == 2) result.UpdatedCount++;
                    else result.SkippedCount++;
                    if (changed != 0) result.SuccessCount++;
                    // Lưu theo dòng trong cùng transaction để kiểm tra ràng buộc và báo đúng dòng lỗi.
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or FormatException)
                {
                    var message = ex is DbUpdateException
                        ? "Không thể lưu do dữ liệu trùng hoặc đã thay đổi. Toàn bộ lần nhập đã được hoàn tác."
                        : ex.Message;
                    throw new InvalidOperationException($"Dòng {row.RowNumber}: {message}", ex);
                }
            }
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    private void ValidatePreviewRow(string entity, Dictionary<string, string> values)
    {
        switch (entity)
        {
            case "department":
                ValidateModel(new DepartmentModel
                {
                    DepartmentCode = values.GetValueOrDefault("DepartmentCode", "").ToUpperInvariant(),
                    DepartmentName = values.GetValueOrDefault("DepartmentName", "")
                });
                break;
            case "employee":
                if (!DateTime.TryParse(values.GetValueOrDefault("HireDate"), out var hireDate))
                    throw new FormatException("Ngày vào làm không hợp lệ.");
                ValidateModel(new EmployeeModel
                {
                    EmpId = values.GetValueOrDefault("EmpId", ""),
                    FullName = values.GetValueOrDefault("FullName", ""), HireDate = hireDate
                });
                break;
            case "product":
                if (!int.TryParse(values.GetValueOrDefault("Id"), out var productId) || productId <= 0 ||
                    !TryParsePrice(values.GetValueOrDefault("Price"), out var price))
                    throw new InvalidOperationException("ID hoặc giá sản phẩm không hợp lệ.");
                ValidateModel(new ProductModel { Id = productId, Name = values.GetValueOrDefault("Name"), Price = price });
                break;
            case "student":
                if (!int.TryParse(values.GetValueOrDefault("AdmissionYear"), out var year) || year is < 2000 or > 2099)
                    throw new InvalidOperationException("Năm nhập học phải từ 2000 đến 2099.");
                var trainingSystemCode = values.GetValueOrDefault("TrainingSystemCode") ?? "";
                var majorCode = values.GetValueOrDefault("MajorCode") ?? "";
                if (trainingSystemCode.Length != 2 || !trainingSystemCode.All(char.IsAsciiDigit) ||
                    majorCode.Length != 2 || !majorCode.All(char.IsAsciiDigit))
                    throw new InvalidOperationException("Mã hệ và mã ngành phải gồm 2 chữ số.");
                var studentCode = values.GetValueOrDefault("StudentCode");
                if (!string.IsNullOrWhiteSpace(studentCode))
                {
                    var prefix = StudentCodeGenerator.Prefix(year, trainingSystemCode, majorCode);
                    if (!StudentCodeGenerator.TryGetSequence(studentCode, out _))
                        throw new InvalidOperationException("Mã sinh viên phải gồm đúng 10 chữ số (để trống để hệ thống tự sinh).");
                    if (!studentCode.StartsWith(prefix, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Mã sinh viên {studentCode} không khớp năm/hệ/ngành của dòng (phải bắt đầu bằng {prefix}).");
                }
                ValidateModel(new StudentFormViewModel
                {
                    FullName = values.GetValueOrDefault("FullName", ""),
                    Address = values.GetValueOrDefault("Address", ""),
                    University = values.GetValueOrDefault("University", ""),
                    DepartmentCode = values.GetValueOrDefault("DepartmentCode", ""),
                    AdmissionYear = year, TrainingSystemCode = trainingSystemCode, MajorCode = majorCode,
                    Email = NormalizeImportOptional(values, "Email"),
                    PhoneNumber = NormalizeImportOptional(values, "PhoneNumber"),
                    DateOfBirth = ParseDateOfBirth(values), Gender = ParseGender(values)
                });
                break;
        }
    }

    private static void ValidateModel(object model)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), errors, true))
            throw new InvalidOperationException(string.Join(" ", errors.Select(error => error.ErrorMessage)));
    }

    private static DateOnly? ParseDateOfBirth(Dictionary<string, string> values)
    {
        var text = NormalizeImportOptional(values, "DateOfBirth");
        if (text is null) return null;
        return DateOnly.TryParse(text, out var date)
            ? date : throw new InvalidOperationException("Ngày sinh không hợp lệ.");
    }

    private static StudentGender? ParseGender(Dictionary<string, string> values)
    {
        var text = NormalizeImportOptional(values, "Gender");
        if (text is null) return null;
        return Enum.TryParse<StudentGender>(text, true, out var gender) && Enum.IsDefined(gender)
            ? gender : throw new InvalidOperationException("Giới tính chỉ nhận Nam, Nữ hoặc Khác.");
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
            "rolepermission" => await FindRolePermissionAsync(values, cancellationToken) is not null,
            _ => false
        };
    }

    private async Task CaptureExistingAsync(string entity, ExcelImportRow row, CancellationToken cancellationToken)
    {
        var values = row.Values;
        switch (entity)
        {
            case "department":
                var department = await context.Departments.AsNoTracking().FirstOrDefaultAsync(
                    x => x.DepartmentCode == values["DepartmentCode"].ToUpperInvariant(), cancellationToken);
                if (department is not null)
                    row.ExistingValues = Snapshot(("DepartmentCode", department.DepartmentCode), ("DepartmentName", department.DepartmentName));
                break;
            case "employee":
                var employee = await context.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.EmpId == values["EmpId"], cancellationToken);
                if (employee is not null)
                    row.ExistingValues = Snapshot(("EmpId", employee.EmpId), ("FullName", employee.FullName), ("HireDate", employee.HireDate.ToString("yyyy-MM-dd")));
                break;
            case "product":
                var id = int.Parse(values["Id"]);
                var product = await context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (product is not null)
                    row.ExistingValues = Snapshot(("Id", product.Id.ToString()), ("Name", product.Name), ("Price", product.Price.ToString(CultureInfo.InvariantCulture)));
                break;
            case "student":
                var code = values.GetValueOrDefault("StudentCode", "");
                var student = await context.Students.AsNoTracking().FirstOrDefaultAsync(x => x.StudentCode == code, cancellationToken);
                if (student is not null)
                {
                    row.ConcurrencyStamp = student.ConcurrencyStamp;
                    row.ExistingValues = Snapshot(("StudentCode", student.StudentCode), ("FullName", student.FullName),
                        ("Address", student.Address), ("University", student.University), ("DepartmentCode", student.DepartmentCode),
                        ("AdmissionYear", student.AdmissionYear.ToString()), ("TrainingSystemCode", student.TrainingSystemCode),
                        ("MajorCode", student.MajorCode), ("Email", student.Email), ("PhoneNumber", student.PhoneNumber),
                        ("DateOfBirth", student.DateOfBirth?.ToString("yyyy-MM-dd")), ("Gender", student.Gender?.ToString()));
                }
                break;
            case "rolepermission":
                var roleName = values["RoleName"].ToUpperInvariant();
                var permission = values["Permission"];
                var grant = await context.ApplicationRolePermissions.AsNoTracking().FirstOrDefaultAsync(
                    x => x.Role.NormalizedName == roleName && x.Permission.Key == permission, cancellationToken);
                if (grant is not null)
                    row.ExistingValues = Snapshot(("RoleName", values["RoleName"]), ("Permission", permission), ("IsGranted", grant.IsGranted ? "1" : "0"));
                break;
        }
        row.IsExisting = row.ExistingValues.Count > 0;
    }

    private static Dictionary<string, string> Snapshot(params (string Key, string? Value)[] fields) =>
        fields.ToDictionary(field => field.Key, field => field.Value ?? "", StringComparer.OrdinalIgnoreCase);

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
            case "rolepermission":
                return await ExecuteRolePermissionAsync(row.Values, mode, cancellationToken);
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
        if (!TryParsePrice(values["Price"], out var price))
            throw new InvalidOperationException("Giá sản phẩm không hợp lệ. Dùng dấu chấm cho phần thập phân và không dùng dấu phân cách hàng nghìn.");
        var existing = await context.Products.FindAsync([id], cancellationToken);
        if (existing is not null)
        {
            if (mode == ExcelImportMode.Append) return 0;
            existing.Name = values["Name"].Trim();
            existing.Price = price;
            return 2;
        }
        context.Products.Add(new ProductModel
        {
            Id = id,
            Name = values["Name"].Trim(),
            Price = price
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
            if (mode == ExcelImportMode.Append)
            {
                // Dữ liệu cũ có thể chưa đồng bộ bộ đếm; dòng tự sinh sau đó vẫn phải tránh mã đã có.
                await studentCodeGenerator.ReserveExistingCodeAsync(existing, cancellationToken);
                return 0;
            }
            existing.FullName = values["FullName"].Trim();
            existing.Address = values["Address"].Trim();
            existing.University = values["University"].Trim();
            existing.DepartmentCode = departmentCode;
            if (values.ContainsKey("Email")) existing.Email = NormalizeImportOptional(values, "Email")?.ToLowerInvariant();
            if (values.ContainsKey("PhoneNumber")) existing.PhoneNumber = NormalizeImportOptional(values, "PhoneNumber");
            if (values.ContainsKey("DateOfBirth")) existing.DateOfBirth = ParseDateOfBirth(values);
            if (values.ContainsKey("Gender")) existing.Gender = ParseGender(values);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.ConcurrencyStamp = Guid.NewGuid();
            // Mã cũ có thể được nhập trước khi có cơ chế đồng bộ bộ đếm: đảm bảo bộ đếm không tụt sau mã này.
            await studentCodeGenerator.ReserveExistingCodeAsync(existing, cancellationToken);
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
            Email = NormalizeImportOptional(values, "Email")?.ToLowerInvariant(),
            PhoneNumber = NormalizeImportOptional(values, "PhoneNumber"),
            DateOfBirth = ParseDateOfBirth(values), Gender = ParseGender(values)
        };
        if (string.IsNullOrWhiteSpace(student.StudentCode))
            await studentCodeGenerator.AssignNextCodeAsync(student, cancellationToken);
        else
            await studentCodeGenerator.ReserveExistingCodeAsync(student, cancellationToken);
        context.Students.Add(student);
        return 1;
    }

    private static string? NormalizeImportOptional(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string ReadCellValue(ExcelRange cell, string header)
    {
        // Giá lấy từ giá trị số gốc, không lấy chuỗi hiển thị có ký hiệu tiền tệ hoặc làm tròn của Excel.
        if (string.Equals(header, "Price", StringComparison.OrdinalIgnoreCase) &&
            cell.Value is double or decimal or int or long or float)
            return Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? "";
        return cell.Text.Trim();
    }

    private static bool TryParsePrice(string? value, out decimal price) =>
        decimal.TryParse(value,
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out price);

    // ----- Phân quyền role (bảng ApplicationRolePermissions) -----
    // Cột: RoleName | Permission (khoá Controller.Action, vd. Student.Index) | IsGranted (tuỳ chọn, mặc định 1).
    // Bảng ApplicationPermissions (danh mục quyền) tự sinh từ code; StudentCodeCounters tự đồng bộ khi nhập sinh viên.

    private async Task ValidatePreviewRowAsync(string entity, Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        if (entity == "student")
        {
            var departmentCode = values["DepartmentCode"].ToUpperInvariant();
            if (!await context.Departments.AnyAsync(x => x.DepartmentCode == departmentCode, cancellationToken))
                throw new InvalidOperationException($"Khoa {departmentCode} không tồn tại.");
            var email = NormalizeImportOptional(values, "Email")?.ToLowerInvariant();
            var code = values.GetValueOrDefault("StudentCode", "");
            if (email is not null && await context.Students.AnyAsync(
                x => x.Email != null && x.Email.ToLower() == email && x.StudentCode != code, cancellationToken))
                throw new InvalidOperationException("Email đã được sử dụng cho sinh viên khác.");
            return;
        }
        if (entity != "rolepermission")
            return;

        var roleName = values.GetValueOrDefault("RoleName")?.Trim();
        var permissionKey = values.GetValueOrDefault("Permission")?.Trim();
        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(permissionKey))
            throw new InvalidOperationException("RoleName và Permission là bắt buộc.");
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Vai trò Admin luôn có toàn bộ quyền, không nhập phân quyền cho Admin.");
        if (await FindRoleAsync(roleName, cancellationToken) is null)
            throw new InvalidOperationException($"Vai trò '{roleName}' không tồn tại. Hãy tạo vai trò trước.");
        if (!await context.ApplicationPermissions.AnyAsync(item => item.Key == permissionKey, cancellationToken))
            throw new InvalidOperationException($"Quyền '{permissionKey}' không tồn tại (dạng Controller.Action, vd. Student.Index).");
        ParseGranted(values);
    }

    private static bool ParseGranted(Dictionary<string, string> values)
    {
        var text = values.GetValueOrDefault("IsGranted")?.Trim().ToLowerInvariant();
        return text switch
        {
            null or "" or "1" or "true" or "x" or "yes" or "có" or "co" => true,
            "0" or "false" or "no" or "không" or "khong" => false,
            _ => throw new InvalidOperationException("IsGranted chỉ nhận 1/0, true/false, có/không hoặc để trống (= có).")
        };
    }

    private Task<Microsoft.AspNetCore.Identity.IdentityRole?> FindRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var normalized = roleName.Trim().ToUpperInvariant();
        return context.Roles.FirstOrDefaultAsync(item => item.NormalizedName == normalized, cancellationToken);
    }

    private async Task<ApplicationRolePermission?> FindRolePermissionAsync(Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        var role = await FindRoleAsync(values.GetValueOrDefault("RoleName") ?? "", cancellationToken);
        if (role is null)
            return null;
        var permissionKey = values.GetValueOrDefault("Permission")?.Trim() ?? "";
        return await context.ApplicationRolePermissions
            .FirstOrDefaultAsync(item => item.RoleId == role.Id && item.Permission.Key == permissionKey, cancellationToken);
    }

    private async Task<int> ExecuteRolePermissionAsync(Dictionary<string, string> values, ExcelImportMode mode, CancellationToken cancellationToken)
    {
        await ValidatePreviewRowAsync("rolepermission", values, cancellationToken);
        var granted = ParseGranted(values);
        var existing = await FindRolePermissionAsync(values, cancellationToken);
        if (existing is not null)
        {
            if (mode == ExcelImportMode.Append) return 0;
            existing.IsGranted = granted;
            return 2;
        }

        var role = (await FindRoleAsync(values["RoleName"], cancellationToken))!;
        var permissionKey = values["Permission"].Trim();
        var permission = await context.ApplicationPermissions.SingleAsync(item => item.Key == permissionKey, cancellationToken);
        // Hai dòng cùng role+quyền trong file đã bị chặn ở bước xem trước, nhưng vẫn kiểm tra bản đang chờ lưu.
        if (context.ApplicationRolePermissions.Local.Any(item => item.RoleId == role.Id && item.PermissionId == permission.Id))
            return 0;

        context.ApplicationRolePermissions.Add(new ApplicationRolePermission
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            IsGranted = granted
        });
        return 1;
    }
}
