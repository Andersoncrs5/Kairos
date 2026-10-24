using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;

public sealed class FindPermissionUserByIdUseCase(IUnitOfWork uow)
    : IFindPermissionUserByIdUseCase
{
    public async Task<Result<PermissionUserModel>> ExecuteAsync(
        string id,
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

        var permissionUser = await uow.PermissionUserRepository.GetByIdAsync(
            permissionUserId.ToString(),
            cancellationToken);

        if (permissionUser is null)
        {
            return Result<PermissionUserModel>.NotFound(
                "Permission user not found.");
        }

        return Result<PermissionUserModel>.Success(permissionUser);
    }
}