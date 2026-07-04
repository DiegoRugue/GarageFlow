using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.StartEstimateService;

public sealed record StartEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : ICommand;
