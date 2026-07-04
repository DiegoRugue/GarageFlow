using GarageFlow.Adapters.Api.WorkOrders.Responses;

namespace GarageFlow.Adapters.Api.WorkOrders.ListWorkOrders;

public sealed record ListWorkOrdersResponse(
    IReadOnlyList<WorkOrderDetailsResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
