using GarageFlow.Api.WorkOrders.Responses;

namespace GarageFlow.Api.WorkOrders.ListWorkOrders;

public sealed record ListWorkOrdersResponse(
    IReadOnlyList<WorkOrderDetailsResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
