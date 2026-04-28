using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Entities;

public sealed class VehicleColor : Entity<VehicleColorId>, IAggregateRoot
{
    public VehicleColorName Name { get; private set; }

    private VehicleColor(VehicleColorId id, VehicleColorName name) : base(id)
    {
        Name = name;
    }

    public static VehicleColor Create(string name)
    {
        var normalizedName = VehicleColorName.Create(name);
        var id = VehicleColorId.New();
        var color = new VehicleColor(id, normalizedName);

        color.RaiseDomainEvent(new VehicleColorCreated(
            VehicleColorId: id,
            Name: normalizedName.Value,
            CreatedAt: color.CreatedAt));

        return color;
    }

    public void Update(string name)
    {
        Name = VehicleColorName.Create(name);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleColorUpdated(
            VehicleColorId: Id,
            Name: Name.Value));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleColorDeleted(
            VehicleColorId: Id,
            Name: Name.Value));
    }
}
