using GarageFlow.BuildingBlocks.Domain.Exceptions;
using System.Globalization;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public readonly record struct VehicleYear
{
    public int Value { get; }

    private VehicleYear(int value)
    {
        Value = value;
    }

    public static VehicleYear Create(int value)
    {
        if (value < 1)
        {
            throw new ValidationException("Vehicle year must be greater than or equal to 1.");
        }

        return new VehicleYear(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static implicit operator int(VehicleYear year) => year.Value;
}
