using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Services;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePermissionAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
