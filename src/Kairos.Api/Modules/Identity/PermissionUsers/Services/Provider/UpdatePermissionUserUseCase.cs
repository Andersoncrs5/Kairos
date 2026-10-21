using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Mapper;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;

public sealed class UpdatePermissionUserUseCase(IUnitOfWork uow)
    : IUpdatePermissionUserUseCase
{
    public async Task<Result<PermissionUserModel>> ExecuteAsync(
        string id,
        UpdatePermissionUserDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["Id is required."]);
        }

        if (!Guid.TryParse(id, out var permissionUserId))
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        if (dto is null)
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["Request body is required."]);
        }

        var permissionUser = await uow.PermissionUserRepository.GetByIdAsync(
            permissionUserId.ToString(),
            cancellationToken);

        if (permissionUser is null)
        {
            return Result<PermissionUserModel>.NotFound(
                "Permission-user association not found.");
        }

        PermissionUserMapper.ApplyUpdate(dto, permissionUser);

        uow.PermissionUserRepository.Update(permissionUser);

        return Result<PermissionUserModel>.Success(permissionUser);
    }
}