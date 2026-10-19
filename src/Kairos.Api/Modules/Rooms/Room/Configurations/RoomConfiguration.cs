
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.Room.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<RoomModel>
{
    public void Configure(EntityTypeBuilder<RoomModel> builder)
    {
        builder.ToTable("rooms", table =>
        {
            table.HasCheckConstraint(
                "ck_rooms_capacity",
                "capacity > 0");
        });

        builder.HasKey(room => room.Id);

        builder.Property(room => room.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(room => room.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(room => room.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(room => room.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(room => room.RoomTypeId)
            .HasColumnName("room_type_id")
            .IsRequired();

        builder.HasOne<RoomTypeModel>()
            .WithMany()
            .HasForeignKey(room => room.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(room => room.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(room => room.Location)
            .HasColumnName("location")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(room => room.Capacity)
            .HasColumnName("capacity")
            .IsRequired();

        builder.Property(room => room.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(room => room.RoomTypeId);
    }
}