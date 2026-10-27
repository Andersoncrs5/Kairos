using Microsoft.AspNetCore.Authorization;

namespace Kairos.Api.Config.Authentication;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; }

    public PermissionRequirement(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        Permissions = permissions
            .Select(permission => permission?.Trim() ?? string.Empty)
            .ToArray();
    }
}