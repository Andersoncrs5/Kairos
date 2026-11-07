
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Configuration;

public sealed class ReservationAttendeeConfiguration
    : IEntityTypeConfiguration<ReservationAttendeeModel>
{
    public void Configure(EntityTypeBuilder<ReservationAttendeeModel> builder)
    {
        builder.ToTable("reservation_attendees");

        builder.HasKey(x => x.Id);

        // ---------------------------------------------------------------
        // Properties
        // ---------------------------------------------------------------

        builder.Property(x => x.ReservationId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Role)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.InvitedBy)
            .IsRequired(false);

        builder.Property(x => x.ResponseNote)
            .IsRequired(false);

        builder.Property(x => x.RespondedAt)
            .IsRequired(false);

        builder.Property(x => x.CheckedInAt)
            .IsRequired(false);

        // ---------------------------------------------------------------
        // Relationships
        // ---------------------------------------------------------------

        builder.HasOne(x => x.Reservation)
            .WithMany()
            .HasForeignKey(x => x.ReservationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Inviter)
            .WithMany()
            .HasForeignKey(x => x.InvitedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------
        // Indexes
        // ---------------------------------------------------------------

        builder.HasIndex(x => new
        {
            x.ReservationId,
            x.UserId
        })
        .IsUnique();

        builder.HasIndex(x => x.UserId);
    }
}
