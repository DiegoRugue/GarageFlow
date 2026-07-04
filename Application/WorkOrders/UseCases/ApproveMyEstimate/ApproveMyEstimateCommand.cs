using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.ApproveMyEstimate;

public sealed record ApproveMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : ICommand;
