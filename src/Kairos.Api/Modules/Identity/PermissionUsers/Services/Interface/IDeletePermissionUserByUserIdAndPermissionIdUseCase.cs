using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface IDeletePermissionUserByUserIdAndPermissionIdUseCase
{
    Task<Result<object>> ExecuteAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default);
}   