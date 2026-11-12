using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DemoMVC.Services;

public sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly IAuthorizationPolicyProvider _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
            return _fallback.GetPolicyAsync(policyName);

        var permissionKey = policyName["Permission:".Length..];
        var builder = new AuthorizationPolicyBuilder();
        builder.AddRequirements(new PermissionAuthorizationRequirement(permissionKey));
        return Task.FromResult<AuthorizationPolicy?>(builder.Build());
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
