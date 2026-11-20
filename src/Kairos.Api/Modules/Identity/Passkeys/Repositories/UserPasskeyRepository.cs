using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Passkeys.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Identity.Passkeys.Repositories;

public sealed class UserPasskeyRepository(AppDbContext context)
    : BaseRepositoryImpl<UserPasskeyModel>(context),
        IUserPasskeyRepository
{
    public Task<UserPasskeyModel?> GetByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        return context.Set<UserPasskeyModel>()
            .SingleOrDefaultAsync(
                passkey => passkey.CredentialId == credentialId,
                ct);
    }

    public Task<bool> ExistsByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        return context.Set<UserPasskeyModel>()
            .AsNoTracking()
            .AnyAsync(
                passkey => passkey.CredentialId == credentialId,
                ct);
    }

    public async Task<IReadOnlyList<UserPasskeyModel>> GetByUserIdAsync(
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return await context.Set<UserPasskeyModel>()
            .AsNoTracking()
            .Where(passkey => passkey.UserId == userId)
            .OrderBy(passkey => passkey.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<UserPasskeyModel?> GetByIdAndUserIdAsync(
        string id,
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return context.Set<UserPasskeyModel>()
            .SingleOrDefaultAsync(
                passkey => passkey.Id == id && passkey.UserId == userId,
                ct);
    }

    public Task<int> CountByUserIdAsync(
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return context.Set<UserPasskeyModel>()
            .AsNoTracking()
            .CountAsync(passkey => passkey.UserId == userId, ct);
    }
}