using GarageFlow.Api.WorkOrders.Responses;

namespace GarageFlow.Api.WorkOrders.ListMyWorkOrders;

public sealed record ListMyWorkOrdersResponse(
    IReadOnlyList<CustomerWorkOrderDetailsResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
