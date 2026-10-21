using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface IFindPermissionByIdUseCase
{
    Task<Result<PermissionsModel>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default);
}