using Kairos.Api.Modules.Identity.Passkeys.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Identity.Passkeys.Configurations;

public sealed class UserPasskeyConfiguration
    : IEntityTypeConfiguration<UserPasskeyModel>
{
    public void Configure(EntityTypeBuilder<UserPasskeyModel> builder)
    {
        builder.ToTable("user_passkeys");

        builder.HasKey(passkey => passkey.Id);

        builder.Property(passkey => passkey.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(passkey => passkey.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(passkey => passkey.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(passkey => passkey.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(passkey => passkey.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(passkey => passkey.User)
            .WithMany()
            .HasForeignKey(passkey => passkey.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(passkey => passkey.UserId)
            .HasDatabaseName("ix_user_passkeys_user_id");

        builder.Property(passkey => passkey.CredentialId)
            .HasColumnName("credential_id")
            .IsRequired();

        builder.HasIndex(passkey => passkey.CredentialId)
            .IsUnique()
            .HasDatabaseName("ux_user_passkeys_credential_id");

        builder.Property(passkey => passkey.PublicKey)
            .HasColumnName("public_key")
            .IsRequired();

        builder.Property(passkey => passkey.SignCount)
            .HasColumnName("sign_count")
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(passkey => passkey.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(passkey => passkey.IsBackupEligible)
            .HasColumnName("is_backup_eligible")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(passkey => passkey.IsBackedUp)
            .HasColumnName("is_backed_up")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(passkey => passkey.LastUsedAt)
            .HasColumnName("last_used_at");
    }
}