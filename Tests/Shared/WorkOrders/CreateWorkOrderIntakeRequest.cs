namespace GarageFlow.Tests.Shared.WorkOrders;

public sealed record CreateWorkOrderIntakeRequest(
    Guid RequestId,
    IntakeCustomerRequest? Customer,
    IntakeVehicleRequest? Vehicle,
    IReadOnlyList<IntakeServiceRequest>? Services,
    IReadOnlyList<IntakeInventoryItemRequest>? InventoryItems);
