using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Providers;

public sealed class CreateUserPreferenceUseCase(IUnitOfWork uow)
    : ICreateUserPreferenceUseCase
{
    public async Task<Result<UserPreferenceModel>> ExecuteAsync(
        CreateUserPreferenceCommand input,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (input is null)
            return Result<UserPreferenceModel>.ValidationFailure(["User preference command is required."]);

        if (!UseCaseValidation.TryValidate(input, "Invalid user preference command.", out string[] errors))
            return Result<UserPreferenceModel>.ValidationFailure(errors);

        // ---------------------------------------------------------------
        // 2. Validate User ID
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(input.UserId, out string userId))
            return Result<UserPreferenceModel>.ValidationFailure(["User ID must be a valid UUID."]);
        
        // ---------------------------------------------------------------
        // 3. Find user
        // ---------------------------------------------------------------

        var user = await uow.UserRepository.GetByIdAsync(userId);

        if (user is null)
            return Result<UserPreferenceModel>.NotFound("User not found.");

        // ---------------------------------------------------------------
        // 4. Check existing preferences
        // ---------------------------------------------------------------

        UserPreferenceModel? existing = await uow.UserPreferenceRepository.GetByUserIdAsync(userId, ct);

        if (existing is not null)
        {
            return Result<UserPreferenceModel>.Conflict(
                "User preferences already exist.");
        }

        // ---------------------------------------------------------------
        // 5. Create default preferences
        // ---------------------------------------------------------------

        ct.ThrowIfCancellationRequested();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserPreferenceModel preference = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Version = 1,
            CreatedAt = now
        };
        
        await uow.UserPreferenceRepository.AddAsync(preference, ct);

        return Result<UserPreferenceModel>.Created(preference);
    }
}