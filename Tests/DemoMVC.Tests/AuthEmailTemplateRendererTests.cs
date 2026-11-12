using System.Text.Encodings.Web;
using DemoMVC.Services;
using Xunit;

namespace DemoMVC.Tests;

public class AuthEmailTemplateRendererTests
{
    private readonly AuthEmailTemplateRenderer _renderer = new(HtmlEncoder.Default);

    [Fact]
    public void Confirmation_escapes_user_supplied_name_and_link()
    {
        var html = _renderer.Confirmation("<script>alert(1)</script>", "https://example.test/?x=\" onmouseover=\"alert(1)");

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" onmouseover=\"alert(1)", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Password_changed_template_escapes_full_name()
    {
        var html = _renderer.PasswordChanged("<img src=x>");

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img", html, StringComparison.Ordinal);
    }
}
