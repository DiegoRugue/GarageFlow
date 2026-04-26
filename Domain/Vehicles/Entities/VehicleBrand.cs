using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Entities;

public sealed class VehicleBrand : Entity<VehicleBrandId>, IAggregateRoot
{
    public const int MaxNameLength = 100;

    public string Name { get; private set; }

    private VehicleBrand(VehicleBrandId id) : base(id)
    {
        Name = null!;
    }

    private VehicleBrand(VehicleBrandId id, string name) : base(id)
    {
        Name = name;
    }

    public static VehicleBrand Create(string name)
    {
        var normalizedName = NormalizeName(name, "Vehicle brand name");
        var id = VehicleBrandId.New();
        var brand = new VehicleBrand(id, normalizedName);

        brand.RaiseDomainEvent(new VehicleBrandCreated(
            VehicleBrandId: id,
            Name: normalizedName,
            CreatedAt: brand.CreatedAt));

        return brand;
    }

    public void Update(string name)
    {
        Name = NormalizeName(name, "Vehicle brand name");
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleBrandUpdated(
            VehicleBrandId: Id,
            Name: Name));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleBrandDeleted(
            VehicleBrandId: Id,
            Name: Name));
    }

    private static string NormalizeName(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} cannot be empty.");
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxNameLength)
        {
            throw new ValidationException($"{fieldName} cannot exceed {MaxNameLength} characters.");
        }

        return normalizedValue;
    }
}
