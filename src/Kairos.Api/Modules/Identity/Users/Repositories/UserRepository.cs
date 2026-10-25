using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.Users.Repositories;

public class UserRepository(AppDbContext context, UserManager<UserModel> manager): IUserRepository
{
    public Task<UserModel?> GetByIdAsync(string id)
        => manager.FindByIdAsync(id);
    public async Task<bool> ExistsByIdAsync(string id)
        => await manager.FindByIdAsync(id) != null;

    public Task<UserModel?> GetByEmail(string email)
        => manager.FindByEmailAsync(email);

    public async Task<bool> ExistsByEmail(string email)
        => await manager.FindByEmailAsync(email) != null;

    public Task<UserModel?> GetByUsername(string username)
        => manager.FindByNameAsync(username);

    public async Task<bool> ExistsByUsername(string username)
        => await manager.FindByNameAsync(username) != null;

    public Task<bool> CheckPassword(UserModel user, string password)
        => manager.CheckPasswordAsync(user, password);

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
    
    public Task<IdentityResult> Delete(UserModel user)
        => manager.DeleteAsync(user);
    
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