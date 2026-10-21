using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Mapper;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public sealed class CreatePermissionUseCase(IUnitOfWork uow)
    : ICreatePermissionUseCase
{
    public async Task<Result<PermissionsModel>> ExecuteAsync(
        CreatePermissionDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return Result<PermissionsModel>.ValidationFailure(
                ["Permission code is required."]);
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<PermissionsModel>.ValidationFailure(
                ["Permission name is required."]);
        }

        var code = dto.Code.Trim();

        var exists = await uow.PermissionRepository.ExistsByCodeAsync(
            code,
            cancellationToken);

        if (exists)
        {
            return Result<PermissionsModel>.Conflict(
                $"Permission with code '{code}' already exists.");
        }

        var permission = PermissionMapper.ToModel(dto, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);

        permission.Code = code;
        permission.IsSystem = false;
        permission.Id = Guid.NewGuid().ToString();

        await uow.PermissionRepository.AddAsync(permission, cancellationToken);

        return Result<PermissionsModel>.Created(permission);
    }
}