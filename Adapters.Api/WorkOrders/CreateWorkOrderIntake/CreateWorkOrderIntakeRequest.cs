namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrderIntake;

public sealed record CreateWorkOrderIntakeRequest(
    Guid RequestId,
    IntakeCustomerRequest? Customer,
    IntakeVehicleRequest? Vehicle,
    IReadOnlyList<IntakeServiceRequest>? Services,
    IReadOnlyList<IntakeInventoryItemRequest>? InventoryItems);
