using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Entities;

public sealed class VehicleModel : Entity<VehicleModelId>, IAggregateRoot
{
    public const int MaxNameLength = 100;

    public VehicleBrandId VehicleBrandId { get; private set; }
    public string Name { get; private set; }

    private VehicleModel(VehicleModelId id) : base(id)
    {
        Name = null!;
    }

    private VehicleModel(VehicleModelId id, VehicleBrandId vehicleBrandId, string name) : base(id)
    {
        VehicleBrandId = EnsureValidBrandId(vehicleBrandId);
        Name = name;
    }

    public static VehicleModel Create(VehicleBrandId vehicleBrandId, string name)
    {
        var normalizedName = NormalizeName(name);
        var id = VehicleModelId.New();
        var validatedBrandId = EnsureValidBrandId(vehicleBrandId);
        var model = new VehicleModel(id, validatedBrandId, normalizedName);

        model.RaiseDomainEvent(new VehicleModelCreated(
            VehicleModelId: id,
            VehicleBrandId: validatedBrandId,
            Name: normalizedName,
            CreatedAt: model.CreatedAt));

        return model;
    }

    public void Update(VehicleBrandId vehicleBrandId, string name)
    {
        VehicleBrandId = EnsureValidBrandId(vehicleBrandId);
        Name = NormalizeName(name);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleModelUpdated(
            VehicleModelId: Id,
            VehicleBrandId: VehicleBrandId,
            Name: Name));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleModelDeleted(
            VehicleModelId: Id,
            VehicleBrandId: VehicleBrandId,
            Name: Name));
    }

    private static VehicleBrandId EnsureValidBrandId(VehicleBrandId vehicleBrandId)
    {
        if (vehicleBrandId.Value == Guid.Empty)
        {
            throw new ValidationException("Vehicle brand identifier cannot be empty.");
        }

        return vehicleBrandId;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Vehicle model name cannot be empty.");
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxNameLength)
        {
            throw new ValidationException($"Vehicle model name cannot exceed {MaxNameLength} characters.");
        }

        return normalizedValue;
    }
}
