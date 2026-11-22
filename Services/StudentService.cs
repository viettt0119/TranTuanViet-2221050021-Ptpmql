using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.ViewModels.Students;
using Microsoft.EntityFrameworkCore;

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

        var query = context.Students
            .AsNoTracking()
            .Include(student => student.Department)
            .Where(student => true);

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var keyword = $"%{filter.Q.Trim()}%";
            query = query.Where(student =>
                EF.Functions.Like(student.StudentCode, keyword) ||
                EF.Functions.Like(student.FullName, keyword) ||
                (student.Email != null && EF.Functions.Like(student.Email, keyword)));
        }
        if (!string.IsNullOrWhiteSpace(filter.DepartmentCode))
            query = query.Where(student => student.DepartmentCode == filter.DepartmentCode);
        if (filter.AdmissionYear.HasValue)
            query = query.Where(student => student.AdmissionYear == filter.AdmissionYear.Value);

        var descending = string.Equals(filter.Dir, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<StudentModel> ordered = filter.Sort.ToLowerInvariant() switch
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

        var projected = ordered.Select(student => new StudentListItemViewModel
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
        if (await EmailExistsAsync(form.Email, null, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.Email), "Email này đã được sử dụng.");
        if (!await context.Departments.AnyAsync(department => department.DepartmentCode == form.DepartmentCode, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.DepartmentCode), "Vui lòng chọn Khoa đang tồn tại.");

        var student = new StudentModel
        {
            FullName = form.FullName.Trim(),
            DateOfBirth = form.DateOfBirth,
            Gender = form.Gender,
            Email = NormalizeOptional(form.Email),
            PhoneNumber = NormalizeOptional(form.PhoneNumber),
            Address = form.Address.Trim(),
            University = form.University.Trim(),
            DepartmentCode = form.DepartmentCode,
            AdmissionYear = form.AdmissionYear,
            TrainingSystemCode = form.TrainingSystemCode,
            MajorCode = form.MajorCode
        };

        await codeGenerator.CreateAsync(student, cancellationToken);
        return ServiceResult.Ok($"Đã thêm sinh viên {student.StudentCode}.", student.StudentCode);
    }

    public async Task<ServiceResult> UpdateAsync(int id, StudentFormViewModel form, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (student is null)
            return ServiceResult.Fail("Sinh viên không tồn tại.");
        if (student.ConcurrencyStamp != form.ConcurrencyStamp)
            return ServiceResult.Fail("Sinh viên đã bị người khác thay đổi hoặc xoá. Vui lòng đóng và tải lại.");
        if (await EmailExistsAsync(form.Email, id, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.Email), "Email này đã được sử dụng.");
        if (!await context.Departments.AnyAsync(department => department.DepartmentCode == form.DepartmentCode, cancellationToken))
            return ServiceResult.FieldFail(nameof(form.DepartmentCode), "Vui lòng chọn Khoa đang tồn tại.");

        student.FullName = form.FullName.Trim();
        student.DateOfBirth = form.DateOfBirth;
        student.Gender = form.Gender;
        student.Email = NormalizeOptional(form.Email);
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
            return ServiceResult.Fail("Sinh viên đã bị người khác thay đổi hoặc xoá. Vui lòng đóng và tải lại.");
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
        var normalized = NormalizeOptional(email);
        if (normalized is null)
            return false;
        return await context.Students.AnyAsync(
            student => student.Email == normalized && (!excludeId.HasValue || student.Id != excludeId.Value),
            cancellationToken);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
