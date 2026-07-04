using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Configurations;

public sealed class EstimateEntityConfiguration : IEntityTypeConfiguration<Estimate>
{
    public void Configure(EntityTypeBuilder<Estimate> builder)
    {
        builder.ToTable("WorkOrderEstimates");

        builder.HasKey(estimate => estimate.Id);

        builder.Property(estimate => estimate.Id)
            .HasConversion(
                estimateId => estimateId.Value,
                value => EstimateId.From(value))
            .ValueGeneratedNever();

        builder.Property(estimate => estimate.WorkOrderId)
            .HasConversion(
                workOrderId => workOrderId.Value,
                value => WorkOrderId.From(value))
            .IsRequired();

        builder.Property(estimate => estimate.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(estimate => estimate.CreatedAt)
            .IsRequired();

        builder.Property(estimate => estimate.UpdatedAt)
            .IsRequired();

        builder.HasIndex(estimate => new { estimate.WorkOrderId, estimate.Status });

        builder.HasIndex(estimate => estimate.WorkOrderId)
            .HasDatabaseName("UX_WorkOrderEstimates_WorkOrderId_Approved")
            .IsUnique()
            .HasFilter("\"Status\" = 'Approved'");

        builder.HasMany(estimate => estimate.InventoryLines)
            .WithOne()
            .HasForeignKey(line => line.EstimateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(estimate => estimate.InventoryLines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(estimate => estimate.ServiceLines)
            .WithOne()
            .HasForeignKey(line => line.EstimateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(estimate => estimate.ServiceLines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(estimate => estimate.TotalAmount);
        builder.Ignore(estimate => estimate.DomainEvents);
    }
}
