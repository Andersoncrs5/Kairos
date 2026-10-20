using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Repositories;

public interface IPermissionUserRepository
    : IBaseRepository<PermissionUserModel>
{
    Task<IReadOnlyList<PermissionUserModel>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<PermissionUserModel?> GetByUserIdAndPermissionIdAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActivePermissionAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default);
}