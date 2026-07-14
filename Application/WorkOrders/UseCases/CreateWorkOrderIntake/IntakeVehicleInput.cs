namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed record IntakeVehicleInput(
    string Plate,
    int Year,
    string Brand,
    string Model,
    string Color);
