using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Reservations.Reservation.Configurations;

public sealed class ReservationConfiguration
    : IEntityTypeConfiguration<ReservationModel>
{
    public void Configure(EntityTypeBuilder<ReservationModel> builder)
    {
        builder.ToTable("reservations", table =>
        {
            table.HasCheckConstraint(
                "ck_reservations_valid_period",
                "starts_at < ends_at");
        });

        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(reservation => reservation.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(reservation => reservation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(reservation => reservation.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(reservation => reservation.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.Property(reservation => reservation.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(reservation => reservation.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(reservation => reservation.Title)
            .HasColumnName("title")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(reservation => reservation.StartsAt)
            .HasColumnName("starts_at")
            .IsRequired();

        builder.Property(reservation => reservation.EndsAt)
            .HasColumnName("ends_at")
            .IsRequired();

        builder.Property(reservation => reservation.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ReservationStatusEnum.Confirmed)
            .IsRequired();

        builder.Property(reservation => reservation.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100);

        builder.HasIndex(reservation => new
        {
            reservation.UserId,
            reservation.IdempotencyKey
        })
        .IsUnique()
        .HasFilter("idempotency_key IS NOT NULL");
    }
}
