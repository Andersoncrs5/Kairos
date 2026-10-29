using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Configurations;

public sealed class RoomBlockoutConfiguration
    : IEntityTypeConfiguration<RoomBlockoutModel>
{
    public void Configure(
        EntityTypeBuilder<RoomBlockoutModel> builder)
    {
        builder.ToTable("room_blockouts", table =>
        {
            table.HasCheckConstraint(
                "ck_room_blockouts_valid_period",
                "starts_at < ends_at");
        });

        // Primary key
        builder.HasKey(blockout => blockout.Id);

        builder.Property(blockout => blockout.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // BaseModel
        builder.Property(blockout => blockout.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(blockout => blockout.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(blockout => blockout.UpdatedAt)
            .HasColumnName("updated_at");

        // Room
        builder.Property(blockout => blockout.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.HasOne(blockout => blockout.Room)
            .WithMany()
            .HasForeignKey(blockout => blockout.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Type
        builder.Property(blockout => blockout.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Status
        builder.Property(blockout => blockout.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Reason
        builder.Property(blockout => blockout.Reason)
            .HasColumnName("reason")
            .HasMaxLength(255)
            .IsRequired();

        // Notes
        builder.Property(blockout => blockout.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        // Period
        builder.Property(blockout => blockout.StartsAt)
            .HasColumnName("starts_at")
            .IsRequired();

        builder.Property(blockout => blockout.EndsAt)
            .HasColumnName("ends_at")
            .IsRequired();

        // Cancellation
        builder.Property(blockout => blockout.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(blockout => blockout.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(255);

        builder.Property(blockout => blockout.CancelledBy)
            .HasColumnName("cancelled_by");

        builder.HasOne(blockout => blockout.Canceller)
            .WithMany()
            .HasForeignKey(blockout => blockout.CancelledBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Audit
        builder.Property(blockout => blockout.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.HasOne(blockout => blockout.Creater)
            .WithMany()
            .HasForeignKey(blockout => blockout.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(blockout => blockout.RoomId);

        builder.HasIndex(blockout => new
        {
            blockout.RoomId,
            blockout.StartsAt,
            blockout.EndsAt
        });

        builder.HasIndex(blockout => new
        {
            blockout.RoomId,
            blockout.Status,
            blockout.StartsAt
        });
    }
}