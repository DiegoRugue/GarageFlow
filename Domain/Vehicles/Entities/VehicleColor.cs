using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Entities;

public sealed class VehicleColor : Entity<VehicleColorId>, IAggregateRoot
{
    public const int MaxNameLength = 100;

    public string Name { get; private set; }

    private VehicleColor(VehicleColorId id) : base(id)
    {
        Name = null!;
    }

    private VehicleColor(VehicleColorId id, string name) : base(id)
    {
        Name = name;
    }

    public static VehicleColor Create(string name)
    {
        var normalizedName = NormalizeName(name);
        var id = VehicleColorId.New();
        var color = new VehicleColor(id, normalizedName);

        color.RaiseDomainEvent(new VehicleColorCreated(
            VehicleColorId: id,
            Name: normalizedName,
            CreatedAt: color.CreatedAt));

        return color;
    }

    public void Update(string name)
    {
        Name = NormalizeName(name);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleColorUpdated(
            VehicleColorId: Id,
            Name: Name));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleColorDeleted(
            VehicleColorId: Id,
            Name: Name));
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Vehicle color name cannot be empty.");
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxNameLength)
        {
            throw new ValidationException($"Vehicle color name cannot exceed {MaxNameLength} characters.");
        }

        return normalizedValue;
    }
}
