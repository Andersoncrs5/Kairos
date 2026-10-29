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

            table.HasCheckConstraint(
                "ck_rooms_area_square_meters",
                "area_square_meters IS NULL OR area_square_meters > 0");
        });

        // Primary key
        builder.HasKey(room => room.Id);

        builder.Property(room => room.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // BaseModel
        builder.Property(room => room.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(room => room.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(room => room.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        // Room type relationship
        builder.Property(room => room.RoomTypeId)
            .HasColumnName("room_type_id")
            .IsRequired();

        builder.HasOne(room => room.RoomTypeModel)
            .WithMany()
            .HasForeignKey(room => room.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Identification
        builder.Property(room => room.Code)
            .HasColumnName("code")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(room => room.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        // Location
        builder.Property(room => room.Location)
            .HasColumnName("location")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(room => room.Floor)
            .HasColumnName("floor")
            .HasMaxLength(30);

        // Description
        builder.Property(room => room.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        // Capacity and area
        builder.Property(room => room.Capacity)
            .HasColumnName("capacity")
            .IsRequired();

        builder.Property(room => room.AreaSquareMeters)
            .HasColumnName("area_square_meters")
            .HasPrecision(10, 2);

        // Accessibility
        builder.Property(room => room.IsAccessible)
            .HasColumnName("is_accessible")
            .HasDefaultValue(false)
            .IsRequired();

        // Reservation
        builder.Property(room => room.IsReservable)
            .HasColumnName("is_reservable")
            .HasDefaultValue(true)
            .IsRequired();

        // Status
        builder.Property(room => room.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // Indexes
        builder.HasIndex(room => room.Code)
            .IsUnique();

        builder.HasIndex(room => room.RoomTypeId);
    }
}