namespace DemoMVC.Models.ViewModels.Students;

public sealed class StudentFilterViewModel
{
    public string? Q { get; set; }
    public string? DepartmentCode { get; set; }
    public int? AdmissionYear { get; set; }
    public string Sort { get; set; } = "code";
    public string Dir { get; set; } = "asc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
