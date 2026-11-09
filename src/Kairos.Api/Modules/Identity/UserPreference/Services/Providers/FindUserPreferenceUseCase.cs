using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Providers;

public sealed class FindUserPreferenceUseCase(IUnitOfWork uow)
    : IFindUserPreferenceUseCase
{
    public async Task<Result<UserPreferenceModel>> ExecuteAsync(
        string userId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate User ID
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(userId, out string normalizedUserId))
            return Result<UserPreferenceModel>.ValidationFailure(["User ID must be a valid UUID."]);

        // ---------------------------------------------------------------
        // 2. Find preferences
        // ---------------------------------------------------------------

        UserPreferenceModel? preference = await uow.UserPreferenceRepository.GetByUserIdAsync(normalizedUserId, ct);

        if (preference is null) 
            return Result<UserPreferenceModel>.NotFound("User preferences not found.");

        return Result<UserPreferenceModel>.Success(preference);
    }
}