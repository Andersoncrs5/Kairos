using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Requests;
using Kairos.Api.Modules.Identity.UserPreference.Mapper;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Providers;

public sealed class UpdateUserPreferenceUseCase(IUnitOfWork uow)
    : IUpdateUserPreferenceUseCase
{
    public async Task<Result<UserPreferenceModel>> ExecuteAsync(
        UpdateUserPreferenceCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<UserPreferenceModel>.ValidationFailure(
                ["User preference update command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid user preference update command.",
                out string[] errors))
        {
            return Result<UserPreferenceModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        UpdateUserPreferenceDTO dto = command.Dto;

        if (dto is null)
        {
            return Result<UserPreferenceModel>.ValidationFailure(
                ["User preference data is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                dto,
                "Invalid user preference data.",
                out errors))
        {
            return Result<UserPreferenceModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 3. Validate User ID
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.UserId,
                out string userId))
        {
            return Result<UserPreferenceModel>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Validate supplied fields
        // ---------------------------------------------------------------

        List<string> validationErrors = [];

        if (dto.Theme.HasValue &&
            !Enum.IsDefined(typeof(UserThemeEnum), dto.Theme.Value))
        {
            validationErrors.Add("Theme is invalid.");
        }

        if (dto.TimeFormat.HasValue &&
            !Enum.IsDefined(typeof(TimeFormatEnum), dto.TimeFormat.Value))
        {
            validationErrors.Add("Time format is invalid.");
        }

        if (dto.Language is not null &&
            string.IsNullOrWhiteSpace(dto.Language))
        {
            validationErrors.Add("Language cannot be empty.");
        }

        if (dto.TimeZoneId is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.TimeZoneId))
            {
                validationErrors.Add("Time zone cannot be empty.");
            }
            else
            {
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(
                        dto.TimeZoneId.Trim());
                }
                catch (TimeZoneNotFoundException)
                {
                    validationErrors.Add("Time zone is invalid.");
                }
                catch (InvalidTimeZoneException)
                {
                    validationErrors.Add("Time zone is invalid.");
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            return Result<UserPreferenceModel>.ValidationFailure(
                validationErrors.Distinct().ToArray());
        }

        // ---------------------------------------------------------------
        // 5. Ensure at least one field was provided
        // ---------------------------------------------------------------

        bool hasChanges =
            dto.Theme.HasValue ||
            dto.Language is not null ||
            dto.TimeZoneId is not null ||
            dto.TimeFormat.HasValue ||
            dto.CompactMode.HasValue ||
            dto.ReducedMotion.HasValue ||
            dto.HighContrast.HasValue ||
            dto.SidebarCollapsed.HasValue ||
            dto.AnimationsEnabled.HasValue ||
            dto.NotificationsEnabled.HasValue ||
            dto.EmailNotificationsEnabled.HasValue ||
            dto.ReservationRemindersEnabled.HasValue ||
            dto.ReservationUpdatesEnabled.HasValue;

        if (!hasChanges)
        {
            return Result<UserPreferenceModel>.ValidationFailure(
                ["At least one preference must be provided."]);
        }

        // ---------------------------------------------------------------
        // 6. Find existing preferences
        // ---------------------------------------------------------------

        UserPreferenceModel? preference =
            await uow.UserPreferenceRepository.GetByUserIdAsync(
                userId,
                ct);

        if (preference is null)
        {
            return Result<UserPreferenceModel>.NotFound(
                "User preferences not found.");
        }

        // ---------------------------------------------------------------
        // 7. Apply update using Mapper
        // ---------------------------------------------------------------

        UserPreferenceMapper.ApplyUpdate(dto, preference);

        // ---------------------------------------------------------------
        // 8. Persist changes
        // ---------------------------------------------------------------

        preference.UpdatedAt = DateTimeOffset.UtcNow;

        uow.UserPreferenceRepository.Update(preference);

        return Result<UserPreferenceModel>.Success(preference);
    }
}