using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendee.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Reservations.ReservationAttendee.Configurations;

public sealed class ReservationAttendeeConfiguration
    : IEntityTypeConfiguration<ReservationAttendeeModel>
{
    public void Configure(EntityTypeBuilder<ReservationAttendeeModel> builder)
    {
        builder.ToTable("reservation_attendees");

        builder.HasKey(attendee => attendee.Id);

        builder.Property(attendee => attendee.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(attendee => attendee.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(attendee => attendee.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(attendee => attendee.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(attendee => attendee.ReservationId)
            .HasColumnName("reservation_id")
            .IsRequired();

        builder.HasOne<ReservationModel>()
            .WithMany()
            .HasForeignKey(attendee => attendee.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(attendee => attendee.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne<UserModel>()
            .WithMany()
            .HasForeignKey(attendee => attendee.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(attendee => attendee.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AttendeeStatus.Pending)
            .IsRequired();

        builder.HasIndex(attendee => new
            {
                attendee.ReservationId,
                attendee.UserId
            })
            .IsUnique();
    }
}