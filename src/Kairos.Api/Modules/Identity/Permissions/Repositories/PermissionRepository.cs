using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.Permissions.Repositories;

public sealed class PermissionRepository(AppDbContext context)
    : BaseRepositoryImpl<PermissionsModel>(context), IPermissionRepository
{
    public async Task<Page<PermissionsModel>> Find(
        PermissionFilterDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var query = context.Set<PermissionsModel>()
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim();

            query = dto.CodeIgnoreCase == true
                ? query.Where(permission =>
                    EF.Functions.ILike(permission.Code, $"%{code}%"))
                : query.Where(permission =>
                    EF.Functions.Like(permission.Code, $"%{code}%"));
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            var name = dto.Name.Trim();

            query = dto.NameIgnoreCase == true
                ? query.Where(permission =>
                    EF.Functions.ILike(permission.Name, $"%{name}%"))
                : query.Where(permission =>
                    EF.Functions.Like(permission.Name, $"%{name}%"));
        }

        if (dto.Modules is { Count: > 0 })
        {
            var modules = dto.Modules;

            query = query.Where(permission =>
                modules.Contains(permission.Module));
        }

        if (dto.IsSystem.HasValue)
        {
            query = query.Where(permission =>
                permission.IsSystem == dto.IsSystem.Value);
        }

        if (dto.IsActive.HasValue)
        {
            query = query.Where(permission =>
                permission.IsActive == dto.IsActive.Value);
        }

        // Paginação: considerando que BaseFilter possui PageNumber e PageSize.
        var pageNumber = Math.Max(dto.Page ?? 1, 1);
        var pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderBy(permission => permission.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Page<PermissionsModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
    
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