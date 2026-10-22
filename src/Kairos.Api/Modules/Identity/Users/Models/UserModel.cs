using Microsoft.AspNetCore.Identity;

namespace Kairos.Api.Modules.Identity.Users.Models;

public class UserModel: IdentityUser
{
    public string? FullName { get; set; }
    public string[] Permissions { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiryTime { get; set; }
    
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    
    public bool AddPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var normalizedPermission = permission.Trim();

        if (Permissions.Contains(
                normalizedPermission,
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        Permissions = [.. Permissions, normalizedPermission];

        return true;
    }

    public bool RemovePermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var normalizedPermission = permission.Trim();

        var updatedPermissions = Permissions
            .Where(p => !string.Equals(
                p,
                normalizedPermission,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (updatedPermissions.Length == Permissions.Length)
        {
            return false;
        }

        Permissions = updatedPermissions;

        return true;
    }

    public bool ExistsPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var normalizedPermission = permission.Trim();

        return Permissions.Contains(
            normalizedPermission,
            StringComparer.OrdinalIgnoreCase);
    }
    
}