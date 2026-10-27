using Microsoft.AspNetCore.Authorization;

namespace Kairos.Api.Config.Authentication;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class RequirePermissionsAttribute : AuthorizeAttribute
{
    internal const string PolicyPrefix = "RequirePermissions:";

    public RequirePermissionsAttribute(params string[] permissions)
    {
        Policy = $"{PolicyPrefix}{string.Join(",", permissions ?? [])}";
    }
}