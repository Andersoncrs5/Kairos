using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Configurations;

public sealed class RoomFeedbackConfiguration
    : IEntityTypeConfiguration<RoomFeedbackModel>
{
    public void Configure(EntityTypeBuilder<RoomFeedbackModel> builder)
    {
        builder.ToTable("room_feedbacks", table =>
        {
            table.HasCheckConstraint(
                "ck_room_feedbacks_rating_range",
                "rating >= 1 AND rating <= 5");
        });

        builder.HasKey(feedback => feedback.Id);

        builder.Property(feedback => feedback.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(feedback => feedback.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(feedback => feedback.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(feedback => feedback.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(feedback => feedback.ReservationId)
            .HasColumnName("reservation_id")
            .IsRequired();

        builder.HasOne(feedback => feedback.Reservation)
            .WithMany()
            .HasForeignKey(feedback => feedback.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(feedback => feedback.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.HasOne(feedback => feedback.Room)
            .WithMany()
            .HasForeignKey(feedback => feedback.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(feedback => feedback.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(feedback => feedback.User)
            .WithMany()
            .HasForeignKey(feedback => feedback.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(feedback => feedback.Rating)
            .HasColumnName("rating")
            .IsRequired();

        builder.Property(feedback => feedback.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(feedback => feedback.IssueType)
            .HasColumnName("issue_type")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(feedback => new
            {
                feedback.ReservationId,
                feedback.UserId
            })
            .IsUnique()
            .HasDatabaseName("ux_room_feedbacks_reservation_user");

        builder.HasIndex(feedback => new
            {
                feedback.RoomId,
                feedback.CreatedAt
            })
            .HasDatabaseName("ix_room_feedbacks_room_created_at");

        builder.HasIndex(feedback => feedback.UserId)
            .HasDatabaseName("ix_room_feedbacks_user_id");
    }
}