
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Rooms.MaintenanceTicket.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.MaintenanceTicket.Configurations;

public sealed class MaintenanceTicketConfiguration
    : IEntityTypeConfiguration<MaintenanceTicketModel>
{
    public void Configure(
        EntityTypeBuilder<MaintenanceTicketModel> builder)
    {
        builder.ToTable("maintenance_tickets", table =>
        {
            // Title
            table.HasCheckConstraint(
                "ck_maintenance_tickets_title_not_empty",
                "length(trim(title)) > 0");

            // Description
            table.HasCheckConstraint(
                "ck_maintenance_tickets_description_not_empty",
                "length(trim(description)) > 0");

            // Status
            table.HasCheckConstraint(
                "ck_maintenance_tickets_valid_status",
                """
                status IN (
                    'Open',
                    'InProgress',
                    'Resolved',
                    'Cancelled'
                )
                """);

            // Category
            table.HasCheckConstraint(
                "ck_maintenance_tickets_valid_category",
                """
                category IN (
                    'Equipment',
                    'Electrical',
                    'Network',
                    'Furniture',
                    'AirConditioning',
                    'Plumbing',
                    'Cleaning',
                    'Structural',
                    'Other'
                )
                """);

            // Priority
            table.HasCheckConstraint(
                "ck_maintenance_tickets_valid_priority",
                """
                priority IN (
                    'Low',
                    'Normal',
                    'High',
                    'Critical'
                )
                """);
        });

        // ---------------------------------------------------------------
        // Primary key
        // ---------------------------------------------------------------

        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // ---------------------------------------------------------------
        // BaseModel
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(ticket => ticket.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(ticket => ticket.UpdatedAt)
            .HasColumnName("updated_at");

        // ---------------------------------------------------------------
        // Room
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.HasOne(ticket => ticket.Room)
            .WithMany()
            .HasForeignKey(ticket => ticket.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------
        // Reporter
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.ReportedBy)
            .HasColumnName("reported_by")
            .IsRequired();

        builder.HasOne(ticket => ticket.Reporter)
            .WithMany()
            .HasForeignKey(ticket => ticket.ReportedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------
        // Title
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.Title)
            .HasColumnName("title")
            .HasMaxLength(255)
            .IsRequired();

        // ---------------------------------------------------------------
        // Description
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.Description)
            .HasColumnName("description")
            .HasMaxLength(4000)
            .IsRequired();

        // ---------------------------------------------------------------
        // Category
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(MaintenanceCategoryEnum.Other)
            .IsRequired();

        // ---------------------------------------------------------------
        // Priority
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(MaintenancePriorityEnum.Normal)
            .IsRequired();

        // ---------------------------------------------------------------
        // Status
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(MaintenanceTicketStatus.Open)
            .IsRequired();

        // ---------------------------------------------------------------
        // DueAt
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.DueAt)
            .HasColumnName("due_at");

        // ---------------------------------------------------------------
        // Resolver
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.ResolvedBy)
            .HasColumnName("resolved_by");

        builder.HasOne(ticket => ticket.Resolver)
            .WithMany()
            .HasForeignKey(ticket => ticket.ResolvedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------
        // RoomBlockout
        // ---------------------------------------------------------------

        builder.Property(ticket => ticket.BlockoutId)
            .HasColumnName("blockout_id")
            .IsRequired();

        builder.HasOne(ticket => ticket.Blockout)
            .WithOne()
            .HasForeignKey<MaintenanceTicketModel>(
                ticket => ticket.BlockoutId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------
        // Indexes
        // ---------------------------------------------------------------

        builder.HasIndex(ticket => ticket.BlockoutId)
            .IsUnique()
            .HasDatabaseName(
                "ux_maintenance_tickets_blockout_id");

        builder.HasIndex(ticket => new
            {
                ticket.RoomId,
                ticket.Status,
                ticket.CreatedAt
            })
            .HasDatabaseName(
                "ix_maintenance_tickets_room_status_created_at");

        builder.HasIndex(ticket => new
            {
                ticket.ReportedBy,
                ticket.CreatedAt
            })
            .HasDatabaseName(
                "ix_maintenance_tickets_reported_by_created_at");

        builder.HasIndex(ticket => new
            {
                ticket.Status,
                ticket.CreatedAt
            })
            .HasDatabaseName(
                "ix_maintenance_tickets_status_created_at");

        builder.HasIndex(ticket => new
            {
                ticket.Priority,
                ticket.Status,
                ticket.DueAt
            })
            .HasDatabaseName(
                "ix_maintenance_tickets_priority_status_due_at");

        builder.HasIndex(ticket => new
            {
                ticket.Category,
                ticket.Status
            })
            .HasDatabaseName(
                "ix_maintenance_tickets_category_status");

        builder.HasIndex(ticket => ticket.ResolvedBy)
            .HasDatabaseName(
                "ix_maintenance_tickets_resolved_by");
    }
}
