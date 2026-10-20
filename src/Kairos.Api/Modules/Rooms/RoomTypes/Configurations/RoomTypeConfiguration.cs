using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Configurations;

public sealed class RoomTypeConfiguration
    : IEntityTypeConfiguration<RoomTypeModel>
{
    public void Configure(EntityTypeBuilder<RoomTypeModel> builder)
    {
        builder.HasKey(roomType => roomType.Id);

        builder.Property(roomType => roomType.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(roomType => roomType.Version)
            .HasColumnName("version")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(roomType => roomType.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(roomType => roomType.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(roomType => roomType.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(roomType => roomType.Name)
            .IsUnique();

        builder.Property(roomType => roomType.Description)
            .HasColumnName("description")
            .HasMaxLength(255);

        builder.Property(roomType => roomType.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();
    }
}