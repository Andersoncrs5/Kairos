using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Repositories;

public interface IPermissionRepository
    : IBaseRepository<PermissionsModel>
{
    Task<Page<PermissionsModel>> Find(PermissionFilterDto dto, CancellationToken ct = default);
    Task<PermissionsModel?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);
}