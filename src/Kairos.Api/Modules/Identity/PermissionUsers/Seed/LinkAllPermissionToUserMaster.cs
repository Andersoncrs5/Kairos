using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Seed;

public sealed class LinkAllPermissionToUserMaster(
    IUnitOfWork uow,
    IOptions<UserMasterOptions> masterOptions,
    ILogger<LinkAllPermissionToUserMaster> logger)
    : ILinkAllPermissionToUserMaster
{
    public async Task Execute(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string userName = masterOptions.Value.UserName.Trim();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        logger.LogInformation(
            "Starting master user permission linking. UserName: {UserName}",
            userName);

        List<(string Code, string Name, string Action)> linkedPermissions = [];

        int totalPermissions = 0;
        int alreadyLinkedCount = 0;
        int userArrayPermissionsAdded = 0;

        await uow.ExecuteTransactionAsync(
            async ct =>
            {
                UserModel? masterUser =
                    await uow.UserRepository.GetByUsername(userName);

                if (masterUser is null)
                {
                    throw new InvalidOperationException(
                        $"Master user '{userName}' was not found. " +
                        "Ensure UserMasterSeed runs before " +
                        "LinkAllPermissionToUserMaster.");
                }

                if (string.IsNullOrWhiteSpace(masterUser.Id))
                {
                    throw new InvalidOperationException(
                        $"Master user '{userName}' has an invalid user ID.");
                }

                IReadOnlyList<PermissionsModel> permissions =
                    await uow.PermissionRepository.GetAllAsync(ct);

                totalPermissions = permissions.Count;

                string[] currentUserPermissions =
                    masterUser.Permissions ?? [];

                HashSet<string> permissionCodes = new(
                    currentUserPermissions,
                    StringComparer.OrdinalIgnoreCase);

                List<string> updatedUserPermissions =
                    [.. currentUserPermissions];

                foreach (PermissionsModel permission in permissions)
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(permission.Id))
                    {
                        throw new InvalidOperationException(
                            $"Permission '{permission.Code}' has an invalid ID.");
                    }

                    if (string.IsNullOrWhiteSpace(permission.Code))
                    {
                        throw new InvalidOperationException(
                            $"Permission with ID '{permission.Id}' has an invalid code.");
                    }

                    // 1. Add the permission code to the user's array.
                    if (permissionCodes.Add(permission.Code))
                    {
                        updatedUserPermissions.Add(permission.Code);
                        userArrayPermissionsAdded++;
                    }

                    // 2. Check whether the relational assignment exists.
                    var existingLink = await uow.PermissionUserRepository
                        .GetByUserIdAndPermissionIdAsync(
                            masterUser.Id,
                            permission.Id,
                            ct);

                    if (existingLink is null)
                    {
                        await uow.PermissionUserRepository.AddAsync(
                            new PermissionUserModel
                            {
                                Id = Guid.NewGuid().ToString(),
                                UserId = masterUser.Id,
                                PermissionId = permission.Id,
                                IsActive = true,
                                CreatedAt = DateTimeOffset.UtcNow
                            },
                            ct);

                        linkedPermissions.Add(
                            (permission.Code, permission.Name, "created"));

                        continue;
                    }

                    if (!existingLink.IsActive)
                    {
                        existingLink.IsActive = true;
                        existingLink.UpdatedAt = DateTimeOffset.UtcNow;

                        uow.PermissionUserRepository.Update(existingLink);

                        linkedPermissions.Add(
                            (permission.Code, permission.Name, "reactivated"));

                        continue;
                    }

                    alreadyLinkedCount++;
                }

                // 3. Persist the updated permission-code array, if needed.
                if (userArrayPermissionsAdded > 0)
                {
                    masterUser.Permissions = updatedUserPermissions.ToArray();

                    IdentityResult updateResult =
                        await uow.UserRepository.Update(masterUser);

                    if (!updateResult.Succeeded)
                    {
                        string errors = string.Join(
                            "; ",
                            updateResult.Errors.Select(error =>
                                $"{error.Code}: {error.Description}"));

                        throw new InvalidOperationException(
                            "Failed to update the master user's permission array. " +
                            errors);
                    }
                }
            },
            cancellationToken);

        // Log assignments only after the transaction has committed.
        foreach (var permission in linkedPermissions)
        {
            logger.LogInformation(
                "Permission linked to master user. " +
                "Code: {PermissionCode}; Name: {PermissionName}; Action: {Action}",
                permission.Code,
                permission.Name,
                permission.Action);
        }

        TimeSpan elapsed = DateTimeOffset.UtcNow - startedAt;

        logger.LogInformation(
            "Master user permission linking completed. " +
            "UserName: {UserName}; TotalPermissions: {TotalPermissions}; " +
            "CreatedLinks: {CreatedLinks}; ReactivatedLinks: {ReactivatedLinks}; " +
            "AlreadyActiveLinks: {AlreadyActiveLinks}; " +
            "UserArrayPermissionsAdded: {UserArrayPermissionsAdded}; " +
            "DurationMs: {DurationMs}",
            userName,
            totalPermissions,
            linkedPermissions.Count(permission => permission.Action == "created"),
            linkedPermissions.Count(permission => permission.Action == "reactivated"),
            alreadyLinkedCount,
            userArrayPermissionsAdded,
            elapsed.TotalMilliseconds);
    }
}