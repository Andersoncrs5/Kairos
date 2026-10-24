using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Mapper;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;

public sealed class CreatePermissionUserUseCase(
    IUnitOfWork uow,
    IFindUserByIdUseCase findUserByIdUseCase)
    : ICreatePermissionUserUseCase
{
    public async Task<Result<PermissionUserModel>> ExecuteAsync(
        CreatePermissionUserDTO dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (dto is null)
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["Request body is required."]);
        }

        if (string.IsNullOrWhiteSpace(dto.UserId))
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["UserId is required."]);
        }

        if (string.IsNullOrWhiteSpace(dto.PermissionId))
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["PermissionId is required."]);
        }

        var userId = dto.UserId.Trim();

        if (!Guid.TryParse(dto.PermissionId.Trim(), out var permissionGuid))
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["PermissionId must be a valid UUID."]);
        }

        var permissionId = permissionGuid.ToString();
        
        PermissionsModel? permission =
            await uow.PermissionRepository.GetByIdAsync(
                permissionId,
                ct);

        if (permission is null)
        {
            return Result<PermissionUserModel>.NotFound(
                "Permission not found.");
        }

        if (!permission.IsActive)
        {
            return Result<PermissionUserModel>.Conflict(
                "Inactive permissions cannot be assigned to users.");
        }

        if (string.IsNullOrWhiteSpace(permission.Code))
        {
            return Result<PermissionUserModel>.Failure(
                "The permission has an invalid code.",
                500);
        }

        var permissionCode = permission.Code.Trim();

        var userResult = await findUserByIdUseCase.ExecuteAsync(
            userId,
            ct);

        if (userResult.IsFailure)
        {
            return Result<PermissionUserModel>.Failure(
                userResult.Errors,
                userResult.StatusCode);
        }

        UserModel? user = userResult.Value;

        if (user is null)
        {
            return Result<PermissionUserModel>.NotFound(
                "User not found.");
        }

        var normalizedUserId = user.Id;
        var normalizedPermissionId = permission.Id;

        var existingAssociation =
            await uow.PermissionUserRepository
                .GetByUserIdAndPermissionIdAsync(
                    normalizedUserId,
                    normalizedPermissionId,
                    ct);

        if (existingAssociation is not null)
        {
            return Result<PermissionUserModel>.Conflict(
                "This permission is already assigned to the user.");
        }

        var permissionUser = PermissionUserMapper.ToModel(
            dto,
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow);

        permissionUser.UserId = normalizedUserId;
        permissionUser.PermissionId = normalizedPermissionId;
        permissionUser.IsActive = true;

        await uow.PermissionUserRepository.AddAsync(
            permissionUser,
            ct);

        user.AddPermission(permissionCode);

        return Result<PermissionUserModel>.Created(permissionUser);
    }
}