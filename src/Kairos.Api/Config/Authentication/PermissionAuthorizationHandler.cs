using Microsoft.AspNetCore.Authorization;

namespace Kairos.Api.Config.Authentication;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (requirement.Permissions.Count == 0 ||
            requirement.Permissions.Any(
                string.IsNullOrWhiteSpace))
        {
            return Task.CompletedTask;
        }

        HashSet<string> userPermissions = context.User
            .FindAll("permission")
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool hasAllPermissions = requirement.Permissions
            .All(userPermissions.Contains);

        if (hasAllPermissions)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}