using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.UserPreference.DTOs.Requests;

public sealed class UpdateUserPreferenceDTO
{
    public UserThemeEnum? Theme { get; set; }

    public string? Language { get; set; }

    public string? TimeZoneId { get; set; }

    public TimeFormatEnum? TimeFormat { get; set; }

    public bool? CompactMode { get; set; }

    public bool? ReducedMotion { get; set; }

    public bool? HighContrast { get; set; }

    public bool? SidebarCollapsed { get; set; }

    public bool? AnimationsEnabled { get; set; }

    public bool? NotificationsEnabled { get; set; }

    public bool? EmailNotificationsEnabled { get; set; }

    public bool? ReservationRemindersEnabled { get; set; }

    public bool? ReservationUpdatesEnabled { get; set; }
}