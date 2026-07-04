using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Adapters.Infrastructure.Vehicles.Configurations;

public sealed class VehicleModelEntityConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> builder)
    {
        builder.ToTable("VehicleModels");

        builder.HasKey(vehicleModel => vehicleModel.Id);

        builder.Property(vehicleModel => vehicleModel.Id)
            .HasConversion(
                vehicleModelId => vehicleModelId.Value,
                value => VehicleModelId.From(value))
            .ValueGeneratedNever();

        builder.Property(vehicleModel => vehicleModel.VehicleBrandId)
            .HasConversion(
                vehicleBrandId => vehicleBrandId.Value,
                value => VehicleBrandId.From(value))
            .IsRequired();

        builder.Property(vehicleModel => vehicleModel.Name)
            .HasConversion(
                name => name.Value,
                value => VehicleModelName.Create(value))
            .HasMaxLength(VehicleModelName.MaxLength)
            .IsRequired();

        builder.HasAlternateKey(vehicleModel => new { vehicleModel.Id, vehicleModel.VehicleBrandId });

        builder.HasIndex(vehicleModel => new { vehicleModel.VehicleBrandId, vehicleModel.Name })
            .IsUnique();

        builder.HasOne<VehicleBrand>()
            .WithMany()
            .HasForeignKey(vehicleModel => vehicleModel.VehicleBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(vehicleModel => vehicleModel.CreatedAt)
            .IsRequired();

        builder.Property(vehicleModel => vehicleModel.UpdatedAt)
            .IsRequired();

        builder.Ignore(vehicleModel => vehicleModel.DomainEvents);
    }
}
