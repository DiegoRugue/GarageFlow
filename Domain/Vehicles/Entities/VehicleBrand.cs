using GarageFlow.SharedKernel.Domain.Entities;
using GarageFlow.SharedKernel.Domain.Interfaces;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Entities;

public sealed class VehicleBrand : Entity<VehicleBrandId>, IAggregateRoot
{
    public VehicleBrandName Name { get; private set; }

    private VehicleBrand(VehicleBrandId id, VehicleBrandName name) : base(id)
    {
        Name = name;
    }

    public static VehicleBrand Create(string name)
    {
        var normalizedName = VehicleBrandName.Create(name);
        var id = VehicleBrandId.New();
        var brand = new VehicleBrand(id, normalizedName);

        brand.RaiseDomainEvent(new VehicleBrandCreated(
            VehicleBrandId: id,
            Name: normalizedName.Value,
            CreatedAt: brand.CreatedAt));

        return brand;
    }

    public void Update(string name)
    {
        Name = VehicleBrandName.Create(name);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleBrandUpdated(
            VehicleBrandId: Id,
            Name: Name.Value));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleBrandDeleted(
            VehicleBrandId: Id,
            Name: Name.Value));
    }
}
