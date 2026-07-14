using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Inbox;

public sealed class EstimateDecisionInboxEventConfiguration : IEntityTypeConfiguration<EstimateDecisionInboxEvent>
{
    public void Configure(EntityTypeBuilder<EstimateDecisionInboxEvent> builder)
    {
        builder.ToTable("EstimateDecisionInboxEvents");
        builder.HasKey(item => item.EventId);
        builder.Property(item => item.EventId).ValueGeneratedNever();
        builder.Property(item => item.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.OccurredAt).IsRequired();
        builder.Property(item => item.ReceivedAt).IsRequired();
    }
}
