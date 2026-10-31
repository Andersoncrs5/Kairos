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

        // Room
        builder.Property(reservation => reservation.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.HasOne(reservation => reservation.Room)
            .WithMany()
            .HasForeignKey(reservation => reservation.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // User
        builder.Property(reservation => reservation.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(reservation => reservation.User)
            .WithMany()
            .HasForeignKey(reservation => reservation.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Category
        builder.Property(reservation => reservation.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.HasOne(reservation => reservation.Category)
            .WithMany()
            .HasForeignKey(reservation => reservation.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Title and description
        builder.Property(reservation => reservation.Title)
            .HasColumnName("title")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(reservation => reservation.Description)
            .HasColumnName("description");

        // Reservation period
        builder.Property(reservation => reservation.StartsAt)
            .HasColumnName("starts_at")
            .IsRequired();

        builder.Property(reservation => reservation.EndsAt)
            .HasColumnName("ends_at")
            .IsRequired();

        // Status
        builder.Property(reservation => reservation.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ReservationStatusEnum.Confirmed)
            .IsRequired();

        // Idempotency
        builder.Property(reservation => reservation.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100);

        // Cancellation
        builder.Property(reservation => reservation.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(reservation => reservation.CancelledBy)
            .HasColumnName("cancelled_by");

        builder.HasOne(reservation => reservation.Canceller)
            .WithMany()
            .HasForeignKey(reservation => reservation.CancelledBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(reservation => reservation.CancellationReason)
            .HasColumnName("cancellation_reason");

        // Check-in / Check-out
        builder.Property(reservation => reservation.CheckedInAt)
            .HasColumnName("checked_in_at");

        builder.Property(reservation => reservation.CheckedOutAt)
            .HasColumnName("checked_out_at");

        // Room period lookup
        builder.HasIndex(reservation => new
            {
                reservation.RoomId,
                reservation.StartsAt,
                reservation.EndsAt
            })
            .HasDatabaseName("ix_reservations_room_period");

        // User reservation lookup
        builder.HasIndex(reservation => new
            {
                reservation.UserId,
                reservation.StartsAt
            })
            .HasDatabaseName("ix_reservations_user_starts_at");

        // Idempotency
        builder.HasIndex(reservation => new
            {
                reservation.UserId,
                reservation.IdempotencyKey
            })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_reservations_user_idempotency_key");
    }
}