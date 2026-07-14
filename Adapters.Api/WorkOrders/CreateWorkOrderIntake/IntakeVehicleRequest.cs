namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrderIntake;

public sealed record IntakeVehicleRequest(
    string Plate,
    int Year,
    string Brand,
    string Model,
    string Color);
