using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Domain.InventoryItems.ValueObjects;

public sealed record InventoryItemName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private InventoryItemName(string value)
    {
        Value = value;
    }

    public static InventoryItemName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Inventory item name cannot be empty.");
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxLength)
        {
            throw new ValidationException($"Inventory item name cannot exceed {MaxLength} characters.");
        }

        return new InventoryItemName(normalizedValue);
    }

    public override string ToString() => Value;

    public static implicit operator string(InventoryItemName name) => name.Value;
}
