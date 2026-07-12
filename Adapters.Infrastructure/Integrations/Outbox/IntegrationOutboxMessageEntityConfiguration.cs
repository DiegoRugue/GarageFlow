using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxMessageEntityConfiguration
    : IEntityTypeConfiguration<IntegrationOutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<IntegrationOutboxMessageEntity> builder)
    {
        builder.ToTable("IntegrationOutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.EventKey).HasMaxLength(128).IsRequired();
        builder.Property(message => message.AggregateId).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.OccurredAt).IsRequired();
        builder.Property(message => message.CorrelationId).HasMaxLength(64);
        builder.Property(message => message.AttemptCount).HasDefaultValue(0).IsRequired();
        builder.Property(message => message.NextAttemptAt).IsRequired();
        builder.Property(message => message.ProcessedAt);
        builder.Property(message => message.LastError).HasMaxLength(1024);
        builder.Property(message => message.LeaseId);
        builder.Property(message => message.LeaseExpiresAt);
        builder.HasIndex(message => new { message.NextAttemptAt, message.OccurredAt, message.Id })
            .HasFilter("\"ProcessedAt\" IS NULL");
    }
}
