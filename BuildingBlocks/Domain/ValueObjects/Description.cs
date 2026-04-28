using GarageFlow.BuildingBlocks.Domain.Exceptions;

namespace GarageFlow.BuildingBlocks.Domain.ValueObjects;

public sealed record Description
{
    public const int MaxLength = 200;

    public string Value { get; }

    private Description(string value)
    {
        Value = value;
    }

    public static Description Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Service description cannot be empty or whitespace.");
        }

        if (value.Length > MaxLength)
        {
            throw new ValidationException($"Service description cannot exceed {MaxLength} characters.");
        }

        return new Description(value.Trim());
    }

    public override string ToString() => Value;

    public static implicit operator string(Description description) => description.Value;
}
