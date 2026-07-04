using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;

namespace GarageFlow.Application.WorkOrders.UseCases.ListWorkOrders;

public sealed record ListWorkOrdersResult(
    IReadOnlyList<WorkOrderDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
