using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Services.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Services.Configurations;

public sealed class ServiceEntityConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");

        builder.HasKey(service => service.Id);

        builder.Property(service => service.Id)
            .HasConversion(
                serviceId => serviceId.Value,
                value => ServiceId.From(value))
            .ValueGeneratedNever();

        builder.Property(service => service.Description)
            .HasConversion(
                description => description.Value,
                value => ServiceDescription.Create(value))
            .HasMaxLength(ServiceDescription.MaxLength)
            .IsRequired();

        builder.Property(service => service.Price)
            .HasConversion(
                price => price.Value,
                value => ServicePrice.Create(value))
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(service => service.CreatedAt)
            .IsRequired();

        builder.Property(service => service.UpdatedAt)
            .IsRequired();

        builder.Ignore(service => service.DomainEvents);
    }
}

