using System.Text.RegularExpressions;
using Xunit;

namespace DemoMVC.Tests;

public class LessonViewMarkupTests
{
    // Một </div> thừa trong bài học sẽ đóng sớm khung nội dung của _Layout,
    // đẩy phần còn lại của trang ra ngoài và bị thanh bên che mất.
    [Fact]
    public void Lesson_views_close_exactly_the_divs_they_open()
    {
        var views = Directory.GetFiles(Path.Combine(TestProject.Root(), "Views", "Lesson"), "*.cshtml");
        Assert.NotEmpty(views);

        foreach (var view in views)
        {
            var markup = Regex.Replace(File.ReadAllText(view), @"<!--.*?-->|@\*.*?\*@", string.Empty, RegexOptions.Singleline);
            var depth = 0;
            foreach (Match tag in Regex.Matches(markup, @"</div\s*>|<div(?=[\s>])"))
            {
                depth += tag.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
                var line = markup.AsSpan(0, tag.Index).Count('\n') + 1;
                Assert.True(depth >= 0, $"{Path.GetFileName(view)}: </div> thừa gần dòng {line}.");
            }
            Assert.True(depth == 0, $"{Path.GetFileName(view)}: còn {depth} thẻ <div> chưa đóng.");
        }
    }
}
