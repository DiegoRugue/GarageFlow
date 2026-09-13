using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderDailyMetrics;

public sealed record GetWorkOrderDailyMetricsQuery(DateOnly Date) : IRequest<GetWorkOrderDailyMetricsResult>;
