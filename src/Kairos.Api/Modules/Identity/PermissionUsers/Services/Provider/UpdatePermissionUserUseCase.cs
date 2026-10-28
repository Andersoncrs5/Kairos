using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;
using Kairos.Api.Modules.Identity.PermissionUsers.Mapper;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;

public sealed class UpdatePermissionUserUseCase(IUnitOfWork uow)
    : IUpdatePermissionUserUseCase
{
    public async Task<Result<PermissionUserModel>> ExecuteAsync(
        UpdatePermissionUserCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.Id))
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["Id is required."]);
        }

        if (!Guid.TryParse(command.Id, out var permissionUserId))
        {
            return Result<PermissionUserModel>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        if (command.Dto is null)
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

        PermissionUserMapper.ApplyUpdate(command.Dto, permissionUser);

        uow.PermissionUserRepository.Update(permissionUser);

        return Result<PermissionUserModel>.Success(permissionUser);
    }
}