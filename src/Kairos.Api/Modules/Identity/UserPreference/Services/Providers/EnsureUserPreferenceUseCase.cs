using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Providers;

public sealed class EnsureUserPreferenceUseCase(IUnitOfWork uow)
    : IEnsureUserPreferenceUseCase
{
    public async Task<Result<UserPreferenceModel>> ExecuteAsync(
        CreateUserPreferenceCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<UserPreferenceModel>.ValidationFailure(
                ["User preference command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid user preference command.",
                out string[] errors))
        {
            return Result<UserPreferenceModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate User ID
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.UserId,
                out string userId))
        {
            return Result<UserPreferenceModel>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find user
        // ---------------------------------------------------------------

        var user = await uow.UserRepository.GetByIdAsync(userId);

        if (user is null)
        {
            return Result<UserPreferenceModel>.NotFound(
                "User not found.");
        }

        // ---------------------------------------------------------------
        // 4. Check existing preferences
        // ---------------------------------------------------------------

        UserPreferenceModel? existing =
            await uow.UserPreferenceRepository.GetByUserIdAsync(
                userId,
                ct);

        if (existing is not null)
        {
            return Result<UserPreferenceModel>.Success(existing);
        }

        // ---------------------------------------------------------------
        // 5. Create default preferences
        // ---------------------------------------------------------------

        ct.ThrowIfCancellationRequested();

        UserPreferenceModel preference = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // ---------------------------------------------------------------
        // 6. Persist
        // ---------------------------------------------------------------

        await uow.UserPreferenceRepository.AddAsync(
            preference,
            ct);

        return Result<UserPreferenceModel>.Created(preference);
    }
}