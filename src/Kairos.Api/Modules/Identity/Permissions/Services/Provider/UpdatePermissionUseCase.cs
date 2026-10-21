using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Mapper;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public sealed class UpdatePermissionUseCase(IUnitOfWork uow)
    : IUpdatePermissionUseCase
{
    public async Task<Result<PermissionsModel>> ExecuteAsync(
        string id,
        UpdatePermissionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<PermissionsModel>.ValidationFailure(
                ["Id is required."]);
        }

        if (!Guid.TryParse(id, out var permissionId))
        {
            return Result<PermissionsModel>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var permission = await uow.PermissionRepository.GetByIdAsync(
            permissionId.ToString(),
            cancellationToken);

        if (permission is null)
        {
            return Result<PermissionsModel>.NotFound(
                "Permission not found.");
        }

        if (permission.IsSystem)
        {
            return Result<PermissionsModel>.Forbidden(
                "System permissions cannot be modified.");
        }
        
        PermissionMapper.ApplyUpdate(dto, permission);

        uow.PermissionRepository.Update(permission);
        
        return Result<PermissionsModel>.Success(permission);
    }
}