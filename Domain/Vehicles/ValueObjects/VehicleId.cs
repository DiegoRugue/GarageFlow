using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public readonly record struct VehicleId : IStronglyTypedId
{
    public Guid Value { get; }

    private VehicleId(Guid value)
    {
        Value = value;
    }

    public static VehicleId New() => new(Guid.NewGuid());

    public static VehicleId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Vehicle identifier cannot be empty.");
        }

        return new VehicleId(value);
    }

    public override string ToString() => Value.ToString();
}
