using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.UserPreference.Models;

public sealed class UserPreferenceModel : BaseModel
{
    public required string UserId { get; set; }

    public UserModel? User { get; set; }

    // Appearance
    public UserThemeEnum Theme { get; set; } = UserThemeEnum.System;

    public bool CompactMode { get; set; } = false;

    public bool ReducedMotion { get; set; } = false;

    public bool HighContrast { get; set; } = false;

    public bool SidebarCollapsed { get; set; } = false;

    public bool AnimationsEnabled { get; set; } = true;

    // Localization
    public string Language { get; set; } = "pt-BR";

    public string TimeZoneId { get; set; } = "America/Fortaleza";

    public TimeFormatEnum TimeFormat { get; set; } = TimeFormatEnum.Hour24;

    // Notifications
    public bool NotificationsEnabled { get; set; } = true;

    public bool EmailNotificationsEnabled { get; set; } = false;

    public bool ReservationRemindersEnabled { get; set; } = true;

    public bool ReservationUpdatesEnabled { get; set; } = true;
}