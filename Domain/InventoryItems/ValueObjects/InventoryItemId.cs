using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.ValueObjects;

public readonly record struct InventoryItemId : IStronglyTypedId
{
    public Guid Value { get; }

    private InventoryItemId(Guid value)
    {
        Value = value;
    }

    public static InventoryItemId New() => new(Guid.NewGuid());

    public static InventoryItemId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Inventory item identifier cannot be empty.");
        }

        return new InventoryItemId(value);
    }

    public override string ToString() => Value.ToString();
}
