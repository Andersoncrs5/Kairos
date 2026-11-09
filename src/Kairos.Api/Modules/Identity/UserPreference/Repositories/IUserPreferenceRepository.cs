using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Identity.UserPreference.Models;

namespace Kairos.Api.Modules.Identity.UserPreference.Repositories;

public interface IUserPreferenceRepository
    : IBaseRepository<UserPreferenceModel>
{
    Task<UserPreferenceModel?> GetByUserIdAsync(
        string userId,
        CancellationToken ct = default);
    
    Task<bool> ExistsByUserIdAsync(
        string userId,
        CancellationToken ct = default);
}