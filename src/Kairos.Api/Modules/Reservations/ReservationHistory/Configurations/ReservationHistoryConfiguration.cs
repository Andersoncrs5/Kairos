using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationHistory.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Reservations.ReservationHistory.Configurations;

public sealed class ReservationHistoryConfiguration
    : IEntityTypeConfiguration<ReservationHistoryModel>
{
    public void Configure(EntityTypeBuilder<ReservationHistoryModel> builder)
    {
        builder.ToTable("reservation_history");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(history => history.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(history => history.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(history => history.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(history => history.ReservationId)
            .HasColumnName("reservation_id")
            .IsRequired();

        builder.HasOne<ReservationModel>()
            .WithMany()
            .HasForeignKey(history => history.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(history => history.ActorUserId)
            .HasColumnName("actor_user_id")
            .IsRequired();

        builder.HasOne<UserModel>()
            .WithMany()
            .HasForeignKey(history => history.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(history => history.Action)
            .HasColumnName("action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(history => history.OldValues)
            .HasColumnName("old_values")
            .HasColumnType("jsonb");

        builder.Property(history => history.NewValues)
            .HasColumnName("new_values")
            .HasColumnType("jsonb");

        builder.HasIndex(history => history.ReservationId);

        builder.HasIndex(history => new
        {
            history.ReservationId,
            history.CreatedAt
        });
    }
}