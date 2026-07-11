using GarageFlow.Adapters.Api.WorkOrders.Responses;

namespace GarageFlow.Adapters.Api.WorkOrders.ListMyWorkOrders;

public sealed record ListMyWorkOrdersResponse(
    IReadOnlyList<CustomerWorkOrderDetailsResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
