using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.AspNetCore.Identity;

namespace Kairos.Api.Modules.Identity.Users.Repositories;

public interface IUserRepository
{
    Task<UserModel?> GetByIdAsync(string id);
    Task<bool> ExistsByIdAsync(string id);
    Task<UserModel?> GetByEmail(string email);
    Task<bool> ExistsByEmail(string email);
    Task<bool> CheckPassword(UserModel user, string password);
    Task<UserModel?> GetByRefreshToken(string refreshToken);
    Task<bool> ExistsByUsername(string username);
    Task<UserModel?> GetByUsername(string username);
    Task<IdentityResult> Delete(UserModel user);
    Task<IdentityResult> Insert(UserModel user);
    Task<IdentityResult> Insert(UserModel user, string password);
    Task<IdentityResult> Update(UserModel user);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<IdentityResult> DeleteByIdAsync(string id);
    Task<bool> ExistsPermission(string permission, string userId);
    Task<IdentityResult> ChangePassword(UserModel user, string currentPassword, string newPassword);
}