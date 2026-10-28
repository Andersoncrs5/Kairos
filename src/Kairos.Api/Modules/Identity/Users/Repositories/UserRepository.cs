using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.Users.Repositories;

public class UserRepository(AppDbContext context, UserManager<UserModel> manager): IUserRepository
{
    
    public async Task<Page<UserModel>> Find(
        FilterUserDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        int page = dto.Page ?? 1;
        int pageSize = dto.PageSize ?? 20;

        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        IQueryable<UserModel> query = manager.Users.AsNoTracking();

        // BaseFilter: ID
        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            var id = dto.Id.Trim();

            query = query.Where(user => user.Id == id);
        }

        /* BaseFilter: Version
        if (dto.VersionMin.HasValue)
        {
            var versionMin = dto.VersionMin.Value;

            query = query.Where(user => user.Version >= versionMin);
        }

        if (dto.VersionMax.HasValue)
        {
            var versionMax = dto.VersionMax.Value;

            query = query.Where(user => user.Version <= versionMax);
        }
        */
        
        // BaseFilter: CreatedAt
        if (dto.CreatedAtMin.HasValue)
        {
            var createdAtMin = dto.CreatedAtMin.Value;

            query = query.Where(user => user.CreatedAt >= createdAtMin);
        }

        if (dto.CreatedAtMax.HasValue)
        {
            var createdAtMax = dto.CreatedAtMax.Value;

            query = query.Where(user => user.CreatedAt <= createdAtMax);
        }

        // BaseFilter: UpdatedAt
        if (dto.UpdatedAtMin.HasValue)
        {
            var updatedAtMin = dto.UpdatedAtMin.Value;

            query = query.Where(user =>
                user.UpdatedAt.HasValue &&
                user.UpdatedAt.Value >= updatedAtMin);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            var updatedAtMax = dto.UpdatedAtMax.Value;

            query = query.Where(user =>
                user.UpdatedAt.HasValue &&
                user.UpdatedAt.Value <= updatedAtMax);
        }

        // UserName
        if (!string.IsNullOrWhiteSpace(dto.UserName))
        {
            var value = dto.UserName.Trim();

            if (dto.IgnoreCaseUserName == true)
            {
                var normalized = value.ToUpperInvariant();

                query = query.Where(user =>
                    user.UserName != null &&
                    user.UserName.ToUpper().Contains(normalized));
            }
            else
            {
                query = query.Where(user =>
                    user.UserName != null &&
                    user.UserName.Contains(value));
            }
        }

        // Email
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var value = dto.Email.Trim();

            if (dto.IgnoreCaseEmail == true)
            {
                var normalized = value.ToUpperInvariant();

                query = query.Where(user =>
                    user.Email != null &&
                    user.Email.ToUpper().Contains(normalized));
            }
            else
            {
                query = query.Where(user =>
                    user.Email != null &&
                    user.Email.Contains(value));
            }
        }

        // FullName
        if (!string.IsNullOrWhiteSpace(dto.FullName))
        {
            var value = dto.FullName.Trim();

            if (dto.IgnoreCaseFullName == true)
            {
                var normalized = value.ToUpperInvariant();

                query = query.Where(user =>
                    user.FullName != null &&
                    user.FullName.ToUpper().Contains(normalized));
            }
            else
            {
                query = query.Where(user =>
                    user.FullName != null &&
                    user.FullName.Contains(value));
            }
        }

        // PhoneNumber
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            var value = dto.PhoneNumber.Trim();

            if (dto.IgnoreCasePhoneNumber == true)
            {
                var normalized = value.ToUpperInvariant();

                query = query.Where(user =>
                    user.PhoneNumber != null &&
                    user.PhoneNumber.ToUpper().Contains(normalized));
            }
            else
            {
                query = query.Where(user =>
                    user.PhoneNumber != null &&
                    user.PhoneNumber.Contains(value));
            }
        }

        // Identity flags
        if (dto.EmailConfirmed.HasValue)
        {
            var emailConfirmed = dto.EmailConfirmed.Value;

            query = query.Where(user =>
                user.EmailConfirmed == emailConfirmed);
        }

        if (dto.PhoneNumberConfirmed.HasValue)
        {
            var phoneNumberConfirmed = dto.PhoneNumberConfirmed.Value;

            query = query.Where(user =>
                user.PhoneNumberConfirmed == phoneNumberConfirmed);
        }

        if (dto.TwoFactorEnabled.HasValue)
        {
            var twoFactorEnabled = dto.TwoFactorEnabled.Value;

            query = query.Where(user =>
                user.TwoFactorEnabled == twoFactorEnabled);
        }

        if (dto.LockoutEnabled.HasValue)
        {
            var lockoutEnabled = dto.LockoutEnabled.Value;

            query = query.Where(user =>
                user.LockoutEnabled == lockoutEnabled);
        }

        // Failed login attempts
        if (dto.AccessFailedCountMin.HasValue)
        {
            var min = dto.AccessFailedCountMin.Value;

            query = query.Where(user =>
                user.AccessFailedCount >= min);
        }

        if (dto.AccessFailedCountMax.HasValue)
        {
            var max = dto.AccessFailedCountMax.Value;

            query = query.Where(user =>
                user.AccessFailedCount <= max);
        }

        // Total before pagination
        long totalCount = await query.LongCountAsync(cancellationToken);

        // Deterministic ordering
        var columnOrder = dto.ColumnOrder ?? ColumnOrderEnum.ASC;

        IOrderedQueryable<UserModel> orderedQuery =
            columnOrder == ColumnOrderEnum.DESC
                ? query.OrderByDescending(user => user.CreatedAt)
                    .ThenBy(user => user.Id)
                : query.OrderBy(user => user.CreatedAt)
                    .ThenBy(user => user.Id);

        // DESC must apply to the complete ordering, including tie-breaking.
        if (columnOrder == ColumnOrderEnum.DESC)
        {
            orderedQuery = query
                .OrderByDescending(user => user.CreatedAt)
                .ThenByDescending(user => user.Id);
        }

        long offset = ((long)page - 1) * pageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dto.Page),
                "The requested page exceeds the supported offset.");
        }

        List<UserModel> users = await orderedQuery
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Page<UserModel>.Create(
            users,
            totalCount,
            page,
            pageSize);
    }
        
    public Task<UserModel?> GetByIdAsync(string id) => manager.FindByIdAsync(id);
    public async Task<bool> ExistsByIdAsync(string id) => await manager.FindByIdAsync(id) != null;
    public Task<UserModel?> GetByEmail(string email) => manager.FindByEmailAsync(email);
    public async Task<bool> ExistsByEmail(string email) => await manager.FindByEmailAsync(email) != null;
    public Task<UserModel?> GetByUsername(string username) => manager.FindByNameAsync(username);
    public async Task<bool> ExistsByUsername(string username) => await manager.FindByNameAsync(username) != null;
    public Task<bool> CheckPassword(UserModel user, string password) => manager.CheckPasswordAsync(user, password);
    public async Task<UserModel?> GetByRefreshToken(string refreshToken)
    {
        return await manager.Users
            .Where(u =>
                u.RefreshToken == refreshToken &&
                u.RefreshTokenExpiryTime > DateTimeOffset.UtcNow
            )
            .FirstOrDefaultAsync();
    }
    public Task<IdentityResult> Insert(UserModel user)
    {
        user.CreatedAt = DateTimeOffset.UtcNow;
        return manager.CreateAsync(user, user.PasswordHash!);
    }
    public Task<IdentityResult> Update(UserModel user)
    {
        user.UpdatedAt = DateTimeOffset.UtcNow;
        return manager.UpdateAsync(user);
    }
    public Task<IdentityResult> Delete(UserModel user) => manager.DeleteAsync(user);
    public async Task<IdentityResult> DeleteByIdAsync(string id)
    {
        var user = await manager.FindByIdAsync(id);

        if (user is null)
        {
            return IdentityResult.Failed(
                new IdentityError
                {
                    Code = "UserNotFound",
                    Description = "User not found."
                });
        }

        return await manager.DeleteAsync(user);
    }

    public Task<int> CountAsync(
        CancellationToken cancellationToken = default)
    {
        return manager.Users.CountAsync(cancellationToken);
    }

    public Task<bool> ExistsPermission(string permission, string userId)
    {
        return manager.Users.AnyAsync(u => u.Permissions.Contains(permission) && u.Id == userId);
    }
    
    public Task<IdentityResult> Insert(
        UserModel user,
        string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        user.CreatedAt = DateTimeOffset.UtcNow;

        return manager.CreateAsync(user, password);
    }
    
    public Task<IdentityResult> ChangePassword(
        UserModel user,
        string currentPassword,
        string newPassword)
    {
        return manager.ChangePasswordAsync(
            user,
            currentPassword,
            newPassword);
    }

}