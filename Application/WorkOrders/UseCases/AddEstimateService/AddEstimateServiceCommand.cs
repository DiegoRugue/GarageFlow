using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.AddEstimateService;

public sealed record AddEstimateServiceCommand(Guid WorkOrderId, Guid EstimateId, Guid ServiceId) : ICommand<AddEstimateServiceResult>;
