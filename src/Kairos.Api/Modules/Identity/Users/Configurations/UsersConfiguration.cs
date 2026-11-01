using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Identity.Users.Configurations;

public sealed class UsersConfiguration
    : IEntityTypeConfiguration<UserModel>
{
    public void Configure(EntityTypeBuilder<UserModel> entity)
    {
        entity.ToTable("app_users");

        entity.Property(user => user.FullName)
            .HasMaxLength(250)
            .IsRequired(false);

        entity.Property(user => user.Permissions)
            .HasColumnName("permissions")
            .HasColumnType("text[]")
            .IsRequired();

        entity.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at");

        entity.Property(user => user.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        entity.Property(user => user.Version)
            .HasColumnName("version")
            .HasDefaultValue(1L)
            .IsRequired();
    }
}