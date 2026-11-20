using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Identity.Passkeys.Models;

namespace Kairos.Api.Modules.Identity.Passkeys.Repositories;

public interface IUserPasskeyRepository : IBaseRepository<UserPasskeyModel>
{
    Task<UserPasskeyModel?> GetByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken ct = default);

    Task<bool> ExistsByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken ct = default);

    Task<IReadOnlyList<UserPasskeyModel>> GetByUserIdAsync(
        string userId,
        CancellationToken ct = default);

    Task<UserPasskeyModel?> GetByIdAndUserIdAsync(
        string id,
        string userId,
        CancellationToken ct = default);

    Task<int> CountByUserIdAsync(
        string userId,
        CancellationToken ct = default);
}