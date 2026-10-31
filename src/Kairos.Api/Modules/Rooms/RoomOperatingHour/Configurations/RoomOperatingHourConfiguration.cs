using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Configurations;

public sealed class RoomOperatingHourConfiguration
    : IEntityTypeConfiguration<RoomOperatingHourModel>
{
    public void Configure(EntityTypeBuilder<RoomOperatingHourModel> builder)
    {
        builder.ToTable("room_operating_hours");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.Property(x => x.DayOfWeek)
            .HasColumnName("day_of_week")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OpensAt)
            .HasColumnName("opens_at")
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(x => x.ClosesAt)
            .HasColumnName("closes_at")
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(x => x.IsClosed)
            .HasColumnName("is_closed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes");

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new
        {
            x.RoomId,
            x.DayOfWeek,
            x.IsActive
        });
    }
}