using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;

public sealed class ExistsPermissionUserByUserIdAndPermissionIdUseCase(
    IUnitOfWork uow)
    : IExistsPermissionUserByUserIdAndPermissionIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<bool>.ValidationFailure(
                ["UserId is required."]);
        }

        if (string.IsNullOrWhiteSpace(permissionId))
        {
            return Result<bool>.ValidationFailure(
                ["PermissionId is required."]);
        }

        var normalizedUserId = userId.Trim();

        if (!Guid.TryParse(permissionId.Trim(), out var permissionGuid))
        {
            return Result<bool>.ValidationFailure(
                ["PermissionId must be a valid UUID."]);
        }

        bool association = await uow.PermissionUserRepository
            .HasActivePermissionAsync(
                normalizedUserId,
                permissionGuid.ToString(),
                cancellationToken);

        return Result<bool>.Success(association);
    }
}