namespace DemoMVC.Models;

public sealed class ExcelImportResult
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; } = new();
    public ExcelImportStatus Status => ErrorCount > 0
        ? (SuccessCount > 0 ? ExcelImportStatus.Partial : ExcelImportStatus.Failed)
        : (SuccessCount > 0 ? ExcelImportStatus.Completed : ExcelImportStatus.Unchanged);
}

public enum ExcelImportStatus { Completed, Partial, Failed, Unchanged }

public sealed record ImportFieldChange(string Field, string OldValue, string NewValue);

public enum ExcelImportMode
{
    Append,
    Overwrite
}

public sealed class ExcelImportPreview
{
    public string Token { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public bool CanAppend { get; set; }
    public bool CanOverwrite { get; set; }
    public int Total { get; set; }
    public int Valid { get; set; }
    public int NewCount { get; set; }
    public int DuplicateCount { get; set; }
    public List<ImportRowError> Errors { get; } = new();
    public List<ExcelImportRow> Rows { get; } = new();
}

public sealed record ImportRowError(int Row, string Message);
public sealed class ExcelImportRow
{
    public int RowNumber { get; set; }
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsExisting { get; set; }
    public Dictionary<string, string> ExistingValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Guid? ConcurrencyStamp { get; set; }
}
