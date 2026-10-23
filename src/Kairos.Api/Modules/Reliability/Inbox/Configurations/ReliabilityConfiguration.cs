using Kairos.Api.Modules.Reliability.Inbox.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Reliability.Inbox.Configurations;

public sealed class ReliabilityConfiguration
    : IEntityTypeConfiguration<InboxModel>
{
    public void Configure(EntityTypeBuilder<InboxModel> builder)
    {
        builder.ToTable("InboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MessageId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.MessageType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.Source)
            .HasMaxLength(256);

        builder.Property(x => x.Headers)
            .HasColumnType("jsonb");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ReceivedAt)
            .IsRequired();

        builder.Property(x => x.ProcessedAt);

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.NextAttemptAt);

        builder.Property(x => x.LastError)
            .HasColumnType("text");

        builder.Property(x => x.LockedBy)
            .HasMaxLength(256);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(256);

        builder.HasIndex(x => x.MessageId)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.Status,
            x.NextAttemptAt
        });

        builder.HasIndex(x => x.CorrelationId);
    }
}