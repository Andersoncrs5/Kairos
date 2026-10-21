using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface ICreatePermissionUseCase
{
    Task<Result<PermissionsModel>> ExecuteAsync(CreatePermissionDto dto, CancellationToken cancellationToken);
}