namespace DemoMVC.Models;

/// <summary>Một mục trên trang "Xem lại bài học" (bài giảng Lec 1–6 hoặc bài thực hành đi kèm).</summary>
public sealed record LessonCatalogItem(
    int Lecture,
    string Kind,
    string Title,
    string Summary,
    string Controller,
    string Action,
    string Icon,
    IReadOnlyList<string> Topics)
{
    public bool Matches(string search) =>
        Title.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Summary.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Kind.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        $"lec {Lecture}".Contains(search, StringComparison.OrdinalIgnoreCase) ||
        $"bài {Lecture}".Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        Topics.Any(topic => topic.Contains(search, StringComparison.CurrentCultureIgnoreCase));
}
