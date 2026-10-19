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

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(category => category.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(category => category.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(category => category.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(category => category.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(category => category.Name)
            .IsUnique();

        builder.Property(category => category.Description)
            .HasColumnName("description")
            .HasMaxLength(255);

        builder.Property(category => category.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();
    }
}