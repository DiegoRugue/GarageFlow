using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public sealed record VehicleModelName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private VehicleModelName(string value)
    {
        Value = value;
    }

    public static VehicleModelName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Vehicle model name cannot be empty.");
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxLength)
        {
            throw new ValidationException($"Vehicle model name cannot exceed {MaxLength} characters.");
        }

        return new VehicleModelName(normalizedValue);
    }

    public override string ToString() => Value;

    public static implicit operator string(VehicleModelName name) => name.Value;
}
