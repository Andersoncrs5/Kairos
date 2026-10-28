using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Repositories;

public sealed class PermissionUserRepository(AppDbContext context)
    : BaseRepositoryImpl<PermissionUserModel>(context),
        IPermissionUserRepository
{
    public async Task<Page<PermissionUserModel>> FilterAsync(
        FilterPermissionUserDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<PermissionUserModel> query = context
            .Set<PermissionUserModel>()
            .AsNoTracking()
            .Include(permissionUser => permissionUser.User)
            .Include(permissionUser => permissionUser.Permissions);

        // BaseFilter: Id
        if (!string.IsNullOrWhiteSpace(filter.Id))
        {
            query = query.Where(permissionUser =>
                permissionUser.Id == filter.Id);
        }

        // BaseFilter: Version
        if (filter.VersionMin.HasValue)
        {
            query = query.Where(permissionUser =>
                permissionUser.Version >= filter.VersionMin.Value);
        }

        if (filter.VersionMax.HasValue)
        {
            query = query.Where(permissionUser =>
                permissionUser.Version <= filter.VersionMax.Value);
        }

        // BaseFilter: CreatedAt
        if (filter.CreatedAtMin.HasValue)
        {
            query = query.Where(permissionUser =>
                permissionUser.CreatedAt >= filter.CreatedAtMin.Value);
        }

        if (filter.CreatedAtMax.HasValue)
        {
            query = query.Where(permissionUser =>
                permissionUser.CreatedAt <= filter.CreatedAtMax.Value);
        }

        // BaseFilter: UpdatedAt
        if (filter.UpdatedAtMin.HasValue)
        {
            query = query.Where(permissionUser =>
                permissionUser.UpdatedAt >= filter.UpdatedAtMin.Value);
        }

        if (filter.UpdatedAtMax.HasValue)
        {
            query = query.Where(permissionUser =>
                permissionUser.UpdatedAt <= filter.UpdatedAtMax.Value);
        }

        // UserId
        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            query = query.Where(permissionUser =>
                permissionUser.UserId == filter.UserId);
        }

        // UserName
        if (!string.IsNullOrWhiteSpace(filter.UserName))
        {
            string userName = filter.UserName.Trim();

            query = filter.IgnoreCaseUserName == true
                ? query.Where(permissionUser =>
                    permissionUser.User != null &&
                    permissionUser.User.UserName != null &&
                    permissionUser.User.UserName
                        .ToLower()
                        .Contains(userName.ToLower()))
                : query.Where(permissionUser =>
                    permissionUser.User != null &&
                    permissionUser.User.UserName != null &&
                    permissionUser.User.UserName.Contains(userName));
        }

        // UserEmail
        if (!string.IsNullOrWhiteSpace(filter.UserEmail))
        {
            string userEmail = filter.UserEmail.Trim();

            query = filter.IgnoreCaseUserEmail == true
                ? query.Where(permissionUser =>
                    permissionUser.User != null &&
                    permissionUser.User.Email != null &&
                    permissionUser.User.Email
                        .ToLower()
                        .Contains(userEmail.ToLower()))
                : query.Where(permissionUser =>
                    permissionUser.User != null &&
                    permissionUser.User.Email != null &&
                    permissionUser.User.Email.Contains(userEmail));
        }

        // PermissionId
        if (!string.IsNullOrWhiteSpace(filter.PermissionId))
        {
            query = query.Where(permissionUser =>
                permissionUser.PermissionId == filter.PermissionId);
        }

        // PermissionCode
        if (!string.IsNullOrWhiteSpace(filter.PermissionCode))
        {
            string permissionCode = filter.PermissionCode.Trim();

            query = filter.IgnoreCasePermissionCode == true
                ? query.Where(permissionUser =>
                    permissionUser.Permissions.Code
                        .ToLower()
                        .Contains(permissionCode.ToLower()))
                : query.Where(permissionUser =>
                    permissionUser.Permissions.Code.Contains(permissionCode));
        }

        // PermissionName
        if (!string.IsNullOrWhiteSpace(filter.PermissionName))
        {
            string permissionName = filter.PermissionName.Trim();

            query = filter.IgnoreCasePermissionName == true
                ? query.Where(permissionUser =>
                    permissionUser.Permissions.Name
                        .ToLower()
                        .Contains(permissionName.ToLower()))
                : query.Where(permissionUser =>
                    permissionUser.Permissions.Name.Contains(permissionName));
        }

        // PermissionModuleEnums
        if (filter.PermissionModuleEnums is { Length: > 0 } modules)
        {
            query = query.Where(permissionUser =>
                Enumerable.Contains(
                    modules,
                    permissionUser.Permissions.Module));
        }

        // IsActive
        if (filter.IsActive.HasValue)
        {
            query = query.Where(permissionUser =>
                permissionUser.IsActive == filter.IsActive.Value);
        }

        // Total de registros antes da paginação
        int totalCount = await query.CountAsync(cancellationToken);

        // Paginação
        int pageNumber = Math.Max(filter.Page ?? 1, 1);
        int pageSize = Math.Clamp(filter.PageSize ?? 20, 1, 100);

        // Ordenação por data de criação
        IOrderedQueryable<PermissionUserModel> orderedQuery =
            filter.ColumnOrder == ColumnOrderEnum.DESC
                ? query
                    .OrderByDescending(permissionUser => permissionUser.CreatedAt)
                    .ThenByDescending(permissionUser => permissionUser.Id)
                : query
                    .OrderBy(permissionUser => permissionUser.CreatedAt)
                    .ThenBy(permissionUser => permissionUser.Id);

        PermissionUserModel[] data = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return Page<PermissionUserModel>.Create(
            data,
            totalCount,
            pageNumber,
            pageSize);
    }
    
    public async Task DeleteByUserIdAndPermissionIdAsync(
        string userId, 
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        await context.PermissionUsers
            .Where(p => p.UserId == userId && p.PermissionId == permissionId)
            .ExecuteDeleteAsync(cancellationToken);
    }
    
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