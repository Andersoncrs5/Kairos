using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface IFindPermissionByCodeUseCase
{
    Task<Result<PermissionsModel>> ExecuteAsync(
        string code,
        CancellationToken cancellationToken = default);
}