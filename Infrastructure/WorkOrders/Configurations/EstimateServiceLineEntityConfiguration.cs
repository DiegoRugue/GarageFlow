using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.WorkOrders.Configurations;

public sealed class EstimateServiceLineEntityConfiguration : IEntityTypeConfiguration<EstimateServiceLine>
{
    public void Configure(EntityTypeBuilder<EstimateServiceLine> builder)
    {
        builder.ToTable("WorkOrderEstimateServiceLines");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .HasConversion(
                lineId => lineId.Value,
                value => EstimateServiceLineId.From(value))
            .ValueGeneratedNever();

        builder.Property(line => line.EstimateId)
            .HasConversion(
                estimateId => estimateId.Value,
                value => EstimateId.From(value))
            .IsRequired();

        builder.Property(line => line.ServiceId)
            .HasConversion(
                serviceId => serviceId.Value,
                value => ServiceId.From(value))
            .IsRequired();

        builder.Property(line => line.DescriptionSnapshot)
            .HasConversion(
                description => description.Value,
                value => Description.Create(value))
            .HasMaxLength(Description.MaxLength)
            .IsRequired();

        builder.Property(line => line.UnitPrice)
            .HasConversion(
                price => price.Value,
                value => Price.Create(value))
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(line => line.CreatedAt)
            .IsRequired();

        builder.Property(line => line.UpdatedAt)
            .IsRequired();

        builder.Ignore(line => line.TotalPrice);
        builder.Ignore(line => line.DomainEvents);
    }
}
