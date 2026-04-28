using GarageFlow.BuildingBlocks.Domain.Exceptions;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public sealed record VehicleColorName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private VehicleColorName(string value)
    {
        Value = value;
    }

    public static VehicleColorName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Vehicle color name cannot be empty.");
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxLength)
        {
            throw new ValidationException($"Vehicle color name cannot exceed {MaxLength} characters.");
        }

        return new VehicleColorName(normalizedValue);
    }

    public override string ToString() => Value;

    public static implicit operator string(VehicleColorName name) => name.Value;
}
