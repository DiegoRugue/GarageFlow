using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Adapters.Infrastructure.Vehicles.Configurations;

public sealed class VehicleBrandEntityConfiguration : IEntityTypeConfiguration<VehicleBrand>
{
    public void Configure(EntityTypeBuilder<VehicleBrand> builder)
    {
        builder.ToTable("VehicleBrands");

        builder.HasKey(vehicleBrand => vehicleBrand.Id);

        builder.Property(vehicleBrand => vehicleBrand.Id)
            .HasConversion(
                vehicleBrandId => vehicleBrandId.Value,
                value => VehicleBrandId.From(value))
            .ValueGeneratedNever();

        builder.Property(vehicleBrand => vehicleBrand.Name)
            .HasConversion(
                name => name.Value,
                value => VehicleBrandName.Create(value))
            .HasMaxLength(VehicleBrandName.MaxLength)
            .IsRequired();

        builder.HasIndex(vehicleBrand => vehicleBrand.Name)
            .IsUnique();

        builder.Property(vehicleBrand => vehicleBrand.CreatedAt)
            .IsRequired();

        builder.Property(vehicleBrand => vehicleBrand.UpdatedAt)
            .IsRequired();

        builder.Ignore(vehicleBrand => vehicleBrand.DomainEvents);
    }
}
