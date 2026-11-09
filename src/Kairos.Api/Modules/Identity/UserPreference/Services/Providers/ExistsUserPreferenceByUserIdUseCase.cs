using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Providers;

public sealed class ExistsUserPreferenceByUserIdUseCase(
    IUnitOfWork uow)
    : IExistsUserPreferenceByUserIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string userId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate User ID
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                userId,
                out string normalizedUserId))
        {
            return Result<bool>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 2. Check preference existence
        // ---------------------------------------------------------------

        bool exists = await uow.UserPreferenceRepository
            .ExistsByUserIdAsync(normalizedUserId, ct);

        return Result<bool>.Success(exists);
    }
}