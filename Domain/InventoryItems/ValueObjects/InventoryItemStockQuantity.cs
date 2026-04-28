using GarageFlow.BuildingBlocks.Domain.Exceptions;
using System.Globalization;

namespace GarageFlow.Domain.InventoryItems.ValueObjects;

public readonly record struct InventoryItemStockQuantity
{
    public int Value { get; }

    private InventoryItemStockQuantity(int value)
    {
        Value = value;
    }

    public static InventoryItemStockQuantity Create(int value)
    {
        if (value < 0)
        {
            throw new ValidationException("Inventory item stock quantity cannot be negative.");
        }

        return new InventoryItemStockQuantity(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static implicit operator int(InventoryItemStockQuantity stockQuantity) => stockQuantity.Value;
}
