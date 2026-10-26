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
    
    public async Task<Page<UserModel>> Find(FilterUserDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ArgumentOutOfRangeException.ThrowIfLessThan(dto.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(dto.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dto.PageSize, 100);

        IQueryable<UserModel> query = manager.Users.AsNoTracking();

        // BaseFilter
        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            query = query.Where(user => user.Id == dto.Id);
        }

        if (dto.CreatedAtMin > DateTimeOffset.MinValue)
        {
            query = query.Where(user => user.CreatedAt >= dto.CreatedAtMin);
        }

        if (dto.CreatedAtMax > DateTimeOffset.MinValue)
        {
            query = query.Where(user => user.CreatedAt <= dto.CreatedAtMax);
        }

        if (dto.UpdatedAtMin.HasValue)
        {
            query = query.Where(user =>
                user.UpdatedAt.HasValue &&
                user.UpdatedAt.Value >= dto.UpdatedAtMin.Value);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            query = query.Where(user =>
                user.UpdatedAt.HasValue &&
                user.UpdatedAt.Value <= dto.UpdatedAtMax.Value);
        }

        // UserName
        if (!string.IsNullOrWhiteSpace(dto.UserName))
        {
            string value = dto.UserName.Trim();

            query = dto.IgnoreCaseUserName == true
                ? query.Where(user =>
                    user.UserName != null &&
                    user.UserName.ToUpper().Contains(value.ToUpper()))
                : query.Where(user =>
                    user.UserName != null &&
                    user.UserName.Contains(value));
        }

        // Email
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            string value = dto.Email.Trim();

            query = dto.IgnoreCaseEmail == true
                ? query.Where(user =>
                    user.Email != null &&
                    user.Email.ToUpper().Contains(value.ToUpper()))
                : query.Where(user =>
                    user.Email != null &&
                    user.Email.Contains(value));
        }

        // FullName
        if (!string.IsNullOrWhiteSpace(dto.FullName))
        {
            string value = dto.FullName.Trim();

            query = dto.IgnoreCaseFullName == true
                ? query.Where(user =>
                    user.FullName != null &&
                    user.FullName.ToUpper().Contains(value.ToUpper()))
                : query.Where(user =>
                    user.FullName != null &&
                    user.FullName.Contains(value));
        }

        // PhoneNumber
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            string value = dto.PhoneNumber.Trim();

            query = dto.IgnoreCasePhoneNumber == true
                ? query.Where(user =>
                    user.PhoneNumber != null &&
                    user.PhoneNumber.ToUpper().Contains(value.ToUpper()))
                : query.Where(user =>
                    user.PhoneNumber != null &&
                    user.PhoneNumber.Contains(value));
        }

        // Identity flags
        if (dto.EmailConfirmed.HasValue)
        {
            query = query.Where(user =>
                user.EmailConfirmed == dto.EmailConfirmed.Value);
        }

        if (dto.PhoneNumberConfirmed.HasValue)
        {
            query = query.Where(user =>
                user.PhoneNumberConfirmed == dto.PhoneNumberConfirmed.Value);
        }

        if (dto.TwoFactorEnabled.HasValue)
        {
            query = query.Where(user =>
                user.TwoFactorEnabled == dto.TwoFactorEnabled.Value);
        }

        if (dto.LockoutEnabled.HasValue)
        {
            query = query.Where(user =>
                user.LockoutEnabled == dto.LockoutEnabled.Value);
        }

        // Failed login attempts
        if (dto.AccessFailedCountMin.HasValue)
        {
            query = query.Where(user =>
                user.AccessFailedCount >= dto.AccessFailedCountMin.Value);
        }

        if (dto.AccessFailedCountMax.HasValue)
        {
            query = query.Where(user =>
                user.AccessFailedCount <= dto.AccessFailedCountMax.Value);
        }

        // Total before pagination
        long totalCount = await query.LongCountAsync(cancellationToken);

        // Deterministic ordering
        IOrderedQueryable<UserModel> orderedQuery =
            dto.ColumnOrder == ColumnOrderEnum.DESC
                ? query.OrderByDescending(user => user.CreatedAt)
                    .ThenBy(user => user.Id)
                : query.OrderBy(user => user.CreatedAt)
                    .ThenBy(user => user.Id);

        long offset = ((long)dto.Page - 1) * dto.PageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dto.Page),
                "The requested page exceeds the supported offset.");
        }

        List<UserModel> users = await orderedQuery
            .Skip((int)offset)
            .Take(dto.PageSize)
            .ToListAsync(cancellationToken);

        return Page<UserModel>.Create(
            users,
            totalCount,
            dto.Page,
            dto.PageSize);
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