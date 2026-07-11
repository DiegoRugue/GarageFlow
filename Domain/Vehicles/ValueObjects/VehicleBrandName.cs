using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public sealed record VehicleBrandName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private VehicleBrandName(string value)
    {
        Value = value;
    }

    public static VehicleBrandName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Vehicle brand name cannot be empty.");
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxLength)
        {
            throw new ValidationException($"Vehicle brand name cannot exceed {MaxLength} characters.");
        }

        return new VehicleBrandName(normalizedValue);
    }

    public override string ToString() => Value;

    public static implicit operator string(VehicleBrandName name) => name.Value;
}
