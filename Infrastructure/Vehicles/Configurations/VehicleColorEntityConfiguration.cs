using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Vehicles.Configurations;

public sealed class VehicleColorEntityConfiguration : IEntityTypeConfiguration<VehicleColor>
{
    public void Configure(EntityTypeBuilder<VehicleColor> builder)
    {
        builder.ToTable("VehicleColors");

        builder.HasKey(vehicleColor => vehicleColor.Id);

        builder.Property(vehicleColor => vehicleColor.Id)
            .HasConversion(
                vehicleColorId => vehicleColorId.Value,
                value => VehicleColorId.From(value))
            .ValueGeneratedNever();

        builder.Property(vehicleColor => vehicleColor.Name)
            .HasConversion(
                name => name.Value,
                value => VehicleColorName.Create(value))
            .HasMaxLength(VehicleColorName.MaxLength)
            .IsRequired();

        builder.HasIndex(vehicleColor => vehicleColor.Name)
            .IsUnique();

        builder.Property(vehicleColor => vehicleColor.CreatedAt)
            .IsRequired();

        builder.Property(vehicleColor => vehicleColor.UpdatedAt)
            .IsRequired();

        builder.Ignore(vehicleColor => vehicleColor.DomainEvents);
    }
}
