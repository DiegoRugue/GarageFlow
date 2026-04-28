using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Vehicles.Configurations;

public sealed class VehicleEntityConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(vehicle => vehicle.Id);

        builder.Property(vehicle => vehicle.Id)
            .HasConversion(
                vehicleId => vehicleId.Value,
                value => VehicleId.From(value))
            .ValueGeneratedNever();

        builder.Property(vehicle => vehicle.CustomerId)
            .HasConversion(
                customerId => customerId.Value,
                value => CustomerId.From(value))
            .IsRequired();

        builder.Property(vehicle => vehicle.Year)
            .HasConversion(
                year => year.Value,
                value => VehicleYear.Create(value))
            .IsRequired();

        builder.Property(vehicle => vehicle.VehicleBrandId)
            .HasConversion(
                vehicleBrandId => vehicleBrandId.Value,
                value => VehicleBrandId.From(value))
            .IsRequired();

        builder.Property(vehicle => vehicle.VehicleModelId)
            .HasConversion(
                vehicleModelId => vehicleModelId.Value,
                value => VehicleModelId.From(value))
            .IsRequired();

        builder.Property(vehicle => vehicle.VehicleColorId)
            .HasConversion(
                vehicleColorId => vehicleColorId.Value,
                value => VehicleColorId.From(value))
            .IsRequired();

        builder.Property(vehicle => vehicle.LicensePlate)
            .HasConversion(
                licensePlate => licensePlate.Value,
                value => LicensePlate.Create(value))
            .HasMaxLength(7)
            .IsRequired();

        builder.HasIndex(vehicle => vehicle.LicensePlate)
            .IsUnique();

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VehicleBrand>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.VehicleBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VehicleModel>()
            .WithMany()
            .HasForeignKey(vehicle => new { vehicle.VehicleModelId, vehicle.VehicleBrandId })
            .HasPrincipalKey(vehicleModel => new { vehicleModel.Id, vehicleModel.VehicleBrandId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VehicleColor>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.VehicleColorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(vehicle => vehicle.CreatedAt)
            .IsRequired();

        builder.Property(vehicle => vehicle.UpdatedAt)
            .IsRequired();

        builder.Ignore(vehicle => vehicle.DomainEvents);
    }
}
