namespace DemoMVC.Services;

public sealed class ServiceResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? EntityCode { get; init; }
    public Dictionary<string, string> FieldErrors { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static ServiceResult Ok(string message, string? entityCode = null) =>
        new() { Success = true, Message = message, EntityCode = entityCode };

    public static ServiceResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static ServiceResult FieldFail(string field, string message) =>
        new()
        {
            Success = false,
            FieldErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [field] = message
            }
        };
}
