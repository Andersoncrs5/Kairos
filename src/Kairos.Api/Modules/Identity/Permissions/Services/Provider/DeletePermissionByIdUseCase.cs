using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public class DeletePermissionByIdUseCase(IUnitOfWork uow): IDeletePermissionByIdUseCase
{
    public async Task<Result<object>> ExecuteAsync(string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Result<object>.ValidationFailure( ["Id is required."]);

        if (!Guid.TryParse(id, out var guid))
            return Result<object>.ValidationFailure( ["Id must be a valid UUID."]);

        PermissionsModel? permission = await uow.PermissionRepository.GetByIdAsync(id, cancellationToken);
        
        if (permission is null)
            return Result<object>.NotFound("Permission not found.");

        if (permission.IsSystem)
            return Result<object>.Forbidden("Permission is system and cannot be deleted.");

        uow.PermissionRepository.Delete(permission);
        
        return Result<object>.Success(true);
    }
}