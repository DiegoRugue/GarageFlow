using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetAverageServiceTime;

public sealed record GetAverageServiceTimeQuery(DateTime From, DateTime To, Guid? ServiceId = null)
    : IRequest<GetAverageServiceTimeResult>;
