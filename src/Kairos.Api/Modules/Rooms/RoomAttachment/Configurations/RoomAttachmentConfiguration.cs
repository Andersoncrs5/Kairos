using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Configurations;

public sealed class RoomAttachmentConfiguration
    : IEntityTypeConfiguration<RoomAttachmentModel>
{
    public void Configure(EntityTypeBuilder<RoomAttachmentModel> builder)
    {
        builder.ToTable("room_attachments", table =>
        {
            table.HasCheckConstraint(
                "ck_room_attachments_size_positive",
                "size_bytes > 0");

            table.HasCheckConstraint(
                "ck_room_attachments_sort_order_non_negative",
                "sort_order >= 0");

            // Só foto pode ser capa
            table.HasCheckConstraint(
                "ck_room_attachments_cover_only_photo",
                "is_cover = false OR type = 'Photo'");
        });

        // Primary key
        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // BaseModel
        builder.Property(attachment => attachment.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(attachment => attachment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(attachment => attachment.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(attachment => attachment.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.HasOne(attachment => attachment.Room)
            .WithMany()
            .HasForeignKey(attachment => attachment.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(attachment => attachment.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Arquivo
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

        builder.Property(attachment => attachment.AltText)
            .HasColumnName("alt_text")
            .HasMaxLength(255);

        builder.Property(attachment => attachment.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(attachment => attachment.IsCover)
            .HasColumnName("is_cover")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(attachment => attachment.CreatedBy)
            .HasColumnName("created_by");

        builder.HasOne(attachment => attachment.Creator)
            .WithMany()
            .HasForeignKey(attachment => attachment.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(attachment => attachment.StorageKey)
            .IsUnique()
            .HasDatabaseName("ux_room_attachments_storage_key");

        builder.HasIndex(attachment => new
            {
                attachment.RoomId,
                attachment.Type,
                attachment.SortOrder
            })
            .HasDatabaseName("ix_room_attachments_room_type_sort");

        builder.HasIndex(attachment => attachment.RoomId)
            .IsUnique()
            .HasFilter("is_cover = true")
            .HasDatabaseName("ux_room_attachments_room_cover");

        builder.HasIndex(attachment => attachment.CreatedBy)
            .HasDatabaseName("ix_room_attachments_created_by");
    }
}