using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Providers;

public sealed class ResetUserPreferenceUseCase(IUnitOfWork uow)
    : IResetUserPreferenceUseCase
{
    public async Task<Result<UserPreferenceModel>> ExecuteAsync(
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
            return Result<UserPreferenceModel>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 2. Find preferences
        // ---------------------------------------------------------------

        UserPreferenceModel? preference =
            await uow.UserPreferenceRepository.GetByUserIdAsync(
                normalizedUserId,
                ct);

        if (preference is null)
        {
            return Result<UserPreferenceModel>.NotFound(
                "User preferences not found.");
        }

        // ---------------------------------------------------------------
        // 3. Reset appearance
        // ---------------------------------------------------------------

        preference.Theme = UserThemeEnum.System;

        preference.CompactMode = false;
        preference.ReducedMotion = false;
        preference.HighContrast = false;
        preference.SidebarCollapsed = false;
        preference.AnimationsEnabled = true;

        // ---------------------------------------------------------------
        // 4. Reset localization
        // ---------------------------------------------------------------

        preference.Language = "pt-BR";
        preference.TimeZoneId = "America/Fortaleza";
        preference.TimeFormat = TimeFormatEnum.Hour24;

        // ---------------------------------------------------------------
        // 5. Reset notifications
        // ---------------------------------------------------------------

        preference.NotificationsEnabled = true;
        preference.EmailNotificationsEnabled = false;
        preference.ReservationRemindersEnabled = true;
        preference.ReservationUpdatesEnabled = true;

        // ---------------------------------------------------------------
        // 6. Persist changes
        // ---------------------------------------------------------------

        preference.UpdatedAt = DateTimeOffset.UtcNow;

        uow.UserPreferenceRepository.Update(preference);

        return Result<UserPreferenceModel>.Success(preference);
    }
}