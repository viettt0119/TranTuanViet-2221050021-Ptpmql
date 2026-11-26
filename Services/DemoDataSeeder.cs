using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DemoMVC.Services;

public sealed class DemoDataSeeder(
    ApplicationDbContext context,
    StudentCodeGenerator studentCodeGenerator,
    IOptions<DemoDataSeedOptions> options,
    IHostEnvironment environment)
{
    private const int SampleRecordCount = 20;
    private const int SampleAdmissionYear = 2026;
    private const string SampleTrainingSystemCode = "99";
    private const string SampleMajorCode = "99";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!(options.Value.Enabled ?? environment.IsDevelopment()))
            return;

        var departments = await context.Departments
            .AsNoTracking()
            .OrderBy(department => department.DepartmentCode)
            .ToListAsync(cancellationToken);

        if (departments.Count == 0)
            throw new InvalidOperationException("Cannot seed sample students because no departments exist.");

        var sampleStudentEmails = Enumerable.Range(1, SampleRecordCount)
            .Select(index => StudentEmail(index))
            .ToArray();
        var existingStudentEmails = await context.Students
            .AsNoTracking()
            .Where(student => student.Email != null && sampleStudentEmails.Contains(student.Email))
            .Select(student => student.Email!)
            .ToHashSetAsync(cancellationToken);

        var sampleCodePrefix = StudentCodeGenerator.Prefix(
            SampleAdmissionYear,
            SampleTrainingSystemCode,
            SampleMajorCode);
        var existingStudentsWithSampleCodePrefix = await context.Students
            .AsNoTracking()
            .Where(student => student.StudentCode.StartsWith(sampleCodePrefix))
            .ToListAsync(cancellationToken);
        foreach (var existingStudent in existingStudentsWithSampleCodePrefix)
            await studentCodeGenerator.ReserveExistingCodeAsync(existingStudent, cancellationToken);

        for (var index = 1; index <= SampleRecordCount; index++)
        {
            var email = StudentEmail(index);
            if (existingStudentEmails.Contains(email))
                continue;

            var student = new StudentModel
            {
                FullName = $"Sinh viên mẫu {index:D2}",
                Address = $"Địa chỉ dữ liệu mẫu {index:D2}",
                University = "Đại học Mỏ - Địa chất",
                DepartmentCode = departments[(index - 1) % departments.Count].DepartmentCode,
                AdmissionYear = SampleAdmissionYear,
                TrainingSystemCode = SampleTrainingSystemCode,
                MajorCode = SampleMajorCode,
                Email = email,
                PhoneNumber = $"0{900000000 + index}",
                DateOfBirth = new DateOnly(2006, 1, 1).AddDays(index - 1),
                Gender = (StudentGender)((index - 1) % 3)
            };

            await studentCodeGenerator.CreateAsync(student, cancellationToken);
            existingStudentEmails.Add(email);
        }

        var sampleEmployeeIds = Enumerable.Range(1, SampleRecordCount)
            .Select(EmployeeId)
            .ToArray();
        var existingEmployeeIds = await context.Employees
            .AsNoTracking()
            .Where(employee => sampleEmployeeIds.Contains(employee.EmpId))
            .Select(employee => employee.EmpId)
            .ToHashSetAsync(cancellationToken);

        var employees = Enumerable.Range(1, SampleRecordCount)
            .Where(index => !existingEmployeeIds.Contains(EmployeeId(index)))
            .Select(index => new EmployeeModel
            {
                EmpId = EmployeeId(index),
                FullName = $"Nhân viên mẫu {index:D2}",
                HireDate = new DateTime(2020, 1, 1).AddDays(index - 1)
            })
            .ToArray();

        if (employees.Length == 0)
            return;

        context.Employees.AddRange(employees);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string StudentEmail(int index) => $"student.sample.{index:D2}@example.invalid";

    private static string EmployeeId(int index) => $"DEMO-EMP-{index:D4}";
}
