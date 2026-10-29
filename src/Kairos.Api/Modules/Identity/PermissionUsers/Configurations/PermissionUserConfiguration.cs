
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Configurations;

public sealed class PermissionUserConfiguration
    : IEntityTypeConfiguration<PermissionUserModel>
{
    public void Configure(EntityTypeBuilder<PermissionUserModel> builder)
    {
        builder.ToTable("permissions_users");

        builder.HasKey(permissionUser => permissionUser.Id);

        builder.Property(permissionUser => permissionUser.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(permissionUser => permissionUser.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(permissionUser => permissionUser.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(permissionUser => permissionUser.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(permissionUser => permissionUser.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(permissionUser => permissionUser.PermissionId)
            .HasColumnName("permission_id")
            .IsRequired();

        builder.Property(permissionUser => permissionUser.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(permissionUser => new
        {
            permissionUser.UserId,
            permissionUser.PermissionId
        }).IsUnique();

        builder
            .HasOne(permissionUser => permissionUser.User)
            .WithMany()
            .HasForeignKey(permissionUser => permissionUser.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(permissionUser => permissionUser.Permissions)
            .WithMany()
            .HasForeignKey(permissionUser => permissionUser.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
