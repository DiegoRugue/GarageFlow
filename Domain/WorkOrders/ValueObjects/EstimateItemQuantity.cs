using GarageFlow.BuildingBlocks.Domain.Exceptions;
using System.Globalization;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public readonly record struct EstimateItemQuantity
{
    public int Value { get; }

    private EstimateItemQuantity(int value)
    {
        Value = value;
    }

    public static EstimateItemQuantity Create(int value)
    {
        if (value <= 0)
        {
            throw new ValidationException("Estimate item quantity must be greater than zero.");
        }

        return new EstimateItemQuantity(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static implicit operator int(EstimateItemQuantity quantity) => quantity.Value;
}
