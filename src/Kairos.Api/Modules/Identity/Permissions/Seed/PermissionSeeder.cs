using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Seed;

public class PermissionSeeder(IUnitOfWork uow) : IPermissionSeeder
{
    public static IReadOnlyList<PermissionsModel> Permissions { get; } = Array.AsReadOnly([
            // ----------------------------------------------------------
            // Users
            // ----------------------------------------------------------
            new PermissionsModel
            {
                Id = Guid.NewGuid()
                    .ToString(),
                Code = "identity.users.create",
                Name = "Create users",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows creating users.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.users.read",
                Name = "Read users",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows retrieving a user by ID.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.users.list",
                Name = "List users",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows listing users with filters and pagination.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.users.update",
                Name = "Update users",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows updating user information.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.users.delete",
                Name = "Delete users",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows deleting users.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },

            // ----------------------------------------------------------
            // Permissions
            // ----------------------------------------------------------

            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permissions.create",
                Name = "Create permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows creating permissions.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permissions.read",
                Name = "Read permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows retrieving a permission by ID.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permissions.list",
                Name = "List permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows listing permissions.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permissions.update",
                Name = "Update permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows updating permissions.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permissions.delete",
                Name = "Delete permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows deleting permissions.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },

            // ----------------------------------------------------------
            // User permissions
            // ----------------------------------------------------------

            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permission-users.create",
                Name = "Grant user permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows assigning permissions to users.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permission-users.read",
                Name = "Read user permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows retrieving permissions assigned to a user.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permission-users.list",
                Name = "List user permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows listing user permission assignments.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permission-users.update",
                Name = "Update user permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows updating permissions assigned to a user.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new PermissionsModel
            {
                Id = Guid.NewGuid().ToString(),
                Code = "identity.permission-users.delete",
                Name = "Revoke user permissions",
                Module = PermissionModuleEnum.Identity,
                IsSystem = true,
                Description = "Allows removing permissions from users.",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            }
        ]);

    public async Task Execute(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await uow.ExecuteTransactionAsync(
            async ct =>
            {
                List<PermissionsModel> permissionsToAdd = [];

                foreach (PermissionsModel permission in Permissions)
                {
                    ct.ThrowIfCancellationRequested();

                    bool exists = await uow.PermissionRepository.ExistsByCodeAsync(permission.Code, ct);

                    if (!exists)
                    {
                        permissionsToAdd.Add(permission);
                    }
                }

                if (permissionsToAdd.Count == 0)
                {
                    return;
                }

                await uow.PermissionRepository.AddAllAsync(permissionsToAdd, ct);
            }, cancellationToken);
    }
}