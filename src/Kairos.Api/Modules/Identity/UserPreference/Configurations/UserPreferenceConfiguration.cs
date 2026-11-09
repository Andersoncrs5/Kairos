using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Identity.UserPreference.Configurations;

public sealed class UserPreferenceConfiguration
    : IEntityTypeConfiguration<UserPreferenceModel>
{
    public void Configure(EntityTypeBuilder<UserPreferenceModel> builder)
    {
        builder.ToTable("user_preferences");

        // Primary key
        builder.HasKey(preference => preference.Id);

        builder.Property(preference => preference.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // BaseModel
        builder.Property(preference => preference.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(preference => preference.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(preference => preference.UpdatedAt)
            .HasColumnName("updated_at");

        // User relationship
        builder.Property(preference => preference.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(preference => preference.User)
            .WithOne()
            .HasForeignKey<UserPreferenceModel>(
                preference => preference.UserId);

        builder.HasIndex(preference => preference.UserId)
            .IsUnique()
            .HasDatabaseName("ux_user_preferences_user_id");

        // Appearance
        builder.Property(preference => preference.Theme)
            .HasColumnName("theme")
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(UserThemeEnum.System)
            .IsRequired();

        builder.Property(preference => preference.CompactMode)
            .HasColumnName("compact_mode")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(preference => preference.ReducedMotion)
            .HasColumnName("reduced_motion")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(preference => preference.HighContrast)
            .HasColumnName("high_contrast")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(preference => preference.SidebarCollapsed)
            .HasColumnName("sidebar_collapsed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(preference => preference.AnimationsEnabled)
            .HasColumnName("animations_enabled")
            .HasDefaultValue(true)
            .IsRequired();

        // Localization
        builder.Property(preference => preference.Language)
            .HasColumnName("language")
            .HasMaxLength(32)
            .HasDefaultValue("pt-BR")
            .IsRequired();

        builder.Property(preference => preference.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(64)
            .HasDefaultValue("America/Fortaleza")
            .IsRequired();

        builder.Property(preference => preference.TimeFormat)
            .HasColumnName("time_format")
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(TimeFormatEnum.Hour24)
            .IsRequired();

        // Notifications
        builder.Property(preference => preference.NotificationsEnabled)
            .HasColumnName("notifications_enabled")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(preference => preference.EmailNotificationsEnabled)
            .HasColumnName("email_notifications_enabled")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(preference => preference.ReservationRemindersEnabled)
            .HasColumnName("reservation_reminders_enabled")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(preference => preference.ReservationUpdatesEnabled)
            .HasColumnName("reservation_updates_enabled")
            .HasDefaultValue(true)
            .IsRequired();
    }
}