using System.Text.RegularExpressions;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services;

/// <summary>
/// Sinh mã sinh viên dạng YYHHNNSSSS, ví dụ 2821050199:
/// 28 = năm nhập học (2028), 21 = hệ đào tạo, 05 = ngành, 0199 = số thứ tự trong nhóm năm/hệ/ngành.
/// Mỗi nhóm có một bộ đếm riêng trong bảng StudentCodeCounters; số thứ tự không bao giờ được cấp lại.
/// </summary>
public sealed partial class StudentCodeGenerator(ApplicationDbContext context)
{
    public const int MaxSequence = 9999;

    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex CodePattern();

    public static string Format(int admissionYear, string trainingSystemCode, string majorCode, int sequence) =>
        $"{admissionYear % 100:D2}{trainingSystemCode}{majorCode}{sequence:D4}";

    /// <summary>Phần đầu (6 chữ số) mà mã của một sinh viên phải có: năm % 100 + hệ + ngành.</summary>
    public static string Prefix(int admissionYear, string trainingSystemCode, string majorCode) =>
        $"{admissionYear % 100:D2}{trainingSystemCode}{majorCode}";

    public static bool TryGetSequence(string? code, out int sequence)
    {
        sequence = 0;
        return code is not null && CodePattern().IsMatch(code) &&
            int.TryParse(code.AsSpan(6), out sequence) && sequence >= 1;
    }

    /// <summary>Thêm sinh viên mới với mã kế tiếp, trong một transaction.</summary>
    public async Task<string> CreateAsync(StudentModel student, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await AssignNextCodeAsync(student, cancellationToken);
        context.Students.Add(student);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return student.StudentCode;
    }

    /// <summary>
    /// Cấp số thứ tự kế tiếp và gán StudentCode. Không tự mở transaction: người gọi phải đang ở trong
    /// một transaction (CreateAsync hoặc lần nhập Excel) để bộ đếm được hoàn tác nếu việc lưu thất bại.
    /// </summary>
    public async Task AssignNextCodeAsync(StudentModel student, CancellationToken cancellationToken = default)
    {
        var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "StudentCodeCounters"
                ("AdmissionYear", "TrainingSystemCode", "MajorCode", "LastNumber")
            VALUES ({student.AdmissionYear}, {student.TrainingSystemCode}, {student.MajorCode}, 1)
            ON CONFLICT ("AdmissionYear", "TrainingSystemCode", "MajorCode")
            DO UPDATE SET "LastNumber" = "LastNumber" + 1
            WHERE "LastNumber" < {MaxSequence};
            """, cancellationToken);

        if (changed == 0)
            throw new StudentCodeExhaustedException(Prefix(student.AdmissionYear, student.TrainingSystemCode, student.MajorCode));

        var number = await context.StudentCodeCounters
            .AsNoTracking()
            .Where(c => c.AdmissionYear == student.AdmissionYear &&
                c.TrainingSystemCode == student.TrainingSystemCode &&
                c.MajorCode == student.MajorCode)
            .Select(c => c.LastNumber)
            .SingleAsync(cancellationToken);

        student.StudentCode = Format(student.AdmissionYear, student.TrainingSystemCode, student.MajorCode, number);
    }

    /// <summary>
    /// Sinh viên nhập từ Excel có sẵn mã: nâng bộ đếm lên ít nhất bằng số thứ tự của mã đó,
    /// để lần sinh mã tự động sau không cấp trùng mã đã nhập.
    /// </summary>
    public async Task ReserveExistingCodeAsync(StudentModel student, CancellationToken cancellationToken = default)
    {
        // Mã không đúng dạng (dữ liệu cũ) hoặc thuộc nhóm khác thì không ảnh hưởng tới bộ đếm của nhóm này.
        if (!TryGetSequence(student.StudentCode, out var sequence) ||
            !student.StudentCode.StartsWith(
                Prefix(student.AdmissionYear, student.TrainingSystemCode, student.MajorCode), StringComparison.Ordinal))
            return;

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "StudentCodeCounters"
                ("AdmissionYear", "TrainingSystemCode", "MajorCode", "LastNumber")
            VALUES ({student.AdmissionYear}, {student.TrainingSystemCode}, {student.MajorCode}, {sequence})
            ON CONFLICT ("AdmissionYear", "TrainingSystemCode", "MajorCode")
            DO UPDATE SET "LastNumber" = MAX("LastNumber", excluded."LastNumber");
            """, cancellationToken);
    }
}

public sealed class StudentCodeExhaustedException(string prefix)
    : InvalidOperationException($"Nhóm năm/hệ/ngành {prefix} đã cấp hết {StudentCodeGenerator.MaxSequence} mã sinh viên.");
