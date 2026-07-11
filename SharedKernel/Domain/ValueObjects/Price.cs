using System.Globalization;
using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.SharedKernel.Domain.ValueObjects;

public sealed record Price
{
    public decimal Value { get; }

    private Price(decimal value)
    {
        Value = value;
    }

    public static Price Create(decimal value)
    {
        if (value < 0)
        {
            throw new ValidationException("Service price cannot be negative.");
        }

        if (decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
        {
            throw new ValidationException("Service price cannot have more than 2 decimal places.");
        }

        return new Price(value);
    }

    public override string ToString() => Value.ToString("0.00", CultureInfo.InvariantCulture);

    public static implicit operator decimal(Price price) => price.Value;
}
