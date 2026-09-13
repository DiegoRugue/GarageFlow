using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Configurations;

public sealed class WorkOrderEntityConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");

        builder.HasKey(workOrder => workOrder.Id);

        builder.Property(workOrder => workOrder.Id)
            .HasConversion(
                workOrderId => workOrderId.Value,
                value => WorkOrderId.From(value))
            .ValueGeneratedNever();

        builder.Property(workOrder => workOrder.CustomerId)
            .HasConversion(
                customerId => customerId.Value,
                value => CustomerId.From(value))
            .IsRequired();

        builder.Property(workOrder => workOrder.VehicleId)
            .HasConversion(
                vehicleId => vehicleId.Value,
                value => VehicleId.From(value))
            .IsRequired();

        builder.Property(workOrder => workOrder.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(workOrder => workOrder.StartedAt);

        builder.Property(workOrder => workOrder.CompletedAt);

        builder.Property(workOrder => workOrder.CreatedAt)
            .IsRequired();

        builder.Property(workOrder => workOrder.UpdatedAt)
            .IsRequired();

        builder.HasMany(workOrder => workOrder.Estimates)
            .WithOne()
            .HasForeignKey(estimate => estimate.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(workOrder => workOrder.Estimates)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(workOrder => workOrder.CustomerId);
        builder.HasIndex(workOrder => workOrder.VehicleId);
        builder.HasIndex(workOrder => workOrder.CompletedAt);
        builder.HasIndex(workOrder => workOrder.CreatedAt);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(workOrder => workOrder.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(workOrder => workOrder.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(workOrder => workOrder.DomainEvents);
    }
}
