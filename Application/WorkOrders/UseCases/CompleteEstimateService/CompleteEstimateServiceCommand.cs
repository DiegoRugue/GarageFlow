using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.CompleteEstimateService;

public sealed record CompleteEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : ICommand;
