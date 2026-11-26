using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.ViewModels.Students;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Services;

public sealed class StudentService(
    ApplicationDbContext context,
    StudentCodeGenerator codeGenerator)
{
    public async Task<StudentIndexViewModel> GetPagedAsync(
        StudentFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter.PageSize = PagedList<StudentListItemViewModel>.PageSizeOptions.Contains(filter.PageSize)
            ? filter.PageSize
            : 10;
        filter.Page = Math.Max(1, filter.Page);

        var projected = BuildFilteredQuery(filter).Select(student => new StudentListItemViewModel
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            Email = student.Email,
            DepartmentCode = student.DepartmentCode,
            DepartmentName = student.Department!.DepartmentName,
            AdmissionYear = student.AdmissionYear
        });

        var paged = await PagedList<StudentListItemViewModel>.CreateAsync(
            projected,
            filter.Page,
            filter.PageSize,
            cancellationToken);

        var departments = await context.Departments
            .AsNoTracking()
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);
        var years = await context.Students
            .AsNoTracking()
            .Select(student => student.AdmissionYear)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync(cancellationToken);

        return new StudentIndexViewModel
        {
            Filter = filter,
            Students = paged,
            Departments = departments,
            AdmissionYears = years
        };
    }

    // Xuất toàn bộ kết quả theo đúng bộ lọc và thứ tự đang xem, không giới hạn ở trang hiện tại.
    public Task<List<StudentModel>> GetForExportAsync(
        StudentFilterViewModel filter, CancellationToken cancellationToken = default) =>
        BuildFilteredQuery(filter).ToListAsync(cancellationToken);

    private IOrderedQueryable<StudentModel> BuildFilteredQuery(StudentFilterViewModel filter)
    {
        filter.Q = NormalizeOptional(filter.Q);
        filter.DepartmentCode = NormalizeOptional(filter.DepartmentCode);
        filter.Sort = filter.Sort?.ToLowerInvariant() is "name" or "year" ? filter.Sort.ToLowerInvariant() : "code";
        filter.Dir = string.Equals(filter.Dir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

        var query = context.Students.AsNoTracking().Include(student => student.Department).AsQueryable();
        if (filter.Q is not null)
        {
            var keyword = $"%{filter.Q}%";
            query = query.Where(student =>
                EF.Functions.Like(student.StudentCode, keyword) ||
                EF.Functions.Like(student.FullName, keyword) ||
                (student.Email != null && EF.Functions.Like(student.Email, keyword)));
        }
        if (filter.DepartmentCode is not null)
            query = query.Where(student => student.DepartmentCode == filter.DepartmentCode);
        if (filter.AdmissionYear.HasValue)
            query = query.Where(student => student.AdmissionYear == filter.AdmissionYear.Value);

        var descending = filter.Dir == "desc";
        return filter.Sort switch
        {
            "name" => descending
                ? query.OrderByDescending(student => student.FullName).ThenBy(student => student.Id)
                : query.OrderBy(student => student.FullName).ThenBy(student => student.Id),
            "year" => descending
                ? query.OrderByDescending(student => student.AdmissionYear).ThenBy(student => student.StudentCode)
                : query.OrderBy(student => student.AdmissionYear).ThenBy(student => student.StudentCode),
            _ => descending
                ? query.OrderByDescending(student => student.StudentCode).ThenBy(student => student.Id)
                : query.OrderBy(student => student.StudentCode).ThenBy(student => student.Id)
        };
    }

    public async Task<StudentDetailsViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Students
            .AsNoTracking()
            .Include(student => student.Department)
            .Where(student => student.Id == id)
            .Select(student => new StudentDetailsViewModel
            {
                Id = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                Email = student.Email,
                DepartmentCode = student.DepartmentCode,
                DepartmentName = student.Department!.DepartmentName,
                AdmissionYear = student.AdmissionYear,
                DateOfBirth = student.DateOfBirth,
                Gender = student.Gender,
                PhoneNumber = student.PhoneNumber,
                Address = student.Address,
                University = student.University,
                TrainingSystemCode = student.TrainingSystemCode,
                MajorCode = student.MajorCode,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt,
                ConcurrencyStamp = student.ConcurrencyStamp
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Hồ sơ sinh viên của chính tài khoản: nối theo email đăng nhập đã xác thực trùng với email trên hồ sơ.
    /// Không nối theo <see cref="ApplicationUser.MemberCode"/> vì người đăng ký tự khai, có thể khai mã của người khác.
    /// Trả null khi chưa liên kết, kể cả khi có nhiều hồ sơ cùng email (không đoán hồ sơ nào là đúng).
    /// </summary>
    public async Task<StudentDetailsViewModel?> GetOwnProfileAsync(
        ApplicationUser account,
        CancellationToken cancellationToken = default)
    {
        var email = account.Email?.Trim().ToLowerInvariant();
        if (account.AccountType != AccountType.Student || !account.EmailConfirmed || string.IsNullOrEmpty(email))
            return null;

        var ids = await context.Students
            .AsNoTracking()
            .Where(student => student.Email != null && student.Email.Trim().ToLower() == email)
            .Select(student => student.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        return ids.Count == 1 ? await GetDetailsAsync(ids[0], cancellationToken) : null;
    }

    public async Task<StudentFormViewModel?> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Students
            .AsNoTracking()
            .Where(student => student.Id == id)
            .Select(student => new StudentFormViewModel
            {
                Id = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                DateOfBirth = student.DateOfBirth,
                Gender = student.Gender,
                Email = student.Email,
                PhoneNumber = student.PhoneNumber,
                Address = student.Address,
                University = student.University,
                DepartmentCode = student.DepartmentCode,
                AdmissionYear = student.AdmissionYear,
                TrainingSystemCode = student.TrainingSystemCode,
                MajorCode = student.MajorCode,
                ConcurrencyStamp = student.ConcurrencyStamp
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult> CreateAsync(StudentFormViewModel form, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateForm(form);
        if (validationError is not null)
            return validationError;
        if (await EmailExistsAsync(form.Email, null, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.Email), "Email này đã được sử dụng.");
        if (!await context.Departments.AnyAsync(department => department.DepartmentCode == form.DepartmentCode, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.DepartmentCode), "Vui lòng chọn Khoa đang tồn tại.");

        var student = new StudentModel
        {
            FullName = form.FullName.Trim(),
            DateOfBirth = form.DateOfBirth,
            Gender = form.Gender,
            Email = NormalizeEmail(form.Email),
            PhoneNumber = NormalizeOptional(form.PhoneNumber),
            Address = form.Address.Trim(),
            University = form.University.Trim(),
            DepartmentCode = form.DepartmentCode,
            AdmissionYear = form.AdmissionYear,
            TrainingSystemCode = form.TrainingSystemCode,
            MajorCode = form.MajorCode
        };

        try
        {
            await codeGenerator.CreateAsync(student, cancellationToken);
        }
        catch (StudentCodeExhaustedException ex)
        {
            context.Entry(student).State = EntityState.Detached;
            return ServiceResult.Fail(ex.Message);
        }
        catch (DbUpdateException)
        {
            // Transaction đã hoàn tác cả bộ đếm; thường do email vừa bị người khác dùng hoặc mã trùng dữ liệu cũ.
            context.Entry(student).State = EntityState.Detached;
            return ServiceResult.Fail("Không thể lưu sinh viên do trùng mã sinh viên hoặc email. Vui lòng thử lại.");
        }

        return ServiceResult.Ok($"Đã thêm sinh viên {student.StudentCode}.", student.StudentCode);
    }

    public async Task<ServiceResult> UpdateAsync(int id, StudentFormViewModel form, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (student is null)
            return ServiceResult.Fail("Sinh viên không tồn tại.");
        if (student.ConcurrencyStamp != form.ConcurrencyStamp)
            return ServiceResult.Fail("Sinh viên đã bị người khác thay đổi hoặc xoá. Vui lòng đóng và tải lại.");
        var validationError = ValidateForm(form);
        if (validationError is not null)
            return validationError;
        if (await EmailExistsAsync(form.Email, id, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.Email), "Email này đã được sử dụng.");
        if (!await context.Departments.AnyAsync(department => department.DepartmentCode == form.DepartmentCode, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.DepartmentCode), "Vui lòng chọn Khoa đang tồn tại.");

        student.FullName = form.FullName.Trim();
        student.DateOfBirth = form.DateOfBirth;
        student.Gender = form.Gender;
        student.Email = NormalizeEmail(form.Email);
        student.PhoneNumber = NormalizeOptional(form.PhoneNumber);
        student.Address = form.Address.Trim();
        student.University = form.University.Trim();
        student.DepartmentCode = form.DepartmentCode;
        student.UpdatedAt = DateTimeOffset.UtcNow;
        student.ConcurrencyStamp = Guid.NewGuid();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ServiceResult.Ok("Đã cập nhật sinh viên.");
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(student).State = EntityState.Detached;
            return ServiceResult.Fail("Sinh viên đã bị người khác thay đổi hoặc xoá. Vui lòng đóng và tải lại.");
        }
        catch (DbUpdateException)
        {
            context.Entry(student).State = EntityState.Detached;
            return ServiceResult.Fail("Không thể lưu sinh viên vì dữ liệu vừa thay đổi hoặc email đã được sử dụng. Vui lòng tải lại và thử lại.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(int id, Guid concurrencyStamp, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (student is null)
            return ServiceResult.Fail("Sinh viên không tồn tại.");
        if (student.ConcurrencyStamp != concurrencyStamp)
            return ServiceResult.Fail("Sinh viên đã bị người khác thay đổi hoặc xoá. Vui lòng đóng và tải lại.");

        context.Students.Remove(student);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ServiceResult.Ok("Đã xoá sinh viên.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("Sinh viên đã bị người khác thay đổi hoặc xoá. Vui lòng đóng và tải lại.");
        }
    }

    private async Task<bool> EmailExistsAsync(string? email, int? excludeId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null)
            return false;
        return await context.Students.AnyAsync(
            student => student.Email != null && student.Email.Trim().ToLower() == normalized &&
                (!excludeId.HasValue || student.Id != excludeId.Value),
            cancellationToken);
    }

    private static ServiceResult? ValidateForm(StudentFormViewModel form)
    {
        form.Email = NormalizeEmail(form.Email);
        var errors = new List<ValidationResult>();
        if (Validator.TryValidateObject(form, new ValidationContext(form), errors, validateAllProperties: true))
            return null;
        return new ServiceResult
        {
            FieldErrors = errors
                .SelectMany(error => error.MemberNames.DefaultIfEmpty(string.Empty)
                    .Select(member => new { Member = member, Message = error.ErrorMessage ?? "Dữ liệu không hợp lệ." }))
                .GroupBy(error => error.Member)
                .ToDictionary(group => group.Key, group => string.Join(" ", group.Select(error => error.Message)))
        };
    }

    private static string? NormalizeEmail(string? email) => NormalizeOptional(email)?.ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
