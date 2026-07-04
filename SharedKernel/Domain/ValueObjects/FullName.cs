using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.SharedKernel.Domain.ValueObjects;

public sealed record FullName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private FullName(string value)
    {
        Value = value;
    }

    public static FullName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Full name cannot be empty or whitespace.");
        }

        if (value.Length > MaxLength)
        {
            throw new ValidationException($"Full name cannot exceed {MaxLength} characters.");
        }

        return new FullName(value.Trim());
    }

    public override string ToString() => Value;

    public static implicit operator string(FullName name) => name.Value;
}
