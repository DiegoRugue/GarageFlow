using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public readonly record struct VehicleModelId : IStronglyTypedId
{
    public Guid Value { get; }

    private VehicleModelId(Guid value)
    {
        Value = value;
    }

    public static VehicleModelId New() => new(Guid.NewGuid());

    public static VehicleModelId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Vehicle model identifier cannot be empty.");
        }

        return new VehicleModelId(value);
    }

    public override string ToString() => Value.ToString();
}
