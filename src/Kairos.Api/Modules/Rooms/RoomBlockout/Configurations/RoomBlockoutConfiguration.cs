using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Configurations;

public sealed class RoomBlockoutConfiguration
    : IEntityTypeConfiguration<RoomBlockoutModel>
{
    public void Configure(EntityTypeBuilder<RoomBlockoutModel> builder)
    {
        builder.ToTable("room_blockouts", table =>
        {
            table.HasCheckConstraint(
                "ck_room_blockouts_valid_period",
                "starts_at < ends_at");
        });

        builder.HasKey(blockout => blockout.Id);

        builder.Property(blockout => blockout.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(blockout => blockout.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(blockout => blockout.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(blockout => blockout.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(blockout => blockout.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.HasOne<RoomModel>()
            .WithMany()
            .HasForeignKey(blockout => blockout.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(blockout => blockout.Reason)
            .HasColumnName("reason")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(blockout => blockout.StartsAt)
            .HasColumnName("starts_at")
            .IsRequired();

        builder.Property(blockout => blockout.EndsAt)
            .HasColumnName("ends_at")
            .IsRequired();

        builder.Property(blockout => blockout.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.HasOne<UserModel>()
            .WithMany()
            .HasForeignKey(blockout => blockout.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(blockout => blockout.RoomId);

        builder.HasIndex(blockout => new
        {
            blockout.RoomId,
            blockout.StartsAt,
            blockout.EndsAt
        });
    }
}