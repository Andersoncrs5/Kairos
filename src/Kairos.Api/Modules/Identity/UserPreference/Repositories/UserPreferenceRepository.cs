using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.UserPreference.Repositories;

public sealed class UserPreferenceRepository(AppDbContext context)
    : BaseRepositoryImpl<UserPreferenceModel>(context),
        IUserPreferenceRepository
{
    public Task<UserPreferenceModel?> GetByUserIdAsync(
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return context.Set<UserPreferenceModel>()
            .SingleOrDefaultAsync(
                preference => preference.UserId == userId,
                ct);
    }
    
    public Task<bool> ExistsByUserIdAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return context.Set<UserPreferenceModel>()
            .AsNoTracking()
            .AnyAsync(
                preference => preference.UserId == userId,
                ct);
    }
}