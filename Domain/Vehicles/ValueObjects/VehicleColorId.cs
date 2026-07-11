using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public readonly record struct VehicleColorId : IStronglyTypedId
{
    public Guid Value { get; }

    private VehicleColorId(Guid value)
    {
        Value = value;
    }

    public static VehicleColorId New() => new(Guid.NewGuid());

    public static VehicleColorId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Vehicle color identifier cannot be empty.");
        }

        return new VehicleColorId(value);
    }

    public override string ToString() => Value.ToString();
}
