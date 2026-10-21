using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface IUpdatePermissionUseCase
{
    Task<Result<PermissionsModel>> ExecuteAsync(string id, UpdatePermissionDto dto, CancellationToken cancellationToken = default);
}