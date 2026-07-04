using GarageFlow.SharedKernel.Domain.Entities;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.Interfaces;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Entities;

public sealed class VehicleModel : Entity<VehicleModelId>, IAggregateRoot
{
    public VehicleBrandId VehicleBrandId { get; private set; }
    public VehicleModelName Name { get; private set; }

    private VehicleModel(VehicleModelId id, VehicleBrandId vehicleBrandId, VehicleModelName name) : base(id)
    {
        VehicleBrandId = EnsureValidBrandId(vehicleBrandId);
        Name = name;
    }

    public static VehicleModel Create(VehicleBrandId vehicleBrandId, string name)
    {
        var normalizedName = VehicleModelName.Create(name);
        var id = VehicleModelId.New();
        var validatedBrandId = EnsureValidBrandId(vehicleBrandId);
        var model = new VehicleModel(id, validatedBrandId, normalizedName);

        model.RaiseDomainEvent(new VehicleModelCreated(
            VehicleModelId: id,
            VehicleBrandId: validatedBrandId,
            Name: normalizedName.Value,
            CreatedAt: model.CreatedAt));

        return model;
    }

    public void Update(VehicleBrandId vehicleBrandId, string name)
    {
        VehicleBrandId = EnsureValidBrandId(vehicleBrandId);
        Name = VehicleModelName.Create(name);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleModelUpdated(
            VehicleModelId: Id,
            VehicleBrandId: VehicleBrandId,
            Name: Name.Value));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleModelDeleted(
            VehicleModelId: Id,
            VehicleBrandId: VehicleBrandId,
            Name: Name.Value));
    }

    private static VehicleBrandId EnsureValidBrandId(VehicleBrandId vehicleBrandId)
    {
        if (vehicleBrandId.Value == Guid.Empty)
        {
            throw new ValidationException("Vehicle brand identifier cannot be empty.");
        }

        return vehicleBrandId;
    }
}
