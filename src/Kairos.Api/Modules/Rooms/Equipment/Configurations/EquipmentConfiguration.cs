using Kairos.Api.Modules.Rooms.Equipment.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.Equipment.Configurations;

public sealed class EquipmentConfiguration
    : IEntityTypeConfiguration<EquipmentModel>
{
    public void Configure(
        EntityTypeBuilder<EquipmentModel> builder)
    {
        builder.ToTable("equipments");

        // ----------------------------------------------------------
        // Primary Key
        // ----------------------------------------------------------

        builder.HasKey(equipment => equipment.Id);

        builder.Property(equipment => equipment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // ----------------------------------------------------------
        // BaseModel / Audit
        // ----------------------------------------------------------

        builder.Property(equipment => equipment.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(equipment => equipment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(equipment => equipment.UpdatedAt)
            .HasColumnName("updated_at");

        // ----------------------------------------------------------
        // Identification
        // ----------------------------------------------------------

        builder.Property(equipment => equipment.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(equipment => equipment.Code)
            .IsUnique();

        builder.Property(equipment => equipment.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(equipment => equipment.Name)
            .IsUnique();

        builder.Property(equipment => equipment.Description)
            .HasColumnName("description")
            .HasMaxLength(255);

        // ----------------------------------------------------------
        // Status
        // ----------------------------------------------------------

        builder.Property(equipment => equipment.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(equipment => equipment.IsActive);
    }
}