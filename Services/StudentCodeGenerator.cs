using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services;

public sealed class StudentCodeGenerator(ApplicationDbContext context)
{
    public async Task<string> CreateAsync(StudentModel student, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

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

        var number = await context.Set<StudentCodeCounter>()
            .AsNoTracking()
            .Where(c => c.AdmissionYear == student.AdmissionYear &&
                c.TrainingSystemCode == student.TrainingSystemCode &&
                c.MajorCode == student.MajorCode)
            .Select(c => c.LastNumber)
            .SingleAsync(cancellationToken);

        student.StudentCode = $"{student.AdmissionYear % 100:D2}{student.TrainingSystemCode}{student.MajorCode}{number:D4}";
        context.Students.Add(student);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return student.StudentCode;
    }
}
