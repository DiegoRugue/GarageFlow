using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Entities;

public sealed class EstimateInventoryLine : Entity<EstimateInventoryLineId>
{
    public EstimateId EstimateId { get; private set; }
    public InventoryItemId InventoryItemId { get; private set; }
    public Description DescriptionSnapshot { get; private set; }
    public EstimateItemQuantity Quantity { get; private set; }
    public Price UnitCost { get; private set; }
    public Price UnitPrice { get; private set; }
    public Price TotalPrice => Price.Create(UnitPrice.Value * Quantity.Value);

    private EstimateInventoryLine(
        EstimateInventoryLineId id,
        EstimateId estimateId,
        InventoryItemId inventoryItemId,
        Description descriptionSnapshot,
        EstimateItemQuantity quantity,
        Price unitCost,
        Price unitPrice) : base(id)
    {
        EstimateId = EnsureValidEstimateId(estimateId);
        InventoryItemId = EnsureValidInventoryItemId(inventoryItemId);
        DescriptionSnapshot = EnsureDescription(descriptionSnapshot);
        Quantity = EstimateItemQuantity.Create(quantity.Value);
        UnitCost = EnsurePrice(unitCost);
        UnitPrice = EnsurePrice(unitPrice);
    }

    public static EstimateInventoryLine Create(
        EstimateId estimateId,
        InventoryItemId inventoryItemId,
        Description description,
        EstimateItemQuantity quantity,
        Price unitCost,
        Price unitPrice)
    {
        return new EstimateInventoryLine(
            EstimateInventoryLineId.New(),
            estimateId,
            inventoryItemId,
            description,
            quantity,
            unitCost,
            unitPrice);
    }

    private static EstimateId EnsureValidEstimateId(EstimateId estimateId)
    {
        if (estimateId.Value == Guid.Empty)
        {
            throw new ValidationException("Estimate identifier cannot be empty.");
        }

        return estimateId;
    }

    private static InventoryItemId EnsureValidInventoryItemId(InventoryItemId inventoryItemId)
    {
        if (inventoryItemId.Value == Guid.Empty)
        {
            throw new ValidationException("Inventory item identifier cannot be empty.");
        }

        return inventoryItemId;
    }

    private static Description EnsureDescription(Description description)
    {
        if (description is null)
        {
            throw new ValidationException("Estimate inventory line description cannot be null.");
        }

        return description;
    }

    private static Price EnsurePrice(Price price)
    {
        if (price is null)
        {
            throw new ValidationException("Estimate inventory line price cannot be null.");
        }

        return price;
    }
}
