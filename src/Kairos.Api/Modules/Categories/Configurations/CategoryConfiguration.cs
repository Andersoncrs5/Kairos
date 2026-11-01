using Kairos.Api.Modules.Categories.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Categories.Configurations;

public sealed class CategoryConfiguration
    : IEntityTypeConfiguration<CategoryModel>
{
    public void Configure(EntityTypeBuilder<CategoryModel> builder)
    {
        builder.ToTable("categories");

        // ----------------------------------------------------------
        // Primary Key
        // ----------------------------------------------------------

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // ----------------------------------------------------------
        // Audit
        // ----------------------------------------------------------

        builder.Property(category => category.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(category => category.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(category => category.UpdatedAt)
            .HasColumnName("updated_at");

        // ----------------------------------------------------------
        // Identification
        // ----------------------------------------------------------

        builder.Property(category => category.Code)
            .HasColumnName("code")
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(category => category.Code)
            .IsUnique();

        builder.Property(category => category.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(category => category.Name)
            .IsUnique();

        builder.Property(category => category.Description)
            .HasColumnName("description")
            .HasMaxLength(255);

        // ----------------------------------------------------------
        // Appearance
        // ----------------------------------------------------------

        builder.Property(category => category.Color)
            .HasColumnName("color")
            .HasMaxLength(12);

        builder.Property(category => category.Icon)
            .HasColumnName("icon")
            .HasMaxLength(50);

        // ----------------------------------------------------------
        // Organization
        // ----------------------------------------------------------

        builder.Property(category => category.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(category => category.DisplayOrder);

        // ----------------------------------------------------------
        // Status
        // ----------------------------------------------------------

        builder.Property(category => category.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // Useful for listing active categories in display order.
        builder.HasIndex(category => new
        {
            category.IsActive,
            category.DisplayOrder
        });
    }
}