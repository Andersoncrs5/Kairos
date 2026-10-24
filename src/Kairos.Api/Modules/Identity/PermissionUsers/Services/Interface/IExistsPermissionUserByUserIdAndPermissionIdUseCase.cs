using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface IExistsPermissionUserByUserIdAndPermissionIdUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default);
}