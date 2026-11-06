using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Configurations;

public sealed class RoomEquipmentConfiguration : IEntityTypeConfiguration<RoomEquipmentModel>
{
    public void Configure(EntityTypeBuilder<RoomEquipmentModel> builder)
    {
        builder.ToTable("room_equipments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoomId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(x => x.EquipmentId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(x => x.Quantity)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.Notes)
            .HasMaxLength(255);

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RoomId, x.EquipmentId })
            .IsUnique();
    }
}