using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Repositories;

public sealed class PermissionUserRepository(AppDbContext context)
    : BaseRepositoryImpl<PermissionUserModel>(context),
        IPermissionUserRepository
{
    public async Task<IReadOnlyList<PermissionUserModel>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<PermissionUserModel>()
            .AsNoTracking()
            .Where(permissionUser => permissionUser.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<PermissionUserModel?> GetByUserIdAndPermissionIdAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        return context.Set<PermissionUserModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                permissionUser =>
                    permissionUser.UserId == userId &&
                    permissionUser.PermissionId == permissionId,
                cancellationToken);
    }

    public Task<bool> HasActivePermissionAsync(
        string userId,
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        return context.Set<PermissionUserModel>()
            .AnyAsync(
                permissionUser =>
                    permissionUser.UserId == userId &&
                    permissionUser.PermissionId == permissionId &&
                    permissionUser.IsActive,
                cancellationToken);
    }
}