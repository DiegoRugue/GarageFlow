namespace GarageFlow.Tests.Shared.WorkOrders;

public sealed record IntakeVehicleRequest(
    string Plate,
    int Year,
    string Brand,
    string Model,
    string Color);
