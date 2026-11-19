namespace DemoMVC.Models;

public sealed class ExcelImportResult
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; } = new();
}

public enum ExcelImportMode
{
    Append,
    Overwrite
}

public sealed class ExcelImportPreview
{
    public string Token { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
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
}
