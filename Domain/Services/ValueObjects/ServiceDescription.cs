using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Domain.Services.ValueObjects;

public sealed record ServiceDescription
{
    public const int MaxLength = 200;

    public string Value { get; }

    private ServiceDescription(string value)
    {
        Value = value;
    }

    public static ServiceDescription Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Service description cannot be empty or whitespace.");
        }

        if (value.Length > MaxLength)
        {
            throw new ValidationException($"Service description cannot exceed {MaxLength} characters.");
        }

        return new ServiceDescription(value.Trim());
    }

    public override string ToString() => Value;

    public static implicit operator string(ServiceDescription description) => description.Value;
}
