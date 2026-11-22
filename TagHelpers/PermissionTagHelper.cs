using DemoMVC.Services;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DemoMVC.TagHelpers;

[HtmlTargetElement(Attributes = "asp-permission")]
public sealed class PermissionTagHelper(PermissionCatalogService permissionCatalog) : TagHelper
{
    [HtmlAttributeName("asp-permission")]
    public string? Permission { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("asp-permission");
        if (string.IsNullOrWhiteSpace(Permission) ||
            ViewContext?.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            output.SuppressOutput();
            return;
        }

        if (ViewContext.HttpContext.User.IsInRole("Admin"))
            return;

        if (!await permissionCatalog.HasAccessAsync(ViewContext.HttpContext.User, Permission))
            output.SuppressOutput();
    }

    [Microsoft.AspNetCore.Mvc.ViewFeatures.ViewContext]
    public Microsoft.AspNetCore.Mvc.Rendering.ViewContext ViewContext { get; set; } = null!;
}
