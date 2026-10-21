using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public sealed class FindPermissionByIdUseCase(IUnitOfWork uow)
    : IFindPermissionByIdUseCase
{
    public async Task<Result<PermissionsModel>> ExecuteAsync(
        string id,
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

        return Result<PermissionsModel>.Success(permission);
    }
}