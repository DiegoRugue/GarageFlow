using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Idempotency;

public sealed class IntakeRequestReceiptConfiguration : IEntityTypeConfiguration<IntakeRequestReceipt>
{
    public void Configure(EntityTypeBuilder<IntakeRequestReceipt> builder)
    {
        builder.ToTable("WorkOrderIntakeRequests");
        builder.HasKey(receipt => receipt.RequestId);
        builder.Property(receipt => receipt.RequestId).ValueGeneratedNever();
        builder.Property(receipt => receipt.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.WorkOrderId);
        builder.Property(receipt => receipt.ResponseJson).HasColumnType("jsonb");
        builder.Property(receipt => receipt.CreatedAt).IsRequired();
        builder.Property(receipt => receipt.CompletedAt);
        builder.HasIndex(receipt => receipt.WorkOrderId);
    }
}
