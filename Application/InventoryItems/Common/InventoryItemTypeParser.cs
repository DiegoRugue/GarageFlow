using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Application.InventoryItems.Common;

public static class InventoryItemTypeParser
{
    public static InventoryItemType Parse(string value)
    {
        var normalized = value?.Trim();
        var canonicalName = Enum.GetNames<InventoryItemType>()
            .SingleOrDefault(name => string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase));
        if (canonicalName is not null)
        {
            return Enum.Parse<InventoryItemType>(canonicalName);
        }

        throw new ValidationException(
            $"Inventory item type '{value}' is invalid. Allowed values: Part, Supply.");
    }
}
