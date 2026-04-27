using System.Globalization;
using GarageFlow.BuildingBlocks.Domain.Exceptions;

namespace GarageFlow.Domain.Services.ValueObjects;

public sealed record ServicePrice
{
    public decimal Value { get; }

    private ServicePrice(decimal value)
    {
        Value = value;
    }

    public static ServicePrice Create(decimal value)
    {
        if (value < 0)
        {
            throw new ValidationException("Service price cannot be negative.");
        }

        if (decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
        {
            throw new ValidationException("Service price cannot have more than 2 decimal places.");
        }

        return new ServicePrice(value);
    }

    public override string ToString() => Value.ToString("0.00", CultureInfo.InvariantCulture);

    public static implicit operator decimal(ServicePrice price) => price.Value;
}
