using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed record CreateWorkOrderIntakeCommand(
    Guid RequestId,
    IntakeCustomerInput? Customer,
    IntakeVehicleInput? Vehicle,
    IReadOnlyList<IntakeServiceInput>? Services,
    IReadOnlyList<IntakeInventoryItemInput>? InventoryItems)
    : ICommand<CreateWorkOrderIntakeResult>, ICorrelatedCommand
{
    public string CorrelationId => RequestId.ToString("D");
}
