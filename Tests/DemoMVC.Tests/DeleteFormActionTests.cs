using System.Reflection;
using System.Text.RegularExpressions;
using DemoMVC.Services;
using Xunit;

namespace DemoMVC.Tests;

public class DeleteFormActionTests
{
    // Action có [ActionName] chỉ truy cập được bằng tên đó: Url.Action(nameof(TênPhươngThức)) tạo ra URL 404.
    [Fact]
    public void Controllers_never_build_urls_from_a_method_name_hidden_by_ActionName()
    {
        var root = TestProject.Root();

        foreach (var controller in Permissions.GetControllerTypes(typeof(Permissions).Assembly))
        {
            var source = File.ReadAllText(Path.Combine(root, "Controllers", controller.Name + ".cs"));
            foreach (var method in Permissions.GetActionMethods(controller)
                         .Where(method => Permissions.GetActionName(method) != method.Name))
            {
                Assert.False(
                    Regex.IsMatch(source, $@"Action\(\s*nameof\({method.Name}\)"),
                    $"{controller.Name} tạo URL từ {method.Name}, hãy dùng \"{Permissions.GetActionName(method)}\".");
            }
        }
    }
}
