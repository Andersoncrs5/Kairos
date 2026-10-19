using Kairos.Api.Modules.Identity.Permissions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Identity.Permissions.Configurations;

public sealed class PermissionsConfiguration
    : IEntityTypeConfiguration<PermissionsModel>
{
    public void Configure(EntityTypeBuilder<PermissionsModel> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(permission => permission.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(permission => permission.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(permission => permission.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(permission => permission.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(permission => permission.IsSystem)
            .HasColumnName("is_system")
            .HasDefaultValue(false)
            .IsRequired();
        
        builder.Property(permission => permission.Module)
            .HasColumnName("module")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();  
        
        builder.HasIndex(permission => permission.Code)
            .IsUnique();

        builder.Property(permission => permission.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasColumnName("description")
            .HasMaxLength(255);

        builder.Property(permission => permission.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();
    }
}