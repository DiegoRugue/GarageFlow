using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public readonly record struct VehicleBrandId : IStronglyTypedId
{
    public Guid Value { get; }

    private VehicleBrandId(Guid value)
    {
        Value = value;
    }

    public static VehicleBrandId New() => new(Guid.NewGuid());

    public static VehicleBrandId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Vehicle brand identifier cannot be empty.");
        }

        return new VehicleBrandId(value);
    }

    public override string ToString() => Value.ToString();
}
