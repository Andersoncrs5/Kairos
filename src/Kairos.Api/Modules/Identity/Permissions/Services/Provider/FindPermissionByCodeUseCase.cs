using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public sealed class FindPermissionByCodeUseCase(IUnitOfWork uow)
    : IFindPermissionByCodeUseCase
{
    public async Task<Result<PermissionsModel>> ExecuteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<PermissionsModel>.ValidationFailure(
                ["Permission code is required."]);
        }

        var permission = await uow.PermissionRepository.GetByCodeAsync(
            code.Trim(),
            cancellationToken);

        if (permission is null)
        {
            return Result<PermissionsModel>.NotFound(
                "Permission not found.");
        }
        
        return Result<PermissionsModel>.Success(permission);
    }
}