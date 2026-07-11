using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.SubmitEstimate;

public sealed record SubmitEstimateCommand(Guid WorkOrderId, Guid EstimateId) : ICommand;
