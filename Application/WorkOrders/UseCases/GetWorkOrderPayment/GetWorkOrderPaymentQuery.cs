using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderPayment;

public sealed record GetWorkOrderPaymentQuery(Guid Id, Guid? CustomerUserId = null)
    : IRequest<GetWorkOrderPaymentResult>;
