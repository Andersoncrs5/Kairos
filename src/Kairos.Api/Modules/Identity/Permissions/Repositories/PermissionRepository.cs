using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.Permissions.Repositories;

public sealed class PermissionRepository(AppDbContext context)
    : BaseRepositoryImpl<PermissionsModel>(context), IPermissionRepository
{
    public Task<PermissionsModel?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return context.Set<PermissionsModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                permission => permission.Code == code,
                cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return context.Set<PermissionsModel>()
            .AnyAsync(
                permission => permission.Code.ToLower() == code.ToLower(),
                cancellationToken);
    }
}