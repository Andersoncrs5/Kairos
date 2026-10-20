using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Repositories;

public interface IPermissionRepository
    : IBaseRepository<PermissionsModel>
{
    Task<PermissionsModel?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);
}