using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.Authentication;

public sealed class PermissionAuthorizationPolicyProvider(
    IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(
        string policyName)
    {
        if (!policyName.StartsWith(
                RequirePermissionsAttribute.PolicyPrefix,
                StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        string permissionsValue = policyName[
            RequirePermissionsAttribute.PolicyPrefix.Length..];

        string[] permissions = permissionsValue.Split(
            ',',
            StringSplitOptions.TrimEntries);

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissions))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}