using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Configurations;

public sealed class ReservationAttachmentConfiguration
    : IEntityTypeConfiguration<ReservationAttachmentModel>
{
    public void Configure(EntityTypeBuilder<ReservationAttachmentModel> builder)
    {
        builder.ToTable("reservation_attachments", table =>
        {
            table.HasCheckConstraint(
                "ck_reservation_attachments_size_positive",
                "size_bytes > 0");
        });
        
        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        
        builder.Property(attachment => attachment.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(attachment => attachment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(attachment => attachment.UpdatedAt)
            .HasColumnName("updated_at");
        
        builder.Property(attachment => attachment.ReservationId)
            .HasColumnName("reservation_id")
            .IsRequired();

        builder.HasOne(attachment => attachment.Reservation)
            .WithMany()
            .HasForeignKey(attachment => attachment.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Property(attachment => attachment.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(attachment => attachment.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(attachment => attachment.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(attachment => attachment.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(attachment => attachment.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(64)
            .IsRequired();
        
        builder.Property(attachment => attachment.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(attachment => attachment.Description)
            .HasColumnName("description")
            .HasMaxLength(500);
        
        builder.Property(attachment => attachment.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        
        builder.Property(attachment => attachment.ScanStatus)
            .HasColumnName("scan_status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(attachment => attachment.ScannedAt)
            .HasColumnName("scanned_at");
        
        builder.Property(attachment => attachment.UploadedBy)
            .HasColumnName("uploaded_by");

        builder.HasOne(attachment => attachment.Uploader)
            .WithMany()
            .HasForeignKey(attachment => attachment.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasIndex(attachment => attachment.StorageKey)
            .IsUnique()
            .HasDatabaseName("ux_reservation_attachments_storage_key");
        
        builder.HasIndex(attachment => new
            {
                attachment.ReservationId,
                attachment.CreatedAt
            })
            .HasDatabaseName("ix_reservation_attachments_reservation_created");

        builder.HasIndex(attachment => attachment.UploadedBy)
            .HasDatabaseName("ix_reservation_attachments_uploaded_by");
    }
}