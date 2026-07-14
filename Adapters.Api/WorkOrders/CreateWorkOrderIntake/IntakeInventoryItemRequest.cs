namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrderIntake;

public sealed record IntakeInventoryItemRequest(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    int Quantity);
