using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(Guid CustomerId, Guid VehicleId) : ICommand<CreateWorkOrderResult>;
