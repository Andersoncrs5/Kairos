using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface IFindPermissionUserByIdUseCase
{
    Task<Result<PermissionUserModel>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default);
}