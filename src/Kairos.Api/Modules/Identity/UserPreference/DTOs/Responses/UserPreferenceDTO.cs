using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Identity.UserPreference.DTOs.Responses;

public sealed class UserPreferenceDTO : BaseDTO
{
    public required string UserId { get; set; }

    public required string Theme { get; set; }

    public bool CompactMode { get; set; }

    public bool ReducedMotion { get; set; }

    public bool HighContrast { get; set; }

    public bool SidebarCollapsed { get; set; }

    public bool AnimationsEnabled { get; set; }

    public required string Language { get; set; }

    public required string TimeZoneId { get; set; }

    public required string TimeFormat { get; set; }

    public bool NotificationsEnabled { get; set; }

    public bool EmailNotificationsEnabled { get; set; }

    public bool ReservationRemindersEnabled { get; set; }

    public bool ReservationUpdatesEnabled { get; set; }
}