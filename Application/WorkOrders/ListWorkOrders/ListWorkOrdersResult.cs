using GarageFlow.Application.WorkOrders.GetWorkOrderById;

namespace GarageFlow.Application.WorkOrders.ListWorkOrders;

public sealed record ListWorkOrdersResult(
    IReadOnlyList<WorkOrderDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
