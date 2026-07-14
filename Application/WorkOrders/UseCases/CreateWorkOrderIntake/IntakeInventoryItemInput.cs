namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed record IntakeInventoryItemInput(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    int Quantity);
