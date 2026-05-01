using Mediator;

namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeQuery(DateTime From, DateTime To) : IRequest<GetAverageServiceTimeResult>;
