using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface IUpdatePermissionUserUseCase
{
    Task<Result<PermissionUserModel>> ExecuteAsync(
        string id,
        UpdatePermissionUserDTO dto,
        CancellationToken cancellationToken = default);
}